// One mechanical control handle, as pure logic. Its Value (-1..1) is the handle's deflection; the board tilt follows it
// through a TiltAxis. While held, the value follows the hand's displacement along the handle's travel axis, measured
// from where the hand grabbed it (so grabbing never makes the handle jump). Beyond the end stops the hand slips: the
// handle stays at the stop and moves again as soon as the hand comes back past it.
using UnityEngine;

namespace Labyrinth.Core
{
    public sealed class Lever
    {
        // Hand travel (metres) from neutral to one end stop.
        public float Travel = 0.08f;
        // false: a released handle stays where it was left (like the knobs of a wooden labyrinth).
        // true: it eases back to neutral at ReturnRate (full deflections per second).
        public bool ReturnToNeutralOnRelease;
        public float ReturnRate = 1.5f;

        public float Value { get; private set; }
        public bool Held { get; private set; }

        float grabValue;
        float grabCoord;

        // handCoord: the hand's position along the travel axis (metres, any fixed origin).
        public void Grab(float handCoord)
        {
            Held = true;
            grabValue = Value;
            grabCoord = handCoord;
        }

        public void Drag(float handCoord)
        {
            if (!Held) return;
            var v = grabValue + (handCoord - grabCoord) / Mathf.Max(0.001f, Travel);
            Value = Mathf.Clamp(v, -1f, 1f);
            // Re-anchor at the stop so the hand "slips": coming back moves the handle immediately.
            if (v > 1f || v < -1f)
            {
                grabValue = Value;
                grabCoord = handCoord;
            }
        }

        public void Release() => Held = false;

        // Direct setting (desktop keys, tests). Ignored while a hand holds the handle.
        public void Set(float value)
        {
            if (!Held) Value = Mathf.Clamp(value, -1f, 1f);
        }

        public void Tick(float dt)
        {
            if (!Held && ReturnToNeutralOnRelease) Value = Mathf.MoveTowards(Value, 0f, ReturnRate * dt);
        }

        public void ResetToNeutral()
        {
            Held = false;
            Value = 0f;
        }
    }
}
