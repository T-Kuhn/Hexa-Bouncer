using UnityEngine;

namespace MachineSimulator.Controlling
{
    public sealed class TiltControlSwitcher : MonoBehaviour
    {
        private TiltControlType _currentTiltControl;

        public TiltControlType CurrentTiltControl => _currentTiltControl;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                SwitchTo(TiltControlType.PD);
            }
            else if (Input.GetKeyDown(KeyCode.A))
            {
                SwitchTo(TiltControlType.Analytical);
            }
        }

        private void SwitchTo(TiltControlType tiltControl)
        {
            if (_currentTiltControl == tiltControl)
            {
                return;
            }

            _currentTiltControl = tiltControl;
            Debug.Log($"Switched tilt control to {_currentTiltControl}");
        }
    }

    public enum TiltControlType
    {
        PD,
        Analytical,
    }
}
