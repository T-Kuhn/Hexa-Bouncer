using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MachineSimulator.Controlling
{
    public sealed class BallVelocityRegression
    {
        private readonly int _sampleCount;
        private readonly Queue<(float time, Vector3 position)> _samples = new Queue<(float, Vector3)>();

        public BallVelocityRegression(int sampleCount = 10)
        {
            _sampleCount = sampleCount;
        }

        public void AddSample(float time, Vector3 position)
        {
            _samples.Enqueue((time, position));
            if (_samples.Count > _sampleCount)
            {
                _samples.Dequeue();
            }
        }

        // NOTE: Samples are never removed when the ball gets lost. So after a detection gap, the window mixes two
        //       different flights and the velocity is meaningless until the window has been refilled.
        public bool IsWindowFresh(float maxTimeSpan)
        {
            return _samples.Count == _sampleCount && _samples.Last().time - _samples.Peek().time <= maxTimeSpan;
        }

        // NOTE: only y-velocity is adjusted to be closer to real time. The others (velocity along x and z) are not adjusted.
        public Vector3 CalculateRealTimeVelocity()
        {
            if (_samples.Count == 0) return Vector3.zero;

            var velocity = CalculateVelocity();

            // NOTE: Our velocity is sampleCount/2 * timestep late (10/2 * 8ms = 40ms). So we need to add ball velocity that
            //       will happen in the next 40ms and add that to our "current" ball velocity to get real time ball velocity.
            var oldestSampleTime = _samples.Peek().time;
            var newestSampleTime = _samples.Last().time;
            var totalSampleTime = newestSampleTime - oldestSampleTime;

            // NOTE: This midTime should be around 40ms.
            var midTime = totalSampleTime / 2f;
            // NOTE: velocity at time t is v_t = a * t (we don't care about initial velocity here in this specific case)
            //       a = - 9.81m/s^2
            var velocityChangeDueToGravity = (-9.81f) * midTime;

            velocity += velocityChangeDueToGravity * Vector3.up;

            return velocity;
        }

        public Vector3 CalculateVelocity()
        {
            if (_samples.Count < 2)
            {
                return Vector3.zero;
            }

            // Simple linear regression for each axis (x, y, z)
            // v = (n*sum(t*p) - sum(t)*sum(p)) / (n*sum(t^2) - (sum(t))^2)

            var n = _samples.Count;
            var firstTime = _samples.Peek().time;
            var sumT = 0f;
            var sumT2 = 0f;
            var sumP = Vector3.zero;
            var sumTP = Vector3.zero;

            foreach (var sample in _samples)
            {
                var t = sample.time - firstTime;
                var p = sample.position;

                sumT += t;
                sumT2 += t * t;
                sumP += p;
                sumTP += t * p;
            }

            var denominator = n * sumT2 - sumT * sumT;
            if (Mathf.Abs(denominator) < 1e-6f)
            {
                return Vector3.zero;
            }

            var velocity = (n * sumTP - sumT * sumP) / denominator;
            return velocity;
        }
    }
}