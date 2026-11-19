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
