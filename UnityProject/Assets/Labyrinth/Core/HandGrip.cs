// One hand's grip on its own handle, as pure logic. A grab starts only on a fresh grip press with the hand within
// GrabRadius of the knob (holding the grip and then reaching for the knob does not grab); while the grip is held the
// handle follows the hand; letting go releases it. Losing tracking or switching the input source (controller <-> bare
// hand) also releases it, and the grip then has to open before a new press counts: a hand that comes back already
// pinched, or a controller picked up with the grip squeezed, never grabs by itself. The handle stays where it was left.
namespace Labyrinth.Core
{
    public enum InputSource { None, Controller, Hand }

    public sealed class HandGrip
    {
        public float GrabRadius = 0.10f;
        public float PressThreshold = 0.55f;
        public float ReleaseThreshold = 0.35f;

        public bool Hover { get; private set; }
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
                    if (lever.Held) lever.Release();
                    gripDown = true; // must open before the next press counts
                }
                Source = source;
            }
            if (source == InputSource.None)
            {
                Hover = false;
                return false;
            }
            var wasDown = gripDown;
            gripDown = gripDown ? grip > ReleaseThreshold : grip >= PressThreshold;
            var near = distance < GrabRadius;
            Hover = !lever.Held && near;
            if (gripDown && !wasDown && near)
            {
                lever.Grab(coord);
                return true;
            }
            if (gripDown && lever.Held) lever.Drag(coord);
            else if (!gripDown && lever.Held) lever.Release();
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

    // Pinch strength 0..1 from the thumb-tip to index-tip distance (joint centres, metres): 1 at or below
    // ClosedDistance, 0 at or above OpenDistance. HandGrip's thresholds give the hysteresis: with the defaults a pinch
    // closes below ~3.1 cm and opens above ~3.8 cm.
    public sealed class HandPinch
    {
        public float ClosedDistance = 0.015f;
        public float OpenDistance = 0.050f;

        public float Strength(float tipDistance)
        {
            var span = OpenDistance - ClosedDistance;
            if (span <= 1e-5f) return tipDistance <= ClosedDistance ? 1f : 0f;
            var t = (OpenDistance - tipDistance) / span;
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        // The tip distance at which a given strength is reached (for logs and tests).
        public float DistanceAt(float strength) => OpenDistance - strength * (OpenDistance - ClosedDistance);
    }
}
