using MachineSimulator.Machine;

namespace MachineSimulator
{
    public static class Constants
    {
        public const int BaudRate = 921600;

        public static readonly LLMachineState OriginMachineState = new LLMachineState();
        public static readonly LLMachineState OffsetFromTableState = new LLMachineState(-0.05f, 0.05f, -0.05f, 0.05f, -0.05f, 0.05f);

        public static readonly int CameraResolutionWidth = 1280;
        public static readonly int CameraResolutionHeight = 720;

        // NOTE: in degrees
        // NOTE: Below already take into account that the camera is mounted 90deg offset.
        public static readonly float CameraHorizontalFov = 53f;
        public static readonly float CameraVerticalFov = 92f;

        // NOTE: Orange ball colour gate, see BallDetection.IsBallPixel. Camera BGR bytes measured on the ball:
        //       bright spot R255 G112 B72, dark spot R109 G22 B16, white-ish highlights never exceed B 130.
        public static byte BallMinRed = 90;              // darker than the darkest measured orange (R109) is background
        public static byte BallMaxBlue = 130;            // highlight measurement
        public static int BallMaxBluePercentOfRed = 60;  // orange 15-28 %, highlights ~51 %, white/grey 85-100 %
        public static int BallMaxGreenPercentOfRed = 85; // orange 20-44 %, highlights up to ~80 %, white/grey/yellow ~100 %
        public static int PixelSpacing = 25;
    }
}