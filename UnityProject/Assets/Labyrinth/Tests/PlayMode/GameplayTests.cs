// Gameplay in the real scene with real physics (script-stepped). Acceptance scenarios A-E of the epic.
using System.Collections;
using Labyrinth.Core;
using Labyrinth.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Labyrinth.Tests
{
    public class GameplayTests
    {
        LabyrinthGame g;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneLoad.Fresh(x => g = x);
            g.Pitch.ResetToNeutral();
            g.Roll.ResetToNeutral();
        }

        [TearDown]
        public void Restore() => SceneLoad.Restore();

        // Board-space displacement (x, z) of the ball over `seconds` with the handles held at (pitch, roll).
        Vector2 Roll(Vector2 from, float pitch, float roll, float seconds)
        {
            g.PlaceBall(from);
            g.Advance(0.05f); // settle
            var a = g.BallOnBoard;
            g.Pitch.Set(pitch);
            g.Roll.Set(roll);
            g.Advance(seconds);
            var b = g.BallOnBoard;
            return new Vector2(b.x - a.x, b.z - a.z);
        }

        static readonly Vector2 Open = new(6.0f, 3.0f); // a clear spot in lane 1

        [Test]
        public void LevelBoard_BallStaysPut()
        {
            g.Advance(0.2f);
            var a = g.BallOnBoard;
            g.Advance(1.5f);
            var b = g.BallOnBoard;
            Assert.Less((b - a).magnitude, 0.005f, $"moved from {a} to {b}");
            Assert.AreEqual(g.Layout.BallRadius, b.y, 0.004f, "ball resting on the floor");
        }

        // Scenario A
        [Test]
        public void PitchForward_RollsAway_PitchBack_RollsTowardsPlayer()
        {
            var away = Roll(Open, 0.6f, 0f, 0.35f);
            Assert.Greater(away.y, 0.01f, $"forward: {away}");
            Assert.Less(Mathf.Abs(away.x), 0.3f * away.y, $"forward drifted sideways: {away}");
            Assert.Less(g.Board.transform.forward.y, -0.03f, "far edge down");

            g.Pitch.Set(0f);
            g.Advance(0.5f);
            var back = Roll(Open, -0.6f, 0f, 0.35f);
            Assert.Less(back.y, -0.01f, $"back: {back}");
        }

        [Test]
        public void RollRight_RollsRight_RollLeft_RollsLeft()
        {
            var right = Roll(Open, 0f, 0.6f, 0.35f);
            Assert.Greater(right.x, 0.01f, $"right: {right}");
            Assert.Less(Mathf.Abs(right.y), 0.3f * right.x, $"right drifted: {right}");
            Assert.Less(g.Board.transform.right.y, -0.03f, "right edge down");

            g.Roll.Set(0f);
            g.Advance(0.5f);
            var left = Roll(Open, 0f, -0.6f, 0.35f);
            Assert.Less(left.x, -0.01f, $"left: {left}");
        }

        // Scenario B
        [Test]
        public void BothHandlesTogether_BoardTiltsOnBothAxes_BallFollowsDiagonal()
        {
            var d = Roll(Open, 0.5f, 0.5f, 0.35f);
            Assert.Greater(d.x, 0.008f, $"{d}");
            Assert.Greater(d.y, 0.008f, $"{d}");
            Assert.AreEqual(1f, d.x / d.y, 0.35f, $"diagonal: {d}");
            Assert.AreEqual(g.PitchAxis.MaxDegrees * 0.5f, g.PitchAxis.Degrees, 0.05f);
            Assert.AreEqual(g.RollAxis.MaxDegrees * 0.5f, g.RollAxis.Degrees, 0.05f);
        }

        [Test]
        public void Tilt_NeverExceedsTheLimit()
        {
            g.Pitch.Set(1f);
            g.Roll.Set(-1f);
            g.Advance(1.5f);
            var up = g.Board.transform.up;
            var tilt = Vector3.Angle(up, g.transform.up);
            var limit = Vector2.SqrMagnitude(new Vector2(g.PitchAxis.MaxDegrees, g.RollAxis.MaxDegrees));
            Assert.LessOrEqual(tilt, Mathf.Sqrt(limit) + 0.1f);
            Assert.Greater(tilt, g.PitchAxis.MaxDegrees);
        }

        // Scenario C: the ball rolls at full tilt into an interior wall and stops against it.
        [Test]
        public void Wall_BlocksTheBall()
        {
            var m = g.Layout;
            var from = new Vector2(2.5f, 1.2f); // lane 0, below the separator at y = 2
            Roll(from, 1f, 0f, 2.0f);
            var p = g.BallOnBoard;
            var wallFace = m.ToBoard(new Vector2(0, 2)).z - m.WallThickness / 2;
            Assert.LessOrEqual(p.z, wallFace - m.BallRadius + 0.002f, $"ball at {p} passed the wall face {wallFace}");
            Assert.Greater(p.z, wallFace - m.BallRadius - 0.01f, $"ball at {p} did not reach the wall");
            Assert.AreEqual(m.BallRadius, p.y, 0.005f, "still on the floor");
            Assert.Less(g.Ball.linearVelocity.magnitude, 0.05f, "came to rest against the wall");
        }

        [Test]
        public void RimWall_BlocksTheBall()
        {
            var m = g.Layout;
            Roll(new Vector2(1.5f, 1.4f), 0f, -1f, 2.0f); // full tilt to the left rim
            var p = g.BallOnBoard;
            Assert.GreaterOrEqual(p.x, -m.Width / 2 + m.WallThickness / 2 + m.BallRadius - 0.002f, $"{p}");
            Assert.AreEqual(RoundState.Playing, g.Round.State);
        }

        // Scenario D: the ball rolls into a hole, falls through, is detected, and comes back to the start.
        [Test]
        public void Hole_BallFallsThrough_AndIsResetToStart()
        {
            var events = new System.Collections.Generic.List<RoundEvent>();
            g.RoundChanged += events.Add;
            var hole = g.Layout.Holes[0]; // (3.5, 0.5)
            Roll(hole + new Vector2(0f, 0.75f), -0.7f, 0f, 1.0f);
            Assert.AreEqual(RoundState.Falling, g.Round.State, $"ball at {g.BallOnBoard}");
            Assert.Contains(RoundEvent.Fell, events);
            Assert.Less(g.BallOnBoard.y, -0.03f, "below the board");
            Assert.AreEqual(1, g.Round.Falls);

            g.Pitch.Set(0f);
            g.Advance(g.Round.FallPauseSeconds + 0.1f);
            Assert.AreEqual(RoundState.Playing, g.Round.State);
            Assert.Contains(RoundEvent.ResetBall, events);
            var start = g.Layout.ToBoard(g.Layout.Start);
            var p = g.BallOnBoard;
            Assert.Less(new Vector2(p.x - start.x, p.z - start.z).magnitude, 0.01f, $"reset to {p}");
            g.Advance(0.5f);
            Assert.AreEqual(g.Layout.BallRadius, g.BallOnBoard.y, 0.004f, "back on the floor after reset");
        }

        // Every hole of the layout swallows the ball (the visual review once found holes missing from the mesh).
        [Test]
        public void EveryHole_SwallowsTheBall()
        {
            for (var i = 0; i < g.Layout.Holes.Count; i++)
            {
                g.Restart();
                g.PlaceBall(g.Layout.Holes[i] + new Vector2(0.05f, 0.05f));
                g.Advance(0.6f);
                Assert.AreEqual(RoundState.Falling, g.Round.State, $"hole {i} at {g.Layout.Holes[i]}: ball at {g.Layout.ToCell(g.BallOnBoard)}");
            }
        }

        // The real frame loop (FixedUpdate physics, Update, LateUpdate) for a few seconds with a level board: the
        // labyrinth stays where it was built and the ball rests on the start. Advance() bypasses Update, so this
        // catches per-frame side effects (seen once: a sound source on the root dragged the whole labyrinth along).
        [UnityTest]
        public IEnumerator RealFrameLoop_KeepsLabyrinthAndBallInPlace()
        {
            var root = g.transform.position;
            Physics.simulationMode = SimulationMode.FixedUpdate;
            var until = Time.time + 1.5f;
            var frames = 0;
            while (Time.time < until && frames < 2000)
            {
                frames++;
                yield return null;
            }
            Assert.AreEqual(root, g.transform.position, "labyrinth root moved");
            var start = g.Layout.ToBoard(g.Layout.Start);
            var p = g.BallOnBoard;
            Assert.Less(new Vector2(p.x - start.x, p.z - start.z).magnitude, 0.01f, $"ball at {p}");
            Assert.AreEqual(g.Layout.BallRadius, p.y, 0.004f);
            Assert.AreEqual(g.transform.TransformPoint(new Vector3(0, g.SurfaceHeight, 0)), g.Board.position);
        }

        // Scenario E: reaching the goal wins, the ball settles there, and restart starts a new attempt.
        [Test]
        public void Goal_Wins_SettlesBall_AndRestartWorks()
        {
            Roll(g.Layout.Goal + new Vector2(1.2f, 0f), 0f, -0.5f, 2.0f);
            Assert.AreEqual(RoundState.Won, g.Round.State, $"ball at {g.Layout.ToCell(g.BallOnBoard)}");
            Assert.IsTrue(g.Ball.isKinematic);
            g.Roll.Set(1f); // tilting afterwards does not move the ball out of the goal
            g.Advance(1f);
            Assert.IsTrue(g.BallInGoal);
            Assert.AreEqual(RoundState.Won, g.Round.State);

            g.Restart();
            Assert.AreEqual(RoundState.Playing, g.Round.State);
            Assert.IsFalse(g.Ball.isKinematic);
            g.Advance(0.05f);
            var s = g.Layout.ToBoard(g.Layout.Start);
            Assert.Less(Vector2.Distance(new Vector2(g.BallOnBoard.x, g.BallOnBoard.z), new Vector2(s.x, s.z)), 0.02f);
        }

        // The hands' path: a hand grabs the pitch knob and pushes it 5 cm forward; the knob, the handle and the board follow.
        [Test]
        public void HandOnKnob_PushForward_TiltsFarEdgeDown_AndMovesTheKnob()
        {
            var view = g.PitchLever;
            var grip = new HandGrip();
            var hand = view.Knob.position;
            Assert.IsTrue(grip.Update(g.Pitch, true, 1f, 0f, view.Coordinate(hand)));
            var knobBefore = view.Knob.position;
            hand += g.transform.forward * 0.05f;
            grip.Update(g.Pitch, true, 1f, 0f, view.Coordinate(hand));
            Assert.AreEqual(0.05f / g.Pitch.Travel, g.Pitch.Value, 1e-3f);
            view.SetValue(g.Pitch.Value);
            var moved = view.Knob.position - knobBefore;
            Assert.AreEqual(0.05f, Vector3.Dot(moved, g.transform.forward), 0.012f, "knob follows the hand");
            g.Advance(1f);
            Assert.Less(g.Board.transform.forward.y, -0.03f);

            // Releasing keeps the handle where it was left (default).
            grip.Update(g.Pitch, true, 0f, 0f, view.Coordinate(hand));
            g.Advance(1f);
            Assert.AreEqual(0.05f / g.Pitch.Travel, g.Pitch.Value, 1e-3f);
        }

        [Test]
        public void HandOnRollKnob_PushRight_TiltsRightEdgeDown()
        {
            var view = g.RollLever;
            var grip = new HandGrip();
            var hand = view.Knob.position;
            grip.Update(g.Roll, true, 1f, 0f, view.Coordinate(hand));
            hand += g.transform.right * 0.04f;
            grip.Update(g.Roll, true, 1f, 0f, view.Coordinate(hand));
            Assert.Greater(g.Roll.Value, 0.4f);
            g.Advance(1f);
            Assert.Less(g.Board.transform.right.y, -0.03f);
            Assert.AreEqual(0f, g.Pitch.Value, "the other handle is untouched");
        }

        // The maze can be completed by handle movements alone (navigable, physically possible).
        [Test]
        public void Autopilot_CompletesTheMaze_WithHandlesOnly()
        {
            var pilot = new Autopilot(g);
            var t = 0f;
            while (g.Round.State != RoundState.Won && t < 180f)
            {
                pilot.Step();
                g.Advance(Time.fixedDeltaTime);
                t += Time.fixedDeltaTime;
                if (g.Round.State == RoundState.Falling)
                    Assert.Fail($"fell at waypoint {pilot.Waypoint}, ball {g.Layout.ToCell(g.BallOnBoard)}, t={t:0.0}s");
            }
            Assert.AreEqual(RoundState.Won, g.Round.State, $"stuck at waypoint {pilot.Waypoint}, ball {g.Layout.ToCell(g.BallOnBoard)}");
            Debug.Log($"[autopilot] completed in {g.Round.WonSeconds:0.0} s, {g.Round.Falls} falls");
        }
    }
}
