// One hand's grip on its own handle, as pure logic. A grab starts only on a fresh grip press with the hand within
// GrabRadius of the knob (holding the grip and then reaching for the knob does not grab); while the grip is held the
// handle follows the hand; letting go releases it. Losing tracking or switching the input source (controller <-> bare
// hand) also releases it, and the grip then has to open before a new press counts: a hand that comes back already
// pinched, or a controller picked up with the grip squeezed, never grabs by itself. The handle stays where it was left.
// A grip only moves a lever it grabbed itself (Holding): two hands may share a lever (the table-height control) without
// one dragging what the other holds, and a lever released from outside (an adjustment, lost focus) also needs the
// grip opened before it can be grabbed again.
namespace Labyrinth.Core
{
    public enum InputSource { None, Controller, Hand }

    public sealed class HandGrip
    {
        public float GrabRadius = 0.10f;
        public float PressThreshold = 0.55f;
        public float ReleaseThreshold = 0.35f;

        public bool Hover { get; private set; }
        public bool Holding { get; private set; }
        public InputSource Source { get; private set; }
        bool gripDown;

        // Controller-only callers: tracked == the controller is tracked.
        public bool Update(Lever lever, bool tracked, float grip, float distance, float coord) =>
            Update(lever, tracked ? InputSource.Controller : InputSource.None, grip, distance, coord);

        // source: what drives this hand now (None = nothing tracked). grip: the analogue grip or pinch strength 0..1.
        // distance: hand to knob (m). coord: the hand along the handle's travel (m). Returns true on the frame a grab starts.
        public bool Update(Lever lever, InputSource source, float grip, float distance, float coord)
        {
            if (source != Source)
            {
                if (Source != InputSource.None)
                {
                    if (Holding && lever.Held) lever.Release();
                    Holding = false;
                    gripDown = true; // must open before the next press counts
                }
                Source = source;
            }
            if (source == InputSource.None)
            {
                Hover = false;
                return false;
            }
            if (Holding && !lever.Held) Holding = false; // released from outside: gripDown stays, so open first
            var wasDown = gripDown;
            gripDown = gripDown ? grip > ReleaseThreshold : grip >= PressThreshold;
            var near = distance < GrabRadius;
            Hover = !lever.Held && near;
            if (gripDown && !wasDown && near && !lever.Held)
            {
                lever.Grab(coord);
                Holding = true;
                return true;
            }
            if (!Holding) return false;
            if (gripDown) lever.Drag(coord);
            else
            {
                lever.Release();
                Holding = false;
            }
            return false;
        }
    }

    // Which source drives one hand: a tracked controller always wins, otherwise the tracked bare hand (if enabled).
    public static class SourceChoice
    {
        public static InputSource Choose(bool controllerTracked, bool handTracked, bool handsEnabled = true) =>
            controllerTracked ? InputSource.Controller
            : handsEnabled && handTracked ? InputSource.Hand
            : InputSource.None;
    }

    // Pinch strength 0..1 from the thumb-tip to index-tip distance (joint centres, metres), set by the two distances the
    // player meets: the pinch closes below PressDistance and opens above ReleaseDistance. Strength is linear in the
    // distance and placed so HandGrip's PressThreshold/ReleaseThreshold fall exactly on those distances; HandGrip keeps
    // the hysteresis. Defaults from the Spike 1 headset run: grabs happened at 1.0-3.1 cm, holds peaked at 3.7 cm, so
    // 3.1 cm to close and 4.5 cm to open.
    public sealed class HandPinch
    {
        public float PressDistance = 0.031f;
        public float ReleaseDistance = 0.045f;
        readonly float pressThreshold, releaseThreshold;

        public HandPinch(float pressThreshold = 0.55f, float releaseThreshold = 0.35f)
        {
            this.pressThreshold = pressThreshold;
            this.releaseThreshold = releaseThreshold;
        }

        public float Strength(float tipDistance)
        {
            var span = ReleaseDistance - PressDistance;
            if (span <= 1e-5f) return tipDistance < PressDistance ? 1f : 0f;
            var perMetre = (pressThreshold - releaseThreshold) / span;
            var s = pressThreshold - (tipDistance - PressDistance) * perMetre;
            return s < 0f ? 0f : s > 1f ? 1f : s;
        }
    }
}
