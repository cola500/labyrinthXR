// Five levels in the real scene with real (script-stepped) physics: loading, clean transitions, holes, completion by
// handles only, progression, retry, final completion and restart (acceptance criteria AC-01..AC-07).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Labyrinth.Core;
using Labyrinth.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Labyrinth.Tests
{
    public class LevelTests
    {
        LabyrinthGame g;
        LabyrinthFeedback feedback;

        static IEnumerable<int> All() => Enumerable.Range(0, Levels.Count);

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneLoad.Fresh(x => g = x);
            feedback = g.GetComponent<LabyrinthFeedback>();
            g.Pitch.ResetToNeutral();
            g.Roll.ResetToNeutral();
        }

        [TearDown]
        public void Restore() => SceneLoad.Restore();

        // Colliders the board should have on a level: the floor, every wall (rim walls included), the 4 outer rim boxes.
        static int ExpectedColliders(MazeLayout m) => 1 + m.AllWalls().Count() + 4;

        Vector2 BallCell => g.Layout.ToCell(g.BallOnBoard);

        void AssertAtStart(string because)
        {
            var s = g.Layout.ToBoard(g.Layout.Start);
            var p = g.BallOnBoard;
            Assert.Less(new Vector2(p.x - s.x, p.z - s.z).magnitude, 0.012f, $"{because}: ball at {BallCell}, start {g.Layout.Start}");
        }

        // Puts the ball beside the goal and lets a gentle tilt roll it in.
        void RollIntoGoal()
        {
            var m = g.Layout;
            var route = m.Route;
            var before = route[route.Count - 2];
            var dir = (m.Goal - before).normalized;
            g.PlaceBall(m.Goal - dir * 0.45f);
            g.Pitch.Set(dir.y * 0.4f);
            g.Roll.Set(dir.x * 0.4f);
            for (var t = 0f; t < 3f && !g.Won; t += Time.fixedDeltaTime) g.Advance(Time.fixedDeltaTime);
            g.Pitch.Set(0f);
            g.Roll.Set(0f);
            Assert.IsTrue(g.Won, $"level {m.Number}: ball at {BallCell} did not reach the goal {m.Goal}");
        }

        // AC-01
        [Test]
        public void GameStarts_AtLevelOne()
        {
            Assert.AreEqual(1, g.Layout.Number);
            Assert.AreEqual(0, g.Campaign.Index);
            Assert.AreEqual("Level 1 / 5 — First Steps", feedback.PlaqueText);
            AssertAtStart("start of the game");
            Assert.AreEqual(ExpectedColliders(g.Layout), g.Board.GetComponentsInChildren<Collider>().Length);
        }

        // AC-01 / AC-07: each level loads with exactly its own geometry, the ball rests on its start (not in a wall,
        // not over a hole), and no collider of the previous level is left.
        [UnityTest]
        public IEnumerator EachLevel_LoadsCleanly([ValueSource(nameof(All))] int i)
        {
            g.LoadLevel(Levels.Count - 1 - i); // come from a different level
            g.LoadLevel(i);
            Assert.AreEqual(i + 1, g.Layout.Number);
            Assert.AreEqual(ExpectedColliders(g.Layout), g.Board.GetComponentsInChildren<Collider>().Length, "colliders on the board");
            Assert.AreEqual(1, g.Board.GetComponentsInChildren<MeshCollider>().Length, "one floor");
            var mazes = Enumerable.Range(0, g.Board.transform.childCount).Select(k => g.Board.transform.GetChild(k))
                                  .Where(t => t.name.StartsWith("Maze")).ToList();
            Assert.AreEqual(1, mazes.Count, "one maze under the board");
            Assert.AreSame(g.Maze, mazes[0].gameObject);
            Assert.AreEqual($"Level {i + 1} / 5 — {g.Layout.Name}", feedback.PlaqueText);
            g.Advance(1.0f);
            Assert.AreEqual(RoundState.Playing, g.Round.State, $"ball fell at the start: {BallCell}");
            AssertAtStart("level loaded");
            Assert.AreEqual(g.Layout.BallRadius, g.BallOnBoard.y, 0.004f, "resting on the floor");
            yield return null; // the old mazes are destroyed at the end of the frame
            var leftovers = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                  .Count(t => t.name.StartsWith("Maze ") && t.gameObject != g.Maze);
            Assert.AreEqual(0, leftovers, "old mazes left in the scene");
        }

        [Test]
        public void EveryHole_OnEveryLevel_SwallowsTheBall([ValueSource(nameof(All))] int i)
        {
            g.LoadLevel(i);
            for (var h = 0; h < g.Layout.Holes.Count; h++)
            {
                g.Restart();
                g.PlaceBall(g.Layout.Holes[h] + new Vector2(0.05f, 0.05f));
                g.Advance(0.6f);
                Assert.AreEqual(RoundState.Falling, g.Round.State, $"level {i + 1} hole {h} at {g.Layout.Holes[h]}: ball at {BallCell}");
            }
        }

        // FR-01: every level can be completed by handle movements alone (physically navigable). Not a measure of
        // how hard it is for a person.
        [Test]
        public void Autopilot_CompletesEachLevel([ValueSource(nameof(All))] int i)
        {
            g.LoadLevel(i);
            var pilot = new Autopilot(g);
            var t = 0f;
            while (!g.Won && t < 240f)
            {
                pilot.Step();
                g.Advance(Time.fixedDeltaTime);
                t += Time.fixedDeltaTime;
                if (g.Round.State == RoundState.Falling)
                    Assert.Fail($"level {i + 1}: fell at waypoint {pilot.Waypoint}, ball {BallCell}, t={t:0.0}s");
            }
            Assert.IsTrue(g.Won, $"level {i + 1}: stuck at waypoint {pilot.Waypoint}, ball {BallCell}");
            Assert.AreEqual(i + 1, g.Layout.Number, "still on the level it completed");
            Debug.Log($"[autopilot] level {i + 1} {g.Layout.Name}: {g.Round.WonSeconds:0.0} s, 0 falls");
        }

        // AC-03: the goal completes the level; after the pause the next level loads with the ball on its start.
        [UnityTest]
        public IEnumerator Goal_AdvancesToTheNextLevel_AfterThePause()
        {
            var levels = new List<int>();
            g.LevelLoaded += levels.Add;
            RollIntoGoal();
            yield return null; // let the message update
            StringAssert.StartsWith("Level 1 complete!", feedback.Text);
            StringAssert.Contains("Next: Finding Balance", feedback.Text);
            g.Advance(g.Campaign.AdvanceSeconds * 0.8f);
            Assert.AreEqual(1, g.Layout.Number, "still showing the completed level");
            g.Advance(g.Campaign.AdvanceSeconds * 0.3f);
            Assert.AreEqual(2, g.Layout.Number);
            CollectionAssert.AreEqual(new[] { 1 }, levels, "exactly one transition");
            Assert.AreEqual(RoundState.Playing, g.Round.State);
            Assert.IsFalse(g.Ball.isKinematic);
            AssertAtStart("after the transition");
            Assert.AreEqual("Level 2 / 5 — Finding Balance", feedback.PlaqueText);
            g.Advance(3f);
            Assert.AreEqual(2, g.Layout.Number, "no further transition");
        }

        // AC-04: a fall on Level 4 resets the ball to Level 4's start.
        [Test]
        public void FallOnLevelFour_StaysOnLevelFour()
        {
            g.LoadLevel(3);
            g.PlaceBall(g.Layout.Holes[2] + new Vector2(0.05f, 0f));
            g.Advance(0.6f);
            Assert.AreEqual(RoundState.Falling, g.Round.State);
            g.Advance(g.Round.FallPauseSeconds + 0.2f);
            Assert.AreEqual(RoundState.Playing, g.Round.State);
            Assert.AreEqual(4, g.Layout.Number);
            Assert.AreEqual(3, g.Campaign.Index);
            AssertAtStart("after the fall");
        }

        // A/X during play: a fresh attempt at the current level, not Level 1.
        [Test]
        public void Restart_DuringPlay_RetriesTheCurrentLevel()
        {
            g.LoadLevel(2);
            g.PlaceBall(new Vector2(5f, 1.4f));
            g.Restart();
            Assert.AreEqual(3, g.Layout.Number);
            AssertAtStart("retry");
        }

        // AC-05: completing Level 5 shows the completion state (no further level), and A/X restarts from Level 1.
        [UnityTest]
        public IEnumerator LevelFive_Completion_ThenRestartFromLevelOne()
        {
            g.LoadLevel(4);
            RollIntoGoal();
            g.Advance(g.Campaign.AdvanceSeconds * 2f);
            yield return null;
            Assert.AreEqual(5, g.Layout.Number);
            Assert.IsTrue(g.CampaignComplete);
            StringAssert.StartsWith("You mastered all five labyrinths!", feedback.Text);
            StringAssert.Contains("from Level 1", feedback.Text);
            g.Restart();
            Assert.AreEqual(1, g.Layout.Number);
            Assert.AreEqual(RoundState.Playing, g.Round.State);
            Assert.IsFalse(g.CampaignComplete);
            AssertAtStart("restart from level 1");
            Assert.AreEqual("Level 1 / 5 — First Steps", feedback.PlaqueText);
        }

        [Test]
        public void RestartFromFirstLevel_FromTheMiddle()
        {
            g.LoadLevel(2);
            g.RestartFromFirstLevel();
            Assert.AreEqual(1, g.Layout.Number);
            Assert.AreEqual(0, g.Campaign.Index);
            AssertAtStart("back to level 1");
        }

        // AC-06: the same physics and control parameters on every level.
        [Test]
        public void PhysicsAndControls_AreTheSameOnEveryLevel()
        {
            var ballCollider = g.Ball.GetComponent<SphereCollider>();
            var reference = Snapshot();
            foreach (var i in All())
            {
                g.LoadLevel(i);
                Assert.AreEqual(reference, Snapshot(), $"level {i + 1}");
                var floor = g.Board.GetComponentInChildren<MeshCollider>();
                Assert.AreEqual(g.Board.GetComponentsInChildren<BoxCollider>()[0].sharedMaterial, floor.sharedMaterial, "wood everywhere");
            }

            string Snapshot() => string.Join("|", g.Ball.mass, g.Ball.linearDamping, g.Ball.angularDamping, g.Ball.maxAngularVelocity,
                ballCollider.radius * g.Ball.transform.lossyScale.x, ballCollider.sharedMaterial.dynamicFriction,
                g.PitchAxis.MaxDegrees, g.RollAxis.MaxDegrees, g.PitchAxis.MaxDegreesPerSecond, g.Pitch.Travel, g.Roll.Travel,
                Physics.gravity, Time.fixedDeltaTime, g.Board.GetComponentInChildren<MeshCollider>().sharedMaterial.dynamicFriction);
        }

        // The full campaign: all five levels in order through normal progression, with no level skipped or repeated.
        [Test]
        public void AllFiveLevels_ThroughNormalProgression()
        {
            var seen = new List<int> { g.Layout.Number };
            g.LevelLoaded += i => seen.Add(i + 1);
            for (var level = 1; level <= 5; level++)
            {
                RollIntoGoal();
                g.Advance(g.Campaign.AdvanceSeconds + 0.2f);
            }
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, seen);
            Assert.IsTrue(g.CampaignComplete);
        }
    }
}
