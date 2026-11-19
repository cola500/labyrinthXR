// One board axis: turns a handle value (-1..1) into a board angle (degrees), smoothly. The target is
// sign(v) * |v|^ResponseExponent * MaxDegrees (exponent 1 = linear; >1 gives finer control near neutral). The angle
// approaches the target exponentially (time constant SmoothingSeconds) and never changes faster than MaxDegreesPerSecond,
// so a sudden hand movement cannot flick the ball off.
using UnityEngine;

namespace Labyrinth.Core
{
    public sealed class TiltAxis
    {
        public float MaxDegrees = 6f;
        public float MaxDegreesPerSecond = 40f;
        public float SmoothingSeconds = 0.06f;
        public float ResponseExponent = 1f;

        public float Degrees { get; private set; }

        public float Target(float leverValue)
        {
            var v = Mathf.Clamp(leverValue, -1f, 1f);
            return Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), Mathf.Max(0.1f, ResponseExponent)) * MaxDegrees;
        }

        public void Step(float leverValue, float dt)
        {
            if (dt <= 0f) return;
            var target = Target(leverValue);
            var eased = SmoothingSeconds > 0f
                ? Degrees + (target - Degrees) * (1f - Mathf.Exp(-dt / SmoothingSeconds))
                : target;
            var maxStep = MaxDegreesPerSecond * dt;
            Degrees = Mathf.Clamp(Degrees + Mathf.Clamp(eased - Degrees, -maxStep, maxStep), -MaxDegrees, MaxDegrees);
        }

        public void Snap(float degrees) => Degrees = Mathf.Clamp(degrees, -MaxDegrees, MaxDegrees);
    }
}
