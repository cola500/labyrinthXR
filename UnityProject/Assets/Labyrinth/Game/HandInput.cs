// The bridge from one hand's raw input (a Touch controller or a tracked bare hand) to the existing HandGrip -> Lever
// path. XrRig fills a HandSample per frame from the devices; tests build the same samples from simulated poses.
// Bare hand (Spike 1): pinch strength (thumb tip to index tip) replaces the analogue grip, the pinch point (between the
// two tips) decides whether the hand is near enough to the knob to grab, and the palm drives the handle while held
// (expected to be steadier than the fingertips while pinching; measured on the headset by HandLog).
using Labyrinth.Core;
using UnityEngine;

namespace Labyrinth.Game
{
    public struct HandSample
    {
        public InputSource Source;
        public Vector3 GrabPoint;   // world: compared with the knob for grabbing
        public Vector3 MovePoint;   // world: projected on the handle's travel while held
        public float Grip;          // 0..1
        public float TipDistance;   // bare hand only: thumb tip to index tip (m), for logs

        public static readonly HandSample None = new() { Source = InputSource.None };

        public static HandSample Controller(Vector3 point, float grip) =>
            new() { Source = InputSource.Controller, GrabPoint = point, MovePoint = point, Grip = grip };

        public static HandSample FromHand(Vector3 palm, Vector3 thumbTip, Vector3 indexTip, HandPinch pinch)
        {
            var tips = Vector3.Distance(thumbTip, indexTip);
            return new HandSample
            {
                Source = InputSource.Hand,
                GrabPoint = (thumbTip + indexTip) * 0.5f,
                MovePoint = palm,
                Grip = pinch.Strength(tips),
                TipDistance = tips,
            };
        }
    }

    // One hand on one handle: the HandGrip plus the geometry (distance to the knob, coordinate along the travel).
    public sealed class HandDriver
    {
        public readonly HandGrip Grip = new();

        // Returns true on the frame a grab starts.
        public bool Step(in HandSample s, Lever lever, LeverView view)
        {
            if (s.Source == InputSource.None) return Grip.Update(lever, InputSource.None, 0f, float.PositiveInfinity, 0f);
            var distance = Vector3.Distance(s.GrabPoint, view.Knob.position);
            return Grip.Update(lever, s.Source, s.Grip, distance, view.Coordinate(s.MovePoint));
        }
    }

    // Debug markers for a bare hand: palm (blue), thumb tip (red), index tip (green), pinch point (white, shown while
    // the pinch counts as closed). Built from primitives with the hand material, coloured per renderer.
    public sealed class HandMarkers
    {
        readonly Transform palm, thumb, index, pinch;
        readonly Renderer pinchRenderer;

        public HandMarkers(Transform parent, Material material, string name)
        {
            palm = Marker(parent, material, $"{name}Palm", 0.022f, new Color(0.25f, 0.45f, 0.95f));
            thumb = Marker(parent, material, $"{name}ThumbTip", 0.012f, new Color(0.95f, 0.25f, 0.20f));
            index = Marker(parent, material, $"{name}IndexTip", 0.012f, new Color(0.25f, 0.85f, 0.30f));
            pinch = Marker(parent, material, $"{name}Pinch", 0.008f, Color.white);
            pinchRenderer = pinch.GetComponent<Renderer>();
            Show(false, default, default, default, false);
        }

        public void Show(bool visible, Vector3 palmAt, Vector3 thumbAt, Vector3 indexAt, bool pinched)
        {
            palm.gameObject.SetActive(visible);
            thumb.gameObject.SetActive(visible);
            index.gameObject.SetActive(visible);
            pinch.gameObject.SetActive(visible && pinched);
            if (!visible) return;
            palm.position = palmAt;
            thumb.position = thumbAt;
            index.position = indexAt;
            pinch.position = (thumbAt + indexAt) * 0.5f;
        }

        public void Destroy()
        {
            foreach (var t in new[] { palm, thumb, index, pinch })
                if (t != null) Object.Destroy(t.gameObject);
        }

        static Transform Marker(Transform parent, Material material, string name, float size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * 0.4f);
            r.SetPropertyBlock(block);
            return go.transform;
        }
    }
}
