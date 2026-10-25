using UnityEngine;

namespace MachineSimulator.Controlling
{
    // NOTE: Port of the AnalyticalTiltController from HighPrecisionStepperJuggler. Instead of reacting to the ball
    //       position like the PD controller, it predicts the ball state at the upcoming impact and calculates the plate
    //       normal that makes the ball land on the target at the following impact:
    //       1. Impact point and incoming velocity follow from ballistic flight (no air drag, like TimeUntilNextImpact).
    //       2. Steady-state bouncing: the ball leaves the plate as fast as it arrived (the plate's up motion replaces
    //          the energy lost in the collision). So the next flight takes T = 2 * v / g, and the outgoing horizontal
    //          velocity has to be (target - impactPoint) / T.
    //       3. A frictionless contact can only push along the plate normal, so the normal is parallel to the
    //          velocity change (outgoing - incoming). This holds for a moving plate and any restitution. The juggler
    //          used the normalized bisector (-incoming.normalized + outgoing.normalized), which points the same way
    //          as long as both velocities have the same magnitude.
    // NOTE: Approximations: ball spin/friction is ignored (a gripping ball gets deflected less than calculated),
    //       ScrewBouncing's y rotation turns the push direction by ~4deg, and modes that change the bounce height
    //       (Alternating, RanTinyBounce) break the steady-state assumption. The tilt is recalculated on every
    //       bounce, so these errors get corrected on the following bounces.
    public sealed class AnalyticalTiltController
    {
        private const float Gravity = 9.81f;

        // NOTE: Max commanded correction around each axis in degrees (same as HighPrecisionStepperJuggler's
        //       Min/MaxTiltAngle). Because of _tiltFractionAtImpact, the plate is tilted about half of this at impact.
        private const float MaxTilt = 5f;

        // NOTE: Below this downward speed at impact (m/s) the prediction isn't a real bounce, e.g. TimeUntilNextImpact
        //       found the upward crossing of a ball that is still rising below the impact height.
        private const float MinImpactSpeed = 0.5f;

        private readonly float _tiltFractionAtImpact;

        // NOTE: GoUpAndDownAsync moves from the flat rest pose to the tilted up pose, and SequenceCreator eases the
        //       rotation with the same cosine curve as the height. The bounce is timed so that the ball hits in the
        //       middle of the up move (Controller.GetTimeThreshold), where the eased progress is (1 - cos(PI/2)) / 2 = 0.5.
        //       So only half of the commanded tilt is on the plate at impact, and we command impactTilt / 0.5.
        //       Values towards 1 make the controller softer.
        public AnalyticalTiltController(float tiltFractionAtImpact = 0.5f)
        {
            _tiltFractionAtImpact = tiltFractionAtImpact;
        }

        // input: world space ball position (m) and velocity (m/s), time (s) until the ball reaches the plate's impact
        //        height, target on the xz plane (m, targetXZ.y is the world z coordinate)
        // output: commanded correction tilt around the Z and X axis in degrees, same meaning and sign as the PD's
        //         (zCorrection, xCorrection). null if the ball isn't falling onto the plate.
        public (float ZTilt, float XTilt)? CalculateTilt(Vector3 ballPosition, Vector3 ballVelocity, float timeUntilImpact, Vector2 targetXZ)
        {
            // Step1: Predict where and how fast the ball hits the plate
            var impactPosition = ballPosition + ballVelocity * timeUntilImpact;
            var incomingVelocity = ballVelocity + Vector3.down * (Gravity * timeUntilImpact);

            var impactSpeed = -incomingVelocity.y;
            if (impactSpeed < MinImpactSpeed)
            {
                return null;
            }

            // Step2: Outgoing velocity that lands the ball on the target at the next impact
            var flightTime = 2f * impactSpeed / Gravity;
            var outgoingVelocity = new Vector3(
                (targetXZ.x - impactPosition.x) / flightTime,
                impactSpeed,
                (targetXZ.y - impactPosition.z) / flightTime);

            // Step3: The plate normal is parallel to the velocity change
            var normal = (outgoingVelocity - incomingVelocity).normalized;

            // Step4: Convert the normal to tilt angles
            // NOTE: Quaternion.Euler(xTilt, 0f, zTilt) * Vector3.up = (-sin(zTilt), cos(zTilt) * cos(xTilt), cos(zTilt) * sin(xTilt))
            var zTilt = -Mathf.Asin(normal.x) * Mathf.Rad2Deg;
            var xTilt = Mathf.Atan2(normal.z, normal.y) * Mathf.Rad2Deg;

            // Step5: Command more tilt since the plate only has part of it at impact
            return ClampToMaxTilt(zTilt / _tiltFractionAtImpact, xTilt / _tiltFractionAtImpact);
        }

        // NOTE: Scales both axes by the same factor so that the ball still gets pushed towards the target.
        private static (float ZTilt, float XTilt) ClampToMaxTilt(float zTilt, float xTilt)
        {
            var largest = Mathf.Max(Mathf.Abs(zTilt), Mathf.Abs(xTilt));
            if (largest <= MaxTilt)
            {
                return (zTilt, xTilt);
            }

            var scale = MaxTilt / largest;
            return (zTilt * scale, xTilt * scale);
        }
    }
}
