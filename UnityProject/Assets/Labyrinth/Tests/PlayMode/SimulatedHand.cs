// A simulated bare right hand for tests and renders: the pinch point (between thumb tip and index tip) at a given
// world point, the palm behind it towards the player and a little up and to the right, as a right hand reaching
// forward-down to a knob. Distances are typical adult values (not measured on the headset).
using Labyrinth.Core;
using Labyrinth.Game;
using UnityEngine;

namespace Labyrinth.Tests
{
    public sealed class SimulatedHand
    {
        readonly Transform root; // the labyrinth root: forward = away from the player, right = player's right
        public Vector3 PinchPoint;
        public float Tips = 0.06f; // thumb tip to index tip (m); open hand

        public SimulatedHand(Transform labyrinthRoot, Vector3 pinchPoint)
        {
            root = labyrinthRoot;
            PinchPoint = pinchPoint;
        }

        // Pinching a knob: the fingertips meet just outside its surface on the player's side, slightly above centre.
        public static SimulatedHand AtKnob(Transform labyrinthRoot, Transform knob)
        {
            var radius = knob.lossyScale.x * 0.5f;
            var p = knob.position + labyrinthRoot.TransformDirection(new Vector3(0f, 0.25f, -1f).normalized * (radius + 0.008f));
            return new SimulatedHand(labyrinthRoot, p);
        }

        public Vector3 Palm => PinchPoint + root.TransformDirection(new Vector3(0.015f, 0.035f, -0.065f));
        public Vector3 ThumbTip => PinchPoint + root.TransformDirection(new Vector3(-0.35f, -0.25f, -0.9f).normalized * (Tips * 0.5f));
        public Vector3 IndexTip => PinchPoint - root.TransformDirection(new Vector3(-0.35f, -0.25f, -0.9f).normalized * (Tips * 0.5f));

        public void Move(Vector3 world) => PinchPoint += world;

        public HandSample Sample(HandPinch pinch) => HandSample.FromHand(Palm, ThumbTip, IndexTip, pinch);
    }
}
