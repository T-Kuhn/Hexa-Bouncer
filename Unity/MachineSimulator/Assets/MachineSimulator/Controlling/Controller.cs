using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MachineSimulator.ImageProcessing;
using MachineSimulator.Machine;
using MachineSimulator.Sequencing;
using UnityEngine;

namespace MachineSimulator.Controlling
{
    public sealed class Controller : MonoBehaviour
    {
        [SerializeField] private SequenceCreator _sequenceCreator;
        [SerializeField] private MachineModel.MachineModel _machineModel;
        [SerializeField] private RealMachine _realMachine;
        [SerializeField] private ModeSwitcher _modeSwitcher;
        [SerializeField] private RestHeightController _restHeightController;
        private float _appliedRestIncrease;

        [SerializeField] private MonoBehaviour _cameOne;
        private IBallPositionProvider BallPositionProviderOne => _cameOne as IBallPositionProvider;

        [SerializeField] private MonoBehaviour _camTwo;
        private IBallPositionProvider BallPositionProviderTwo => _camTwo as IBallPositionProvider;

        private Transform _cameraOneTransform;
        private Transform _cameraTwoTransform;

        private Vector3 _camOneDetectedBallDir;
        private Vector3 _camTwoDetectedBallDir;

        [SerializeField] private Transform _planeOneOrigin;
        [SerializeField] private Transform _planeTwoOrigin;

        [SerializeField] private Transform _ballVisualization;

        private Vector3? _ballPosition;
        private float _lastTimestamp;

        private readonly BallVelocityRegression _ballVelocityRegression = new BallVelocityRegression();

        private readonly struct BounceProfile
        {
            public readonly float CommandTime;
            public readonly float UpHeightOffset;

            public BounceProfile(float commandTime, float upHeightOffset)
            {
                CommandTime = commandTime;
                UpHeightOffset = upHeightOffset;
            }
        }

        // AUDIT THESE PAIRS — a too-fast CommandTime for the given UpHeightOffset is dangerous for the machine.
        // UpHeightOffset is added on top of HexaPlateMover.RestPosition.y at execution time.
        private static readonly BounceProfile SlowBounce = new BounceProfile(0.225f, 0.07f);
        private static readonly BounceProfile HighBounce = new BounceProfile(0.225f, 0.09f);
        private static readonly BounceProfile FastBounce = new BounceProfile(0.15f, 0.04f);
        private static readonly BounceProfile TinyBounce = new BounceProfile(0.15f, 0.0f);

        // NOTE: We should be able to go as high as 0.05f, need to be careful though because in the worst case scenario,
        //       the machine will try to go from X:0.05 to X:-0.05 in one single down-move;
        //       Steppers might not be able to keep up (this might be too high velocity for some of the arms)
        //       So because of the above, we limit the maximum X translation to a lower value for now.
        private const float XZTranslationMax = 0.03f;
        private const float XZTranslationPerBounceMax = 0.01f;

        private void Start()
        {
            RunMachineLoopAsync().Forget();
        }

        private readonly PID _zAxisPid = new PID();
        private readonly PID _xAxisPid = new PID();

        private BounceProfile? GetNextBounceProfile(BallHandlingMode currentMode, bool isFastBounce, int tinyBounceStepState)
        {
            switch (currentMode)
            {
                case BallHandlingMode.None: return null;
                case BallHandlingMode.SlowBouncing: return SlowBounce;
                case BallHandlingMode.HighBouncing: return HighBounce;
                case BallHandlingMode.FastBouncing: return FastBounce;
                case BallHandlingMode.Alternating: return isFastBounce ? FastBounce : SlowBounce;
                case BallHandlingMode.ScrewBouncing: return SlowBounce;
                case BallHandlingMode.RanTinyBounce:
                    if (tinyBounceStepState == 1) return TinyBounce;
                    if (tinyBounceStepState == 2) return FastBounce;
                    return SlowBounce;
                case BallHandlingMode.XZFollowing: return FastBounce;
                default: return SlowBounce;
            }
        }

        private float GetTimeThreshold(BounceProfile profile)
        {
            // NOTE: In an ideal world, we'd want to start moving up commandTime/2f before ball hits because our up motion takes commandTime in total
            //       but because it takes a bit of time for our commands to get to the microcontroller, adding 45ms leads to better results.
            //       So it's basically commandTime/2 + 15ms
            return profile.CommandTime / 2f + 0.015f;
        }

