// One hand's grip on its own handle, as pure logic. A grab starts only on a fresh grip press with the hand within
// GrabRadius of the knob (holding the grip and then reaching for the knob does not grab); while the grip is held the
// handle follows the hand; letting go, or losing tracking, releases it.
namespace Labyrinth.Core
{
    public sealed class HandGrip
    {
        public float GrabRadius = 0.10f;
        public float PressThreshold = 0.55f;
        public float ReleaseThreshold = 0.35f;

        public bool Hover { get; private set; }
        bool gripDown;

        // grip: the analogue grip value 0..1. distance: hand to knob (m). coord: the hand along the handle's travel (m).
        // Returns true on the frame a grab starts.
        public bool Update(Lever lever, bool tracked, float grip, float distance, float coord)
        {
            if (!tracked)
            {
                if (lever.Held) lever.Release();
                gripDown = false;
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
}
