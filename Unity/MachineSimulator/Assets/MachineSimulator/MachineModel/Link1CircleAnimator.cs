using UnityEngine;

namespace MachineSimulator.MachineModel
{
    // NOTE: Demo helper for the IK explanation video. While active, Joint1 (the motor
    //       joint) spins around its X axis at constant speed, sweeping Link1 along the
    //       drawn link1 circle to show that the motor can only ever move Link1 on that
    //       circle. The shoulder gimbal mesh (UniversalJoin:1, normally a child of the
    //       IK-driven Joint2) rides along on Joint1Tip; nothing else moves - Link2 stays
    //       frozen unless its own detach animation is running, which combines freely with
    //       this one. Exit() restores everything bit-exactly.
    public sealed class Link1CircleAnimator
    {
        private readonly Transform _joint1;
        private readonly Transform _joint1Tip;
        private readonly Transform _joint2;

        private Transform _gimbalMesh;

        private Quaternion _savedJoint1LocalRotation;
        private Vector3 _savedGimbalMeshLocalPosition;
        private Quaternion _savedGimbalMeshLocalRotation;
        private int _savedGimbalMeshSiblingIndex;
        private bool _gimbalMeshMoved;

        // NOTE: Joint1's angle at enter time (the joint holds a pure X euler), so the
        //       sweep starts jump-free from the current pose.
        private float _theta0;
        private float _animTime;

        public bool IsActive { get; private set; }

        public Link1CircleAnimator(Transform joint1, Transform joint1Tip, Transform joint2)
        {
            _joint1 = joint1;
            _joint1Tip = joint1Tip;
            _joint2 = joint2;
        }

        public bool TryEnter(out string failReason)
        {
            failReason = null;

            if (IsActive)
            {
                failReason = "an animation is already active";
                return false;
            }

            var theta0 = Vector3.SignedAngle(Vector3.up, _joint1.localRotation * Vector3.up, Vector3.right);
            if (float.IsNaN(theta0))
            {
                failReason = "the current pose contains non-finite values";
                return false;
            }

            // NOTE: The gimbal mesh is optional - without it the sweep still works, only
            //       the shoulder hardware stays frozen with Joint2 instead of riding along.
            if (_gimbalMesh == null) _gimbalMesh = _joint2.Find("UniversalJoin:1");

            _savedJoint1LocalRotation = _joint1.localRotation;
            _theta0 = theta0;
            _animTime = 0f;

            if (_gimbalMesh != null)
            {
                _savedGimbalMeshLocalPosition = _gimbalMesh.localPosition;
                _savedGimbalMeshLocalRotation = _gimbalMesh.localRotation;
                _savedGimbalMeshSiblingIndex = _gimbalMesh.GetSiblingIndex();
                _gimbalMesh.SetParent(_joint1Tip, worldPositionStays: true);
                _gimbalMeshMoved = true;
            }

            IsActive = true;
            return true;
        }

        // NOTE: Restores the original hierarchy. Assigning the saved local values (instead
        //       of relying on worldPositionStays math) makes the restore bit-exact.
        public void Exit()
        {
            if (!IsActive) return;
            IsActive = false;

            if (_gimbalMeshMoved && _gimbalMesh != null && _joint2 != null)
            {
                _gimbalMesh.SetParent(_joint2, worldPositionStays: false);
                _gimbalMesh.localPosition = _savedGimbalMeshLocalPosition;
                _gimbalMesh.localRotation = _savedGimbalMeshLocalRotation;
                _gimbalMesh.SetSiblingIndex(_savedGimbalMeshSiblingIndex);
            }

            _gimbalMeshMoved = false;

            if (_joint1 != null) _joint1.localRotation = _savedJoint1LocalRotation;
        }

        public void Tick(float deltaTime, float speedDegPerSec)
        {
            if (!IsActive) return;

            _animTime += deltaTime;
            _joint1.localRotation = Quaternion.Euler(_theta0 + _animTime * speedDegPerSec, 0f, 0f);
        }
    }
}
