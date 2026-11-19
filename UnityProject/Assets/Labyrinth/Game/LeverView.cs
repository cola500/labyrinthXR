// A handle's visible part: a pivot on the frame with a shaft and a knob. It swings along TravelLocal (in the
// labyrinth root's space: forward for pitch, right for roll) by Value x MaxAngle, and shows whether a hand is near
// enough to grab it (Hover) or holds it (Held).
using UnityEngine;

namespace Labyrinth.Game
{
    public sealed class LeverView : MonoBehaviour
    {
        [SerializeField] Vector3 travelLocal = Vector3.forward;
        [SerializeField] float shaftLength = 0.16f;
        [SerializeField] Transform knob;
        [SerializeField] Renderer knobRenderer;
        [SerializeField] Color idle = new(0.55f, 0.16f, 0.10f);
        [SerializeField] Color hover = new(0.85f, 0.40f, 0.18f);
        [SerializeField] Color held = new(0.95f, 0.62f, 0.25f);

        public float MaxAngle = 30f;
        public float ShaftLength => shaftLength;
        public Transform Knob => knob;
        MaterialPropertyBlock block;
        int lastState = -1;

        // The direction the knob moves for a positive value, in world space.
        public Vector3 TravelWorld => transform.parent.TransformDirection(travelLocal).normalized;

        public void Wire(Vector3 travel, float length, Transform knobTransform, Renderer knobRend)
        {
            travelLocal = travel;
            shaftLength = length;
            knob = knobTransform;
            knobRenderer = knobRend;
        }

        public void SetValue(float value)
        {
            var hinge = Vector3.Cross(Vector3.up, travelLocal.normalized);
            transform.localRotation = Quaternion.AngleAxis(value * MaxAngle, hinge);
        }

        // The hand's position along the travel axis (metres from the pivot).
        public float Coordinate(Vector3 worldPoint) => Vector3.Dot(worldPoint - transform.position, TravelWorld);

        public void ShowState(bool isHover, bool isHeld)
        {
            var state = isHeld ? 2 : isHover ? 1 : 0;
            if (state == lastState || knobRenderer == null) return;
            lastState = state;
            block ??= new MaterialPropertyBlock();
            knobRenderer.GetPropertyBlock(block);
            block.SetColor("_Color", state == 2 ? held : state == 1 ? hover : idle);
            knobRenderer.SetPropertyBlock(block);
        }
    }
}