        private UniTask ExecuteBounceAsync(BounceProfile profile, bool useRealMachine, float zCorrection, float xCorrection, float xOffset = 0f, float zOffset = 0f, float yRotAngle = 0f)
        {
            var totalIncrease = _restHeightController.AccumulatedIncrease;
            var deltaThisBounce = totalIncrease - _appliedRestIncrease;
            _appliedRestIncrease = totalIncrease;

            var currentRest = _machineModel.HexaPlateMover.RestPosition;
            var clampedDx = Mathf.Clamp(xOffset - currentRest.x, -XZTranslationPerBounceMax, XZTranslationPerBounceMax);
            var clampedDz = Mathf.Clamp(zOffset - currentRest.z, -XZTranslationPerBounceMax, XZTranslationPerBounceMax);
            var clampedXOffset = currentRest.x + clampedDx;
            var clampedZOffset = currentRest.z + clampedDz;
            clampedXOffset = Mathf.Clamp(clampedXOffset, -XZTranslationMax, XZTranslationMax);
            clampedZOffset = Mathf.Clamp(clampedZOffset, -XZTranslationMax, XZTranslationMax);

            return SequenceFromCode.GoUpAndDownAsync(
                _machineModel,
                _sequenceCreator,
                profile.CommandTime,
                CancellationToken.None,
                useRealMachine,
                zCorrection,
                xCorrection,
                profile.UpHeightOffset,
                deltaThisBounce,
                clampedXOffset,
                clampedZOffset,
                yRotAngle
            );
        }

        private async UniTask RunMachineLoopAsync()
        {
            const bool useRealMachine = true;
            var isFastBounce = false;
            var bounceCount = 0;
            var tinyBounceStepState = 0;

            while (true)
            {
                var profile = GetNextBounceProfile(_modeSwitcher.CurrentMode, isFastBounce, tinyBounceStepState);

                if (profile == null || _ballPosition == null)
                {
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, CancellationToken.None);
                    continue;
                }

                var plateMidHeight = _machineModel.HexaPlateMover.RestPosition.y + profile.Value.UpHeightOffset / 2f;
                var timeUntilNextImpact = TimeUntilNextImpact.Calculate(_ballPosition.Value.y, _realTimeVelocity.y, plateMidHeight);
                var timeThreshold = GetTimeThreshold(profile.Value);

                if (_ballPosition.HasValue
                    && (BallPositionProviderOne is { IsBallDetected: true } || BallPositionProviderTwo is { IsBallDetected: true })
                    && (!useRealMachine || _realMachine.IsReady)
                    && timeUntilNextImpact.HasValue
                    && timeUntilNextImpact.Value < timeThreshold)
                {
                    // NOTE: ball movement along x axis is driving PID for correction around Z axis
                    //       ball movement along z axis is driving PID for correction around X axis
                    // NOTE: ballPosition stream is plate-relative (cameras are attached to the HexaPlate).
                    //       Add the plate's world x/z to recover the absolute ball position for the PID.
                    var zCorrection = _xAxisPid.Update(_ballPosition.Value.x);
                    var xCorrection = -_zAxisPid.Update(_ballPosition.Value.z);


                    switch (_modeSwitcher.CurrentMode)
                    {
                        case BallHandlingMode.None:
                            await UniTask.Delay(TimeSpan.FromMilliseconds(150));
                            break;

                        case BallHandlingMode.SlowBouncing:
                            await ExecuteBounceAsync(SlowBounce, useRealMachine, zCorrection, xCorrection);
                            break;

                        case BallHandlingMode.HighBouncing:
                            await ExecuteBounceAsync(HighBounce, useRealMachine, zCorrection, xCorrection);
                            break;

                        case BallHandlingMode.FastBouncing:
                            await ExecuteBounceAsync(FastBounce, useRealMachine, zCorrection, xCorrection);
                            break;

                        case BallHandlingMode.Alternating:
                            await ExecuteBounceAsync(isFastBounce ? FastBounce : SlowBounce, useRealMachine, zCorrection, xCorrection);
                            isFastBounce = !isFastBounce;
                            break;

                        case BallHandlingMode.ScrewBouncing:
                            var yRotAngle = bounceCount % 2 == 0 ? 5f : -5f;
                            await ExecuteBounceAsync(profile.Value, useRealMachine, zCorrection, xCorrection, yRotAngle: yRotAngle);
                            break;

                        case BallHandlingMode.RanTinyBounce:
                            if (tinyBounceStepState == 1)
                            {
                                await ExecuteBounceAsync(TinyBounce, useRealMachine, zCorrection, xCorrection);
                                tinyBounceStepState = 2;
                                break;
                            }

                            if (tinyBounceStepState == 2)
                            {
                                await ExecuteBounceAsync(FastBounce, useRealMachine, zCorrection, xCorrection);
                                tinyBounceStepState = 0;
                                break;
                            }

                            await ExecuteBounceAsync(HighBounce, useRealMachine, zCorrection, xCorrection);
                            if (UnityEngine.Random.Range(0f, 1f) > 0.9f)
                            {
                                tinyBounceStepState = 1;
                            }

                            break;

                        case BallHandlingMode.XZFollowing:
                        {
                            var xOffset = 0f;
                            var zOffset = 0f;

                            // NOTE: HexaPlate rest position must be at least 0.2f high before applying xz-translations.
                            //       Below that height the arms would hit the table when translating left/right/front/back.
                            if (_machineModel.HexaPlateMover.RestPosition.y > 0.199f)
                            {
                                xOffset = Mathf.Clamp(_ballPosition.Value.x, -XZTranslationMax, XZTranslationMax);
                                zOffset = Mathf.Clamp(_ballPosition.Value.z, -XZTranslationMax, XZTranslationMax);
                            }
                            else
                            {
                                Debug.Log("XZFollowing: skipping xz-translations because RestPosition.y is not above 0.2");
                            }

                            await ExecuteBounceAsync(profile.Value, useRealMachine, zCorrection, xCorrection, xOffset, zOffset);
                            break;
                        }

                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    bounceCount++;
                }

                if (_modeSwitcher.CurrentMode != BallHandlingMode.Alternating)
                {
                    isFastBounce = false;
                }

                // NOTE: Needs to run after LateUpdate to ensure that we get newest ball position data.
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, CancellationToken.None);
            }
        }


        private void OnValidate()
        {
            if (_cameOne != null && !(_cameOne is IBallPositionProvider))
            {
                Debug.LogError($"{_cameOne.name} does not implement IBallPositionProvider!");
                _cameOne = null;
            }

            if (_camTwo != null && !(_camTwo is IBallPositionProvider))
            {
                Debug.LogError($"{_camTwo.name} does not implement IBallPositionProvider!");
                _camTwo = null;
            }
        }

        public void InjectRefs(Transform cameraOneTransform, Transform cameraTwoTransform)
        {
            _cameraOneTransform = cameraOneTransform;
            _cameraTwoTransform = cameraTwoTransform;
        }

        private bool _isLogging;
        private readonly List<string> _ballPositionLogs = new List<string>();
        private Vector3 _realTimeVelocity;

        // NOTE: LateUpdate because we get newest ball position in Update.
        //       Using LateUpdate to make sure we always get the newest position data.
        private void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                Debug.Log("Start");
                _isLogging = true;
                _ballPositionLogs.Clear();
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                Debug.Log("End");
                _isLogging = false;
                File.WriteAllLines($"ballpositionlogs.txt", _ballPositionLogs);
                _ballPositionLogs.Clear();
            }

            if (BallPositionProviderOne != null && _cameraOneTransform != null)
            {
                _camOneDetectedBallDir = CalculateDetectedBallDirection(_cameraOneTransform, BallPositionProviderOne.NewestBallPosition);
                AlignPlane(_planeOneOrigin, _cameraOneTransform, _camOneDetectedBallDir);
            }

            if (BallPositionProviderTwo != null && _cameraTwoTransform != null)
            {
                _camTwoDetectedBallDir = CalculateDetectedBallDirection(_cameraTwoTransform, BallPositionProviderTwo.NewestBallPosition);
                AlignPlane(_planeTwoOrigin, _cameraTwoTransform, _camTwoDetectedBallDir);
            }

            var (ballposition, timestamp) = CalculateBallPositionFromIntersection();
            _ballPosition = ballposition;
            if (
                _ballPosition.HasValue
                && timestamp.HasValue
                && (Mathf.Abs(timestamp.Value - _lastTimestamp) > 0.0001f)
                && (BallPositionProviderOne is { IsBallDetected: true } || BallPositionProviderTwo is { IsBallDetected: true }))
            {
                _ballVisualization.position = _ballPosition.Value;
                _ballVelocityRegression.AddSample(timestamp.Value, _ballPosition.Value);
                _realTimeVelocity = _ballVelocityRegression.CalculateRealTimeVelocity();
                var plateRestHeight = _machineModel.HexaPlateMover.RestPosition.y;
                var timeUntilNextImpactAtRestHeight = TimeUntilNextImpact.Calculate(_ballPosition.Value.y, _realTimeVelocity.y, plateRestHeight);

                if (_isLogging && timeUntilNextImpactAtRestHeight.HasValue)
                {
                    _ballPositionLogs.Add($"{timestamp};{_ballPosition.Value.x};{_ballPosition.Value.y};{_ballPosition.Value.z};{timeUntilNextImpactAtRestHeight.Value}");
                }

                _lastTimestamp = timestamp.Value;
            }
        }

        private Vector3 CalculateDetectedBallDirection(Transform cameraTransform, Vector2 ballPosition)
        {
            var (horizontal, vertical) = Converter.ConvertToAngle(ballPosition);
            var rotation = cameraTransform.rotation * Quaternion.Euler(vertical, horizontal, 0f);
            return rotation * Vector3.forward;
        }

        private (Vector3? BallPosition, float? TimeStamp) CalculateBallPositionFromIntersection()
        {
            if (BallPositionProviderOne == null || BallPositionProviderTwo == null ||
                _cameraOneTransform == null || _cameraTwoTransform == null ||
                _planeOneOrigin == null || _planeTwoOrigin == null)
            {
                return (null, null);
            }

            // Step1: Shoot rays from both cameras onto both corresponding planes
            var intersectionOnPlaneOne = ShootRayAtPlane(_cameraTwoTransform, _camTwoDetectedBallDir, LayerMask.GetMask("PlaneOne"));
            var intersectionOnPlaneTwo = ShootRayAtPlane(_cameraOneTransform, _camOneDetectedBallDir, LayerMask.GetMask("PlaneTwo"));

            // Step2: If the data of both cameras is new (within 10ms), we return the average of the two intersection points
            if (Mathf.Abs(BallPositionProviderOne.TimeStamp - BallPositionProviderTwo.TimeStamp) < 0.01f)
            {
                var averageTimeStamp = (BallPositionProviderOne.TimeStamp + BallPositionProviderTwo.TimeStamp) / 2f;
                return ((intersectionOnPlaneOne + intersectionOnPlaneTwo) / 2f, averageTimeStamp);
            }

            // NOTE: We add a time offset to make sure that there's a bias towards camOne data being the oldest data
            //       We do this because without this bias - and because the two cameras aren't exactly aligned - we get
            //       position data that is a bit jittery.
            var biasOffset = 0.01f;

            // Step3: Figure out which ballposition is the oldest
            var camOneIsOldest = BallPositionProviderOne.TimeStamp <= BallPositionProviderTwo.TimeStamp + biasOffset;

            // Step4: Return the intersection point depending on which camera's data is the oldest
            return camOneIsOldest ? (intersectionOnPlaneOne, BallPositionProviderTwo.TimeStamp) : (intersectionOnPlaneTwo, BallPositionProviderOne.TimeStamp);
        }

        private Vector3? ShootRayAtPlane(Transform rayOrigin, Vector3 rayDirection, int targetLayerMask)
        {
            var ray = new Ray(rayOrigin.position, rayDirection);

            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, targetLayerMask))
            {
                return hit.point;
            }

            return null;
        }

        private void AlignPlane(Transform planeOrigin, Transform cameraTransform, Vector3 detectedBallDir)
        {
            // Step1: Place plane origin at camera origin
            planeOrigin.position = cameraTransform.position;

            // Step2: Rotate plane so that plane.forward is aligned with detectedBallDir
            var rot = Quaternion.LookRotation(detectedBallDir);

            // NOTE: We only really care about the Y-axis rotation.
            var restrictedRot = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);
            planeOrigin.rotation = restrictedRot;
        }

        private void OnDrawGizmos()
        {
            if (_cameraOneTransform != null && _cameraOneTransform.gameObject.activeSelf)
            {
                DrawGizmoLineFor(_cameraOneTransform, Color.green, _cameraOneTransform.forward, 0.1f);
                DrawGizmoLineFor(_cameraOneTransform, Color.yellow, _camOneDetectedBallDir, 0.1f);
                DrawGizmoLineFor(_cameraOneTransform, Color.blue, _cameraOneTransform.up, 0.05f);
            }

            if (_cameraTwoTransform != null&& _cameraTwoTransform.gameObject.activeSelf)
            {
                DrawGizmoLineFor(_cameraTwoTransform, Color.green, _cameraTwoTransform.forward, 0.1f);
                DrawGizmoLineFor(_cameraTwoTransform, Color.yellow, _camTwoDetectedBallDir, 0.1f);
                DrawGizmoLineFor(_cameraTwoTransform, Color.blue, _cameraTwoTransform.up, 0.05f);
            }
        }

        private void DrawGizmoLineFor(Transform camTransform, Color color, Vector3 direction, float length)
        {
            if (camTransform == null)
            {
                return;
            }

            Gizmos.color = color;
            var position = camTransform.position;
            Gizmos.DrawLine(position, position + direction * length);
        }
    }
}