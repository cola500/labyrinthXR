// Off-headset renders of the real scene for visual review (epic §8). Explicit: needs a graphics device, so it is not
// part of the normal (-nographics) test run. Run: scripts/labyrinth.sh snapshots -> UnityProject/Logs/shots/*.png
using System.Collections;
using System.IO;
using Labyrinth.Core;
using Labyrinth.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Labyrinth.Tests
{
    [Explicit, Category("Snapshots")]
    public class Snapshots
    {
        LabyrinthGame g;
        Camera cam;

        [UnitySetUp]
        public IEnumerator Load()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("needs a graphics device: scripts/labyrinth.sh snapshots");
            yield return SceneLoad.Fresh(x => g = x);
            cam = Camera.main;
            Directory.CreateDirectory("Logs/shots");
        }

        [TearDown]
        public void Restore() => SceneLoad.Restore();

        Vector3 W(float x, float y, float z) => g.transform.TransformPoint(new Vector3(x, y, z));

        [UnityTest]
        public IEnumerator Snapshots_RenderStates()
        {
            g.Advance(0.3f);
            var h = g.SurfaceHeight;
            // 1. Player's viewpoint (desktop eye: standing adult behind the handles).
            Shot("01-player-view", W(0, 1.62f, -0.95f), W(0, 0.92f, 0.05f), 60);
            // 2. Typical VR view: eyes 1.65 m, 0.80 m from the board centre, Quest-like field of view, looking down.
            Shot("02-vr-eye-view", W(0, 1.65f, -0.80f), W(0, h, -0.05f), 95);
            // 3. Overview of the whole board from above.
            Shot("03-overview-top", W(0, 2.45f, -0.05f), W(0, h, 0.0f), 40);
            // 4. Close-up of the ball at the start.
            var ball = g.Ball.position;
            Shot("04-ball-closeup", ball + g.transform.TransformDirection(new Vector3(0.12f, 0.22f, -0.28f)), ball, 45);
            // 5. Goal area and nearby holes.
            var goal = g.Board.transform.TransformPoint(g.Layout.ToBoard(g.Layout.Goal));
            Shot("05-goal-and-holes", goal + g.transform.TransformDirection(new Vector3(0.25f, 0.42f, -0.45f)), goal + g.transform.TransformDirection(new Vector3(0.2f, 0, -0.05f)), 50);
            // 6. Side view at board height: board inside the frame, handles, table (floating/intersecting geometry).
            Shot("06-side-level", W(1.45f, h + 0.12f, -0.55f), W(0, h - 0.05f, -0.1f), 50);
            // 7. Handles close-up.
            Shot("07-handles", W(0, h + 0.35f, -1.05f), W(0, h + 0.02f, -0.45f), 60);

            // 7b. The message head-on (text legibility).
            var msg = GameObject.Find("Message").transform;
            Shot("07b-message-headon", msg.position - msg.forward * 0.9f, msg.position, 40);

            // 8. A tilted board mid-run: the autopilot plays for a while.
            var pilot = new Autopilot(g);
            for (var i = 0; i < 90 * 7 && g.Round.State == RoundState.Playing; i++)
            {
                pilot.Step();
                g.Advance(Time.fixedDeltaTime);
            }
            g.Pitch.Set(1f);
            g.Roll.Set(-1f);
            g.Advance(0.5f); // show the maximum combined tilt
            yield return null; // let LateUpdate pose the handles
            Shot("08-tilted-player-view", W(0, 1.62f, -0.95f), W(0, 0.92f, 0.05f), 60);
            Shot("09-tilted-side", W(1.35f, h + 0.40f, -0.75f), W(0, h, 0f), 50);

            // 10. Falling: a ball dropped into a hole, with the message.
            g.Pitch.Set(0f);
            g.Roll.Set(0f);
            g.Advance(0.6f);
            g.PlaceBall(g.Layout.Holes[1] + new Vector2(0.15f, 0f));
            g.Advance(0.25f);
            yield return null;
            Shot("10-falling", W(0, 1.62f, -0.95f), W(0, 0.92f, 0.05f), 60);
            var hole = g.Board.transform.TransformPoint(g.Layout.ToBoard(g.Layout.Holes[1]));
            Shot("11-ball-in-hole", hole + g.transform.TransformDirection(new Vector3(0.05f, 0.2f, -0.2f)), hole, 45);

            // 12. Success: the ball in the goal, glowing goal and message.
            g.Advance(1.2f);
            g.PlaceBall(g.Layout.Goal + new Vector2(0.5f, 0f));
            g.Roll.Set(-0.6f);
            g.Advance(1.5f);
            g.Roll.Set(0f);
            g.Advance(0.5f);
            yield return null;
            Assert.AreEqual(RoundState.Won, g.Round.State);
            Shot("12-won-player-view", W(0, 1.62f, -0.95f), W(0, 0.92f, 0.05f), 60);
            Shot("13-won-goal", goal + g.transform.TransformDirection(new Vector3(0.15f, 0.3f, -0.3f)), goal, 45);
        }

        // FEATURE-levels: every level as the game loads it (overview + player view), the plaque, the level-complete
        // message, the transition, and the final completion state.
        [UnityTest]
        public IEnumerator Snapshots_Levels()
        {
            var h = g.SurfaceHeight;
            for (var i = 0; i < Levels.Count; i++)
            {
                g.LoadLevel(i);
                g.Advance(0.3f);
                yield return null; // plaque, message and handles update
                Shot($"L{i + 1}-a-overview", W(0, 2.45f, -0.05f), W(0, h, 0.0f), 40);
                Shot($"L{i + 1}-b-player-view", W(0, 1.62f, -0.95f), W(0, 0.92f, 0.05f), 60);
            }
            g.LoadLevel(2);
            g.Advance(0.3f);
            yield return null;
            var plaque = GameObject.Find("LevelPlaque").transform;
            Shot("L-plaque-closeup", plaque.position + g.transform.TransformDirection(new Vector3(0.1f, 0.35f, -0.45f)), plaque.position, 45);

            // Level complete on Level 1, then the transition to Level 2.
            g.LoadLevel(0);
            var goal = g.Layout.Goal;
            g.PlaceBall(goal + new Vector2(0.45f, 0f));
            g.Roll.Set(-0.4f);
            for (var t = 0f; t < 3f && !g.Won; t += Time.fixedDeltaTime) g.Advance(Time.fixedDeltaTime);
            g.Roll.Set(0f);
            g.Advance(0.5f);
            yield return null;
            Assert.IsTrue(g.Won);
            Shot("L1-c-level-complete", W(0, 1.62f, -0.95f), W(0, 0.92f, 0.05f), 60);
            g.Advance(g.Campaign.AdvanceSeconds);
            yield return null;
            Assert.AreEqual(2, g.Layout.Number);
            Shot("L2-c-after-transition", W(0, 1.62f, -0.95f), W(0, 0.92f, 0.05f), 60);

            // Final completion on Level 5.
            g.LoadLevel(4);
            goal = g.Layout.Goal;
            var before = g.Layout.Route[g.Layout.Route.Count - 2];
            var dir = (goal - before).normalized;
            g.PlaceBall(goal - dir * 0.45f);
            g.Pitch.Set(dir.y * 0.4f);
            g.Roll.Set(dir.x * 0.4f);
            for (var t = 0f; t < 3f && !g.Won; t += Time.fixedDeltaTime) g.Advance(Time.fixedDeltaTime);
            g.Pitch.Set(0f);
            g.Roll.Set(0f);
            g.Advance(1f);
            yield return null;
            Assert.IsTrue(g.CampaignComplete);
            Shot("L5-c-campaign-complete", W(0, 1.62f, -0.95f), W(0, 0.92f, 0.05f), 60);
        }

        // Fit: the default layout and a low table with narrow handles (0.70 m, 15 cm from the centre each = 30 cm
        // apart), from an adult's and a child's viewpoint, with the bare-hand markers of a simulated right hand on the
        // moved roll knob, the fit controls and the side clearance.
        [UnityTest]
        public IEnumerator Snapshots_Fit()
        {
            g.LoadLevel(1);
            var rig = Object.FindAnyObjectByType<XrRig>();
            var markers = new HandMarkers(new GameObject("HandMarkersTest").transform, rig.HandMaterial, "Sim");
            var leftMarkers = new HandMarkers(new GameObject("LeftHandMarkersTest").transform, rig.HandMaterial, "SimLeft");
            foreach (var (name, height, spacing) in new[] { ("F1-default", PlayerFit.DefaultTableHeight, PlayerFit.DefaultLeverSpacing), ("F2-low-narrow", 0.70f, 0.15f) })
            {
                g.SetFit(height, spacing, "snapshot");
                g.Pitch.Set(0.3f);
                g.Roll.Set(-0.4f);
                g.Advance(0.6f);
                yield return null;
                var hand = SimulatedHand.AtKnob(g.transform, g.RollLever.Knob);
                hand.Tips = 0.02f;
                markers.Show(true, hand.Palm, hand.ThumbTip, hand.IndexTip, true);
                var lhand = SimulatedHand.AtKnob(g.transform, g.PitchLever.Knob, left: true);
                lhand.Tips = 0.02f;
                leftMarkers.Show(true, lhand.Palm, lhand.ThumbTip, lhand.IndexTip, true);
                yield return null;
                var top = g.transform.position.y + g.SurfaceHeight; // world board surface
                Vector3 At(float x, float y, float z) // (x, z) in the labyrinth's floor plan, y = world height
                {
                    var w = g.transform.TransformPoint(new Vector3(x, 0, z));
                    w.y = y;
                    return w;
                }
                Shot($"{name}-a-adult-view", At(0, 1.62f, -0.95f), At(0, top - 0.03f, 0.05f), 60);
                Shot($"{name}-b-child-view", At(0, 1.20f, -0.80f), At(0, top - 0.02f, 0.0f), 75);
                Shot($"{name}-c-handles", At(0, top + 0.42f, -1.20f), At(0, top + 0.10f, -0.55f), 50);
                Shot($"{name}-d-side", At(1.55f, top + 0.10f, -0.70f), At(0, top - 0.25f, -0.2f), 55);
                var grip = g.HeightGrips[1].position;
                var towards = (g.ResetButton.position + grip) * 0.5f;
                Shot($"{name}-e-fit-controls", towards + g.transform.TransformDirection(new Vector3(-0.05f, 0.35f, -0.55f)), towards, 60);
            }
            markers.Destroy();
            leftMarkers.Destroy();
            g.SetFit(PlayerFit.DefaultTableHeight, PlayerFit.DefaultLeverSpacing, "snapshot");
        }

        // Spike 1: the bare-hand debug markers (palm blue, thumb tip red, index tip green, pinch point white) of a
        // simulated right hand pinching the roll knob, built by the runtime's HandMarkers with the rig's hand material,
        // then the hand moved 4 cm right through HandDriver so the handle and board follow. Layout only: real tracked
        // poses exist only on the headset.
        [UnityTest]
        public IEnumerator Snapshots_HandMarkers()
        {
            g.LoadLevel(1);
            g.Advance(0.3f);
            var rig = Object.FindAnyObjectByType<XrRig>();
            var markers = new HandMarkers(new GameObject("HandMarkersTest").transform, rig.HandMaterial, "Sim");
            var view = g.RollLever;
            var hand = SimulatedHand.AtKnob(g.transform, view.Knob);
            hand.Tips = 0.02f;
            var pinch = new HandPinch();
            var driver = new HandDriver();
            driver.Step(hand.Sample(pinch), g.Roll, view);
            Assert.IsTrue(g.Roll.Held);
            yield return null;
            markers.Show(true, hand.Palm, hand.ThumbTip, hand.IndexTip, true);
            yield return null;
            var k = view.Knob.position;
            Vector3 D(float x, float y, float z) => g.transform.TransformDirection(new Vector3(x, y, z));
            Shot("H1-hand-at-knob-player", k + D(-0.10f, 0.28f, -0.32f), k + D(0, 0.02f, -0.03f), 40);
            Shot("H2-hand-at-knob-side", k + D(0.30f, 0.10f, -0.12f), k + D(0, 0.02f, -0.04f), 30);
            Shot("H3-hand-at-knob-top", k + D(0.0f, 0.38f, -0.02f), k + D(0, 0, -0.03f), 35);

            for (var i = 0; i < 40; i++)
            {
                hand.Move(g.transform.right * 0.001f);
                driver.Step(hand.Sample(pinch), g.Roll, view);
                g.Advance(0.011f);
            }
            g.Advance(1f);
            yield return null; // LateUpdate poses the handle
            markers.Show(true, hand.Palm, hand.ThumbTip, hand.IndexTip, true);
            yield return null;
            Shot("H4-hand-moved-right-player", k + D(-0.10f, 0.28f, -0.32f), k + D(0.02f, 0.02f, -0.03f), 40);
            Shot("H5-hand-moved-right-board", W(0, 1.62f, -0.95f), W(0.2f, 0.92f, -0.2f), 60);
            markers.Destroy();
        }

        void Shot(string name, Vector3 eye, Vector3 look, float fov)
        {
            const int w = 1600, h = 1200;
            cam.transform.position = eye;
            cam.transform.LookAt(look);
            cam.fieldOfView = fov;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes($"Logs/shots/{name}.png", tex.EncodeToPNG());
            Object.Destroy(rt);
            Object.Destroy(tex);
        }
    }
}
