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
        public Vector3 PokePoint;   // world: what presses the reset button (index tip, or the controller's grab point)
        public float Grip;          // 0..1
        public float TipDistance;   // bare hand only: thumb tip to index tip (m), for logs

        public static readonly HandSample None = new() { Source = InputSource.None };

        public static HandSample Controller(Vector3 point, float grip) =>
            new() { Source = InputSource.Controller, GrabPoint = point, MovePoint = point, PokePoint = point, Grip = grip };

        public static HandSample FromHand(Vector3 palm, Vector3 thumbTip, Vector3 indexTip, HandPinch pinch)
        {
            var tips = Vector3.Distance(thumbTip, indexTip);
            return new HandSample
            {
                Source = InputSource.Hand,
                GrabPoint = (thumbTip + indexTip) * 0.5f,
                MovePoint = palm,
                PokePoint = indexTip,
                Grip = pinch.Strength(tips),
                TipDistance = tips,
            };
        }
    }

    // What one hand did this frame (for feedback, haptics and saving the fit).
    public struct HandStep
    {
        public bool GrabbedKnob, GrabbedFit, Adjusted, FitReleased, Reset;
    }

    // One hand: its own handle's knob, plus the fit controls any hand may use: either table-height grip (move the hand
    // up/down), either handle base (move the hand sideways: both handles follow symmetrically) and the reset button
    // (hold a fingertip or the controller on it for ResetSeconds). A fresh press takes only the nearest target in reach
    // (GrabTargets), and a fit control another hand holds is not available.
    public sealed class HandDriver
    {
        public const float HeightGripRadius = 0.07f;
        public const float SpacingGripRadius = 0.06f;
        public const float ResetRadius = 0.03f;
        public const float ResetSeconds = 1.0f;

        public readonly HandGrip Grip = new();        // the hand's own handle
        public readonly HandGrip HeightGrip = new();  // Fit.HeightLever
        public readonly HandGrip SpacingGrip = new(); // Fit.SpacingLever
        public float ResetProgress { get; private set; } // 0..1 while held on the button
        int spacingSide; // which base the spacing grab started on: 0 left (pitch), 1 right (roll)
        bool resetArmed = true;
        readonly float[] distances = new float[3];
        readonly float[] radii = new float[3];
        readonly bool[] available = new bool[3];

        // The handle only (tests of the handle path).
        public bool Step(in HandSample s, Lever lever, LeverView view)
        {
            if (s.Source == InputSource.None) return Grip.Update(lever, InputSource.None, 0f, float.PositiveInfinity, 0f);
            var distance = Vector3.Distance(s.GrabPoint, view.Knob.position);
            return Grip.Update(lever, s.Source, s.Grip, distance, view.Coordinate(s.MovePoint));
        }

        // The handle and the fit controls.
        public HandStep Step(in HandSample s, Lever lever, LeverView view, LabyrinthGame game, float dt)
        {
            var result = new HandStep();
            var fit = game.Fit;
            var heightWasHeld = HeightGrip.Holding;
            var spacingWasHeld = SpacingGrip.Holding;
            if (s.Source == InputSource.None)
            {
                Grip.Update(lever, InputSource.None, 0f, float.PositiveInfinity, 0f);
                HeightGrip.Update(fit.HeightLever, InputSource.None, 0f, float.PositiveInfinity, 0f);
                SpacingGrip.Update(fit.SpacingLever, InputSource.None, 0f, float.PositiveInfinity, 0f);
                ResetProgress = 0f;
                resetArmed = true;
                result.FitReleased = heightWasHeld || spacingWasHeld;
                return result;
            }

            distances[0] = Vector3.Distance(s.GrabPoint, view.Knob.position);
            radii[0] = Grip.GrabRadius;
            available[0] = true;
            distances[1] = Nearest(game.HeightGrips, s.GrabPoint, out _);
            radii[1] = HeightGripRadius;
            available[1] = !fit.HeightLever.Held || HeightGrip.Holding;
            distances[2] = Nearest(game.SpacingGrips, s.GrabPoint, out var side);
            radii[2] = SpacingGripRadius;
            available[2] = !fit.SpacingLever.Held || SpacingGrip.Holding;
            GrabTargets.Gate(distances, radii, available);

            var local = game.transform.InverseTransformPoint(s.MovePoint);
            if (!SpacingGrip.Holding) spacingSide = side;
            var outward = spacingSide == 0 ? -local.x : local.x;

            result.GrabbedKnob = Grip.Update(lever, s.Source, s.Grip, distances[0], view.Coordinate(s.MovePoint));
            var h = HeightGrip.Update(fit.HeightLever, s.Source, s.Grip, distances[1], s.MovePoint.y);
            var w = SpacingGrip.Update(fit.SpacingLever, s.Source, s.Grip, distances[2], outward);
            result.GrabbedFit = h || w;
            if (HeightGrip.Holding || SpacingGrip.Holding) result.Adjusted = game.FitFromLevers(s.Source.ToString());
            result.FitReleased = (heightWasHeld && !HeightGrip.Holding) || (spacingWasHeld && !SpacingGrip.Holding);

            var onButton = game.ResetButton != null && Vector3.Distance(s.PokePoint, game.ResetButton.position) < ResetRadius;
            if (!onButton)
            {
                ResetProgress = 0f;
                resetArmed = true;
            }
            else if (resetArmed)
            {
                ResetProgress += dt / ResetSeconds;
                if (ResetProgress >= 1f)
                {
                    ResetProgress = 0f;
                    resetArmed = false; // once per touch
                    result.Reset = true;
                }
            }
            return result;
        }

        static float Nearest(Transform[] targets, Vector3 p, out int index)
        {
            index = 0;
            var best = float.PositiveInfinity;
            if (targets == null) return best;
            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                var d = Vector3.Distance(p, targets[i].position);
                if (d < best)
                {
                    best = d;
                    index = i;
                }
            }
            return best;
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
