using UnityEngine;

namespace MachineSimulator.MachineModel
{
    public enum StrategyName
    {
        DoNothing,
        UpDown,
        BackForth,
        LeftRight,
        MoveInCircle,
        MoveInCircleCombinedWithUpDown,
        TiltArountX,
        CircleTilt
    }

    public sealed class UpDownStrategy : IHexaplateMovementStrategy
    {
        public (Vector3 Position, Quaternion Rotation) Move(float time)
        {
            var height = Mathf.Sin(time) * 0.05f + 0.05f;
            return (new Vector3(0, height, 0), Quaternion.identity);
        }
    }

    public sealed class BackForthStrategy : IHexaplateMovementStrategy
    {
        public (Vector3 Position, Quaternion Rotation) Move(float time)
        {
            var offset = Mathf.Sin(time) * 0.05f;
            return (new Vector3(0, 0, offset), Quaternion.identity);
        }
    }

    public sealed class LeftRightStrategy : IHexaplateMovementStrategy
    {
        public (Vector3 Position, Quaternion Rotation) Move(float time)
        {
            var offset = Mathf.Sin(time) * 0.1f;
            return (new Vector3(offset, 0, 0), Quaternion.identity);
        }
    }

    public sealed class MoveInCircleStrategy : IHexaplateMovementStrategy
    {
        public (Vector3 Position, Quaternion Rotation) Move(float time)
        {
            var t = time;
            var x = Mathf.Sin(t) * 0.025f;
            var z = Mathf.Cos(t) * 0.025f;
            var yOffset = 0.1f;
            return (new Vector3(x, yOffset, z), Quaternion.identity);
        }
    }

    public sealed class MoveInCircleWhileGoingUpAndDownStrategy : IHexaplateMovementStrategy
    {
        public (Vector3 Position, Quaternion Rotation) Move(float time)
        {
            var t = time;
            var height = Mathf.Sin(t) * 0.05f;
            var x = Mathf.Sin(t) * 0.05f;
            var z = Mathf.Cos(t) * 0.05f;
            var yOffset = 0.1f;
            return (new Vector3(x, height + yOffset, z), Quaternion.identity);
        }
    }

    public sealed class TiltAroundXStrategy : IHexaplateMovementStrategy
    {
        public (Vector3 Position, Quaternion Rotation) Move(float time)
        {
            var tilt = Mathf.Sin(time) * 15f;
            return (Vector3.zero, Quaternion.Euler(tilt, 0, 0));
        }
    }

    public sealed class CircleTiltStrategy : IHexaplateMovementStrategy
    {
        public (Vector3 Position, Quaternion Rotation) Move(float time)
        {
            var t = time;
            var x = Mathf.Sin(t) * 15f;
            var z = Mathf.Cos(t) * 15f;
            var yOffset = 0.05f;
            return (Vector3.zero + Vector3.up * yOffset, Quaternion.Euler(x, 0, z));
        }
    }
}