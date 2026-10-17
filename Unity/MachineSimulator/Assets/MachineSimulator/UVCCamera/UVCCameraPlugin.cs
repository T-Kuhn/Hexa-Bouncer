using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using MachineSimulator.Controlling;
using Unity.MachineSimulator.ImageProcessing;
using UnityEngine;

namespace MachineSimulator.UVCCamera
{
    public class UVCCameraPlugin : MonoBehaviour, IBallPositionProvider
    {
        // Open camera by device index, configure resolution/FPS, and disable all
        // automatic camera controls internally. Returns a handle or IntPtr.Zero on failure.
        [DllImport("UVCCameraPlugin")]
        private static extern IntPtr openCamera(int deviceIndex, int width, int height, int fps);

        [DllImport("UVCCameraPlugin")]
        private static extern void releaseCamera(IntPtr camera);

        [DllImport("UVCCameraPlugin")]
        private static extern int getCameraTexture(IntPtr camera, IntPtr data, int width, int height);

        [DllImport("UVCCameraPlugin")]
        private static extern int getCameraDimensions(IntPtr camera, out int width, out int height);

        [DllImport("UVCCameraPlugin")]
        private static extern int setCameraExposure(IntPtr camera, int value);

        [DllImport("UVCCameraPlugin")]
        private static extern int setCameraGain(IntPtr camera, int value);

        [DllImport("UVCCameraPlugin")]
        private static extern int setCameraContrast(IntPtr camera, int value);

        [SerializeField] private int _id;

        private IntPtr _camera;
        public Texture2D Texture;

        private byte[] _pixelsFront;
        private GCHandle _pixelsFrontHandle;
        private IntPtr _pixelsFrontPtr;

        private byte[] _pixelsBack;
        private GCHandle _pixelsBackHandle;
        private IntPtr _pixelsBackPtr;

        private Thread _cameraThread;
        private volatile bool _isRunning;
        private readonly object _lock = new object();
        private bool _hasNewFrame;

        [SerializeField] private CameraProperties _cameraProperties = new CameraProperties();

        public Vector2 NewestBallPosition { get; private set; }
        public float TimeStamp { get; private set; }
        public bool IsBallDetected { get; private set; }

        // NOTE: Debug readout shown by UVCCameraPluginEditor: every object of the newest frame that passed the
        //       radius filter, and the index of the one taken as the ball (-1 when none).
        public IReadOnlyList<BallRadiusAndPosition> NewestCandidates { get; private set; }
        public int NewestBallCandidateIndex { get; private set; } = -1;

        public void ResetCameraProperties()
        {
            _cameraProperties = new CameraProperties();
        }

        public int ImageRetrievalTookTooLongCount { get; private set; }
        public long LastImageRetrievalTime { get; private set; }

        public bool CameraIsInitialized { get; private set; }

        // Apply exposure, gain, and contrast to the running camera.
        public void SetCameraProperties()
        {
            if (_camera == IntPtr.Zero) return;

            int result;
            result = setCameraExposure(_camera, (int)_cameraProperties.Exposure);
            if (result == 0) Debug.LogWarning($"Failed to set camera exposure to {_cameraProperties.Exposure}");

            result = setCameraGain(_camera, (int)_cameraProperties.Gain);
            if (result == 0) Debug.LogWarning($"Failed to set camera gain to {_cameraProperties.Gain}");

            result = setCameraContrast(_camera, (int)_cameraProperties.Contrast);
            if (result == 0) Debug.LogWarning($"Failed to set camera contrast to {_cameraProperties.Contrast}");
        }

        private void InitializeCamera()
        {
            ResetCameraProperties();

            // Open camera and configure resolution/FPS in one call.
            // All auto-controls are disabled internally by the plugin.
            _camera = openCamera(
                _id,
                (int)_cameraProperties.Width,
                (int)_cameraProperties.Height,
                (int)_cameraProperties.FPS
            );

            if (_camera == IntPtr.Zero)
            {
                Debug.LogError($"Failed to open camera with device index {_id}");
                return;
            }

            // Apply manual exposure / gain / contrast.
            SetCameraProperties();

            // Read back actual negotiated dimensions (may differ from requested).
            var actualWidth = (int)_cameraProperties.Width;
            var actualHeight = (int)_cameraProperties.Height;
            getCameraDimensions(_camera, out actualWidth, out actualHeight);

            Texture = new Texture2D(actualWidth, actualHeight, TextureFormat.RGB24, false);

            // BGR -- 3 bytes per pixel
            var byteCount = actualWidth * actualHeight * 3;
            _pixelsFront = new byte[byteCount];
            _pixelsFrontHandle = GCHandle.Alloc(_pixelsFront, GCHandleType.Pinned);
            _pixelsFrontPtr = _pixelsFrontHandle.AddrOfPinnedObject();

            _pixelsBack = new byte[byteCount];
            _pixelsBackHandle = GCHandle.Alloc(_pixelsBack, GCHandleType.Pinned);
            _pixelsBackPtr = _pixelsBackHandle.AddrOfPinnedObject();

            _isRunning = true;
            _cameraThread = new Thread(CameraLoop);
            _cameraThread.Priority = System.Threading.ThreadPriority.Highest;
            _cameraThread.IsBackground = true;
            _cameraThread.Start();

            CameraIsInitialized = true;
        }

