using UnityEngine;

namespace MachineSimulator.MachineModel
{
    // NOTE: Demo helper for the IK explanation video. While active, the wrist universal
    //       joint stops driving Link3 (it freezes at its current pose) and instead swings
    //       Link2, so Link2's free end (the Joint2/Joint3 end) travels along the IK sphere
    //       surface or along the plane-sphere intersection circle.
    //
    //       Mechanism: holding Link3 and turning Link2 means driving the gimbal from the
    //       Link3 side, so its hinge order reverses: R(link2) = R(link3) * Rx(-beta) * Ry(-alpha).
    //       That is realized with a small transform chain created at the wrist:
    //           pivot (static, = frozen Link3-side frame) -> hingeX -> hingeY -> Link2
    //       (Simply making Link2 a child of Joint5 and writing joint4/joint5 eulers does
    //       NOT work: with Link2's axis nearly along Joint4's yaw axis - the actual rig
    //       pose - the Y-then-X composition can only reach a narrow band of directions and
    //       most of the intersection circle becomes unreachable.)
    //       The wrist gimbal mesh rides hingeX, which matches Joint4's frame exactly at
    //       enter time, so it articulates physically. Exit() restores everything bit-exactly.
    public sealed class Link2DetachAnimator
    {
        public enum Mode
        {
            Sphere,
            Circle,
        }

        private const float NoiseSeedA = 12.9898f;
        private const float NoiseSeedB = 78.233f;
        private const float ValidReconstructionError = 1e-4f;

        private readonly Transform _armRoot;
        private readonly Transform _joint3;
        private readonly Transform _joint4;
        private readonly Transform _joint5;

        private Transform _link2;
        private Transform _link3;
        private Transform _gimbalMesh;
        private Transform _pivot;
        private Transform _hingeX;
        private Transform _hingeY;

        private Vector3 _savedLink2LocalPosition;
        private Quaternion _savedLink2LocalRotation;
        private int _savedLink2SiblingIndex;
        private Vector3 _savedLink3LocalPosition;
        private Quaternion _savedLink3LocalRotation;
        private int _savedLink3SiblingIndex;
        private Vector3 _savedGimbalMeshLocalPosition;
        private Quaternion _savedGimbalMeshLocalRotation;
        private int _savedGimbalMeshSiblingIndex;
        private bool _gimbalMeshMoved;

        private float _animTime;
        private bool _warnedAboutUnsolvableDirection;

        // NOTE: Hinge angles at enter time. They equal the Joint4 (Y) / Joint5 (X) angles
        //       of the frozen IK pose, read from the pure single-axis joint rotations.
        private float _alpha0;
        private float _beta0;
        private float _alphaPrev;
        private float _betaPrev;

        // NOTE: Sphere mode: noise samples at enter time, so the wander starts jump-free
        private float _noiseStartA;
        private float _noiseStartB;

        // NOTE: Circle mode: intersection circle in arm-root local space (z = 0 plane)
        private Vector3 _sphereCenter;
        private Vector2 _circleCenter;
        private float _circleRadius;
        private float _theta0;

        public bool IsActive { get; private set; }
        public Mode CurrentMode { get; private set; }

        public Link2DetachAnimator(Transform armRoot, Transform joint3, Transform joint4, Transform joint5)
        {
            _armRoot = armRoot;
            _joint3 = joint3;
            _joint4 = joint4;
            _joint5 = joint5;
        }

        // NOTE: Captures the current pose, freezes Link3 in place and hangs Link2 onto the
        //       hinge chain at the wrist, so the universal joint now turns Link2.
        //       Returns false without changing anything if the current pose is unusable.
        public bool TryEnter(Mode mode, out string failReason)
        {
            failReason = null;

            if (IsActive)
            {
                failReason = "an animation is already active";
                return false;
            }

            if (_link2 == null) _link2 = _joint3.Find("Link2:1");
            if (_link3 == null) _link3 = _joint5.Find("Link3:1");
            if (_link2 == null || _link3 == null)
            {
                failReason = "could not find 'Link2:1' under Joint3 / 'Link3:1' under Joint5";
                return false;
            }

            // NOTE: The gimbal mesh is optional - without it the animation still works,
            //       only the wrist hardware stays frozen instead of articulating.
            if (_gimbalMesh == null) _gimbalMesh = _joint4.Find("UniversalJoin(Mirror):1");

            var alpha0 = Vector3.SignedAngle(Vector3.forward, _joint4.localRotation * Vector3.forward, Vector3.up);
            var beta0 = Vector3.SignedAngle(Vector3.up, _joint5.localRotation * Vector3.up, Vector3.right);
            var sphereCenter = _armRoot.InverseTransformPoint(_joint4.position);
            var sphereRadius = Mathf.Abs(_joint4.localPosition.z);
            var freeEnd = _armRoot.InverseTransformPoint(_joint3.position);

            // NOTE: The IK solver can produce NaN poses while reporting success (division
            //       by zero when sphereCenter.y == circleCenter.y), so check explicitly.
            if (!IsFinite(sphereCenter) || !IsFinite(freeEnd) || float.IsNaN(alpha0) || float.IsNaN(beta0))
            {
                failReason = "the current pose contains non-finite values";
                return false;
            }

            if (mode == Mode.Circle)
            {
                // NOTE: Same construction as in IkDebugVisualizer: the sphere cut by the
                //       link1 circle plane (arm-local z = 0) forms the intersection circle.
                var circleRadiusSquared = sphereRadius * sphereRadius - sphereCenter.z * sphereCenter.z;
                if (circleRadiusSquared < 1e-6f)
                {
                    failReason = "the link1 circle plane does not cut the link2 sphere at the current pose";
                    return false;
                }

                _circleCenter = new Vector2(sphereCenter.x, sphereCenter.y);
                _circleRadius = Mathf.Sqrt(circleRadiusSquared);

                // NOTE: The free end currently sits on the intersection circle (it is one of
                //       the two IK solutions), so starting theta there makes the start jump-free.
                _theta0 = Mathf.Atan2(freeEnd.y - _circleCenter.y, freeEnd.x - _circleCenter.x);
            }
            else
            {
                _noiseStartA = SignedNoise(0f, NoiseSeedA);
                _noiseStartB = SignedNoise(0f, NoiseSeedB);
            }

            _savedLink2LocalPosition = _link2.localPosition;
            _savedLink2LocalRotation = _link2.localRotation;
            _savedLink2SiblingIndex = _link2.GetSiblingIndex();
            _savedLink3LocalPosition = _link3.localPosition;
            _savedLink3LocalRotation = _link3.localRotation;
            _savedLink3SiblingIndex = _link3.GetSiblingIndex();

            _alpha0 = alpha0;
            _beta0 = beta0;
            _alphaPrev = alpha0;
            _betaPrev = beta0;
            _sphereCenter = sphereCenter;
            _animTime = 0f;
            _warnedAboutUnsolvableDirection = false;

            // NOTE: The pivot holds the frozen Link3-side frame of the gimbal. At the enter
            //       pose, hingeX's frame equals Joint4's frame and hingeY's frame equals
            //       Joint3's, so both Link2 and the gimbal mesh transfer with their world
            //       pose exactly preserved.
            EnsureHingeChain();
            _pivot.SetPositionAndRotation(_joint5.position, _joint5.rotation);
            _hingeX.localRotation = Quaternion.Euler(-beta0, 0f, 0f);
            _hingeY.localRotation = Quaternion.Euler(0f, -alpha0, 0f);

            _link3.SetParent(_armRoot, worldPositionStays: true);
            _link2.SetParent(_hingeY, worldPositionStays: true);

            if (_gimbalMesh != null)
            {
                _savedGimbalMeshLocalPosition = _gimbalMesh.localPosition;
                _savedGimbalMeshLocalRotation = _gimbalMesh.localRotation;
                _savedGimbalMeshSiblingIndex = _gimbalMesh.GetSiblingIndex();
                _gimbalMesh.SetParent(_hingeX, worldPositionStays: true);
                _gimbalMeshMoved = true;
            }

            CurrentMode = mode;
            IsActive = true;

            // NOTE: Self-check: solving for the current free-end direction must reproduce
            //       the captured hinge angles. If this assert fires, the sign-marked line
            //       in the solver below is wrong.
            if (mode == Mode.Circle)
            {
                var enterDir = _pivot.InverseTransformDirection((_joint3.position - _joint5.position).normalized);
                SolveHinges(enterDir, out var alphaCheck, out var betaCheck);
                Debug.Assert(
                    Mathf.Abs(Mathf.DeltaAngle(alphaCheck, alpha0)) < 0.1f
                    && Mathf.Abs(Mathf.DeltaAngle(betaCheck, beta0)) < 0.1f,
                    $"Link2DetachAnimator self-check failed: solver returned ({alphaCheck:F2}, {betaCheck:F2}), expected ({alpha0:F2}, {beta0:F2})");
            }

            return true;
        }

        // NOTE: Restores the original hierarchy. Assigning the saved local values (instead
        //       of relying on worldPositionStays math) makes the restore bit-exact
        //       regardless of float drift accumulated while animating.
        public void Exit()
        {
            if (!IsActive) return;
            IsActive = false;

            if (_link2 != null && _joint3 != null)
            {
                _link2.SetParent(_joint3, worldPositionStays: false);
                _link2.localPosition = _savedLink2LocalPosition;
                _link2.localRotation = _savedLink2LocalRotation;
                _link2.SetSiblingIndex(_savedLink2SiblingIndex);
            }

            if (_link3 != null && _joint5 != null)
            {
                _link3.SetParent(_joint5, worldPositionStays: false);
                _link3.localPosition = _savedLink3LocalPosition;
                _link3.localRotation = _savedLink3LocalRotation;
                _link3.SetSiblingIndex(_savedLink3SiblingIndex);
            }

            if (_gimbalMeshMoved && _gimbalMesh != null && _joint4 != null)
            {
                _gimbalMesh.SetParent(_joint4, worldPositionStays: false);
                _gimbalMesh.localPosition = _savedGimbalMeshLocalPosition;
                _gimbalMesh.localRotation = _savedGimbalMeshLocalRotation;
                _gimbalMesh.SetSiblingIndex(_savedGimbalMeshSiblingIndex);
            }

            _gimbalMeshMoved = false;
        }

        public void Tick(float deltaTime, float wanderSpeed, float wanderAmplitudeDeg, float circleSpeedDegPerSec)
        {
            if (!IsActive) return;

            _animTime += deltaTime;

            if (CurrentMode == Mode.Sphere)
            {
                TickSphere(wanderSpeed, wanderAmplitudeDeg);
            }
            else
            {
                TickCircle(circleSpeedDegPerSec);
            }
        }

        // NOTE: Link2's free end sits at a constant distance from the wrist (= the sphere
        //       center), so ANY pair of hinge angles keeps it on the sphere surface.
        //       Two decorrelated Perlin channels around the enter pose give a smooth random
        //       sweep; subtracting the enter-time samples makes the start jump-free.
        private void TickSphere(float wanderSpeed, float wanderAmplitudeDeg)
        {
            var t = _animTime * wanderSpeed;
            var alpha = _alpha0 + wanderAmplitudeDeg * (SignedNoise(t, NoiseSeedA) - _noiseStartA);
            var beta = _beta0 + wanderAmplitudeDeg * (SignedNoise(t, NoiseSeedB) - _noiseStartB);
            ApplyHingeRotations(alpha, beta);
        }

        private void TickCircle(float circleSpeedDegPerSec)
        {
            var theta = _theta0 + _animTime * circleSpeedDegPerSec * Mathf.Deg2Rad;
            var pointOnCircle = new Vector3(
                _circleCenter.x + _circleRadius * Mathf.Cos(theta),
                _circleCenter.y + _circleRadius * Mathf.Sin(theta),
                0f);

            var worldDir = _armRoot.TransformDirection((pointOnCircle - _sphereCenter).normalized);
            var dirInPivotFrame = _pivot.InverseTransformDirection(worldDir);

            var solvedExactly = SolveHinges(dirInPivotFrame, out var alpha, out var beta);
            if (!solvedExactly && !_warnedAboutUnsolvableDirection)
            {
                _warnedAboutUnsolvableDirection = true;
                Debug.LogWarning("Link2DetachAnimator: could not exactly reach a point on the intersection circle, using the closest reachable pose.");
            }

            ApplyHingeRotations(alpha, beta);
        }

        private void EnsureHingeChain()
        {
            if (_pivot != null) return;

            _pivot = new GameObject("Link2Anim_Pivot").transform;
            _pivot.SetParent(_armRoot, worldPositionStays: false);
            _hingeX = new GameObject("Link2Anim_HingeX").transform;
            _hingeX.SetParent(_pivot, worldPositionStays: false);
            _hingeY = new GameObject("Link2Anim_HingeY").transform;
            _hingeY.SetParent(_hingeX, worldPositionStays: false);
        }

        private void ApplyHingeRotations(float alphaDeg, float betaDeg)
        {
            _hingeX.localRotation = Quaternion.Euler(-betaDeg, 0f, 0f);
            _hingeY.localRotation = Quaternion.Euler(0f, -alphaDeg, 0f);
            _alphaPrev = alphaDeg;
            _betaPrev = betaDeg;
        }

        // NOTE: Solves R_x(-beta) * R_y(-alpha) * forward = dir for the hinge angles, dir
        //       being the wanted free-end direction in the pivot (frozen Link3-side) frame.
        //       The x-component is untouched by R_x, so sin(alpha) = -dir.x directly (two
        //       branches); beta then follows from the yz components. Every unit direction
        //       is reachable. Both candidates are validated by reconstructing the direction
        //       with Unity's own quaternion math, so a sign mistake can never produce a
        //       silently wrong orbit; the branch nearest the previous frame wins.
        private bool SolveHinges(Vector3 dir, out float alphaDeg, out float betaDeg)
        {
            alphaDeg = _alphaPrev;
            betaDeg = _betaPrev;

            var sinAlpha = Mathf.Clamp(-dir.x, -1f, 1f); // NOTE: flip this sign if the enter-time self-check assert fires

            // NOTE: Degenerate only when link2 points along the hinge X axis; keep the previous pose.
            if (1f - sinAlpha * sinAlpha < 1e-8f) return false;

            var alphaA = Mathf.Asin(sinAlpha) * Mathf.Rad2Deg;
            var alphaB = 180f - alphaA;
            var betaA = Mathf.Atan2(dir.y, dir.z) * Mathf.Rad2Deg;
            var betaB = Mathf.Atan2(-dir.y, -dir.z) * Mathf.Rad2Deg;

            var errorA = ReconstructionError(alphaA, betaA, dir);
            var errorB = ReconstructionError(alphaB, betaB, dir);

            var aIsValid = errorA < ValidReconstructionError;
            var bIsValid = errorB < ValidReconstructionError;

            bool pickA;
            if (aIsValid && bIsValid)
            {
                // NOTE: Both branches reach the direction exactly; stay on the branch
                //       closest to the previous frame so the motion never flips mid-orbit.
                //       The branch identity lives in alpha (the branches are alpha and
                //       180 - alpha), so alpha distance alone decides.
                pickA = Mathf.Abs(Mathf.DeltaAngle(_alphaPrev, alphaA)) <= Mathf.Abs(Mathf.DeltaAngle(_alphaPrev, alphaB));
            }
            else if (aIsValid || bIsValid)
            {
                pickA = aIsValid;
            }
            else
            {
                pickA = errorA <= errorB;
            }

            alphaDeg = pickA ? alphaA : alphaB;
            betaDeg = pickA ? betaA : betaB;
            return pickA ? aIsValid : bIsValid;
        }

        private static float ReconstructionError(float alphaDeg, float betaDeg, Vector3 dir)
        {
            var reconstructed = Quaternion.Euler(-betaDeg, 0f, 0f) * (Quaternion.Euler(0f, -alphaDeg, 0f) * Vector3.forward);
            return (reconstructed - dir).sqrMagnitude;
        }

        private static float SignedNoise(float t, float seed)
        {
            return (Mathf.PerlinNoise(t + seed, seed * 0.7317f) - 0.5f) * 2f;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
                   && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
                   && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }
    }
}