        private void CameraLoop()
        {
            var sw = new System.Diagnostics.Stopwatch();
            while (_isRunning)
            {
                sw.Restart();
                var result = getCameraTexture(
                    _camera,
                    _pixelsBackPtr,
                    (int)_cameraProperties.Width,
                    (int)_cameraProperties.Height
                );
                sw.Stop();

                if (sw.ElapsedMilliseconds > 50)
                {
                    Debug.Log($"Image retrieval took {sw.ElapsedMilliseconds}ms on camera with ID: " + _id);
                    ImageRetrievalTookTooLongCount++;
                    LastImageRetrievalTime = sw.ElapsedMilliseconds;
                }

                if (result < 0)
                {
                    Debug.Log($"getCameraTexture returned error code: {result}");
                    continue;
                }

                lock (_lock)
                {
                    // Swap pointers/buffers
                    (_pixelsBackPtr, _pixelsFrontPtr) = (_pixelsFrontPtr, _pixelsBackPtr);
                    (_pixelsBack, _pixelsFront) = (_pixelsFront, _pixelsBack);

                    _hasNewFrame = true;
                }
            }
        }

        private readonly BallDetection _ballDetection = new BallDetection();

        private void Update()
        {
            if (!CameraIsInitialized)
            {
                InitializeCamera();
            }

            if (_hasNewFrame)
            {
                lock (_lock)
                {
                    var res = _ballDetection.BallDataFromPixelBoarders(_pixelsFront, 500);

                    // NOTE: Ball radius is measured in pixels.
                    var filteredResults = res.Where(data => data.Radius is > 20 and < 110).ToList();
                    NewestCandidates = filteredResults;

                    if (filteredResults.Count > 0)
                    {
                        // NOTE: More than one object can pass the radius filter (the ball plus e.g. a brownish
                        //       surface that slips through the colour gate), and the biggest is not necessarily
                        //       the ball. The ball is the round one, so take the candidate whose outline deviates
                        //       least from a circle (CircleDeviation 0, see BallRadiusAndPosition): its radial
                        //       term rejects wrong silhouettes (rectangles, streaks), its outline-length term
                        //       rejects ragged rims, so a candidate must be bad at neither. A white reflection
                        //       cutting into the ball's rim only moves the score a little.
                        var ballIndex = 0;
                        for (var i = 1; i < filteredResults.Count; i++)
                        {
                            if (filteredResults[i].CircleDeviation < filteredResults[ballIndex].CircleDeviation)
                            {
                                ballIndex = i;
                            }
                        }

                        var ball = filteredResults[ballIndex];
                        NewestBallCandidateIndex = ballIndex;
                        NewestBallPosition = new Vector2(ball.PositionX, ball.PositionY);
                        TimeStamp = Time.realtimeSinceStartup;
                        IsBallDetected = true;
                    }
                    else
                    {
                        NewestBallCandidateIndex = -1;
                        IsBallDetected = false;
                    }

                    Texture.SetPixelData(_pixelsFront, 0);
                    Texture.Apply();

                    _hasNewFrame = false;
                }
            }
        }

        private void OnDestroy()
        {
            _isRunning = false;

            if (_cameraThread != null && _cameraThread.IsAlive)
            {
                if (!_cameraThread.Join(1000))
                {
                    Debug.LogWarning("Camera thread did not terminate in time. Memory might leak or crash on exit.");
                }
            }

            if (_pixelsFrontHandle.IsAllocated) _pixelsFrontHandle.Free();
            if (_pixelsBackHandle.IsAllocated) _pixelsBackHandle.Free();

            if (_camera != IntPtr.Zero)
            {
                releaseCamera(_camera);
                _camera = IntPtr.Zero;
            }
        }

        private void OnApplicationQuit()
        {
            OnDestroy();
        }
    }

    [Serializable]
    public class CameraProperties
    {
        public double Width = Constants.CameraResolutionWidth;
        public double Height = Constants.CameraResolutionHeight;
        public double FPS = 120;
        public double Exposure = -8;
        public double Gain = 2;
        public double Contrast = 15;
    }
}