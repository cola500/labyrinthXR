// The player's fit (table height, handle spacing) in the real scene with real physics: everything moves together,
// the handles' grab zones and bases follow, nothing collides at the narrowest/lowest fit, an adjustment while a handle
// is held lets go without a jolt, the fit survives restarts/levels/recentering, and bare hands can adjust it.
using System.Collections;
using System.Collections.Generic;
using Labyrinth.Core;
using Labyrinth.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Labyrinth.Tests
{
    public class FitTests
    {
        LabyrinthGame g;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneLoad.Fresh(x => g = x);
            g.LoadLevel(1);
            g.Pitch.ResetToNeutral();
            g.Roll.ResetToNeutral();
        }

        [TearDown]
        public void Restore() => SceneLoad.Restore();

        Transform Find(string name) => g.transform.Find(name) ?? FindDeep(g.transform, name);

        static Transform FindDeep(Transform t, string name)
        {
            foreach (Transform c in t)
            {
                if (c.name == name) return c;
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        Vector3 Local(Transform t) => g.transform.InverseTransformPoint(t.position);

        [Test]
        public void DefaultFit_IsTheBuiltLayout()
        {
            Assert.IsTrue(g.Fit.IsDefault);
            Assert.AreEqual(-0.36f, Local(g.PitchLever.transform).x, 1e-4f);
            Assert.AreEqual(0.36f, Local(g.RollLever.transform).x, 1e-4f);
            Assert.AreEqual(0.95f, g.Board.position.y - g.FloorY, 1e-4f, "board surface 95 cm above the floor");
            Assert.IsTrue(g.PlaqueOnTop);
            foreach (var leg in g.Legs) Assert.AreEqual(g.FloorY, leg.GetComponent<Renderer>().bounds.min.y, 1e-3f, "legs on the floor");
        }

        [Test]
        public void LowerTable_MovesEverythingTogether_LegsStillReachTheFloor()
        {
            g.Advance(0.3f);
            var parts = new[] { g.Board.transform, Find("Frame"), Find("TableTop"), g.PitchLever.Knob, g.RollLever.Knob,
                                g.Plaque, g.HeightGrips[0], g.HeightGrips[1], g.ResetButton, g.SpacingGrips[0], g.SpacingGrips[1] };
            var before = new List<Vector3>();
            foreach (var p in parts) before.Add(p.position);
            var ballBefore = g.BallOnBoard;
            var tableTopBottom = Find("TableTop").GetComponent<Renderer>().bounds.min.y;

            Assert.IsTrue(g.SetFit(0.70f, PlayerFit.DefaultLeverSpacing, "test"));
            for (var i = 0; i < parts.Length; i++)
            {
                var d = parts[i].position - before[i];
                Assert.AreEqual(-0.25f, d.y, 1e-4f, $"{parts[i].name} moved down with the table");
                Assert.Less(new Vector2(d.x, d.z).magnitude, 1e-4f, $"{parts[i].name} only moved vertically");
            }
            Assert.AreEqual(0.70f, g.Board.position.y - g.FloorY, 1e-4f);
            Assert.Less((g.BallOnBoard - ballBefore).magnitude, 1e-3f, "the ball stays where it was on the board");
            foreach (var leg in g.Legs)
            {
                var b = leg.GetComponent<Renderer>().bounds;
                Assert.AreEqual(g.FloorY, b.min.y, 1e-3f, "leg on the floor");
                Assert.AreEqual(tableTopBottom - 0.25f, b.max.y, 1e-3f, "leg reaches the table top");
            }

            // Ball physics unchanged at the low table: the board still rolls the ball the way the handle points.
            g.PlaceBall(new Vector2(6f, 3f));
            g.Advance(0.05f);
            var a = g.BallOnBoard;
            g.Roll.Set(0.6f);
            g.Advance(0.35f);
            Assert.Greater(g.BallOnBoard.x - a.x, 0.01f, "rolls right");
        }

        [Test]
        public void NarrowHandles_MoveSymmetrically_KeepTravelAxesAndBoard()
        {
            var pitchTravel = g.PitchLever.TravelWorld;
            var rollTravel = g.RollLever.TravelWorld;
            var bracketOffset = Local(g.SpacingGrips[1]) - Local(g.RollLever.transform);
            var boardScale = g.Board.transform.lossyScale;
            var width = g.Layout.Width;

            g.SetFit(PlayerFit.DefaultTableHeight, 0.15f, "test");
            Assert.AreEqual(-0.15f, Local(g.PitchLever.transform).x, 1e-4f);
            Assert.AreEqual(0.15f, Local(g.RollLever.transform).x, 1e-4f);
            Assert.AreEqual(-0.15f, Local(g.PitchLever.Knob).x, 1e-4f, "knob follows");
            Assert.AreEqual(0.15f, Local(g.RollLever.Knob).x, 1e-4f, "knob follows");
            Assert.Less((Local(g.SpacingGrips[1]) - Local(g.RollLever.transform) - bracketOffset).magnitude, 1e-4f, "base follows its handle");
            Assert.AreEqual(pitchTravel, g.PitchLever.TravelWorld);
            Assert.AreEqual(rollTravel, g.RollLever.TravelWorld);
            Assert.AreEqual(boardScale, g.Board.transform.lossyScale);
            Assert.AreEqual(width, g.Layout.Width);
            Assert.IsFalse(g.PlaqueOnTop, "no room between the handles: the plaque moves to the frame's face");
        }

        // At the lowest table and narrowest spacing, with both handles at every end stop, the knobs and shafts stay
        // clear of the frame, the plaque, each other and the other handle's base.
        [UnityTest]
        public IEnumerator LowNarrowFit_HandlesSwingClear()
        {
            g.SetFit(PlayerFit.MinTableHeight, PlayerFit.MinLeverSpacing, "test");
            var frame = new List<Bounds>();
            foreach (Transform c in Find("Frame"))
                if (c.TryGetComponent<Renderer>(out var r)) frame.Add(r.bounds);
            var plaque = Find("LevelPlaque").GetComponentInChildren<Renderer>().bounds;
            var pitchBase = g.SpacingGrips[0].GetComponent<Renderer>().bounds;
            var rollBase = g.SpacingGrips[1].GetComponent<Renderer>().bounds;
            foreach (var p in new[] { -1f, 0f, 1f })
                foreach (var q in new[] { -1f, 0f, 1f })
                {
                    g.Pitch.Set(p);
                    g.Roll.Set(q);
                    yield return null; // LateUpdate poses the handles
                    var pk = g.PitchLever.Knob.GetComponent<Renderer>().bounds;
                    var rk = g.RollLever.Knob.GetComponent<Renderer>().bounds;
                    foreach (var f in frame)
                    {
                        Assert.IsFalse(pk.Intersects(f), $"pitch knob hits the frame at ({p},{q})");
                        Assert.IsFalse(rk.Intersects(f), $"roll knob hits the frame at ({p},{q})");
                    }
                    Assert.IsFalse(pk.Intersects(rk), $"knobs touch at ({p},{q})");
                    Assert.IsFalse(pk.Intersects(plaque) || rk.Intersects(plaque), $"knob hits the plaque at ({p},{q})");
                    Assert.IsFalse(pk.Intersects(rollBase) || rk.Intersects(pitchBase), $"knob hits the other base at ({p},{q})");
                    Assert.Greater(Vector3.Distance(g.PitchLever.Knob.position, g.RollLever.Knob.position), 0.12f,
                        $"room for two hands at ({p},{q})");
                }
            Assert.IsFalse(plaque.Intersects(pitchBase) || plaque.Intersects(rollBase), "plaque clear of the bases");
        }

        [Test]
        public void GrabZones_FollowTheMovedHandles()
        {
            var oldKnob = g.RollLever.Knob.position;
            g.SetFit(PlayerFit.DefaultTableHeight - 0.2f, 0.18f, "test");
            var pinch = new HandPinch();

            var away = new HandDriver();
            var stale = SimulatedHand.AtKnob(g.transform, g.RollLever.Knob);
            stale.Move(oldKnob - g.RollLever.Knob.position); // where the knob used to be
            stale.Tips = 0.07f;
            away.Step(stale.Sample(pinch), g.Roll, g.RollLever);
            stale.Tips = 0.02f;
            Assert.IsFalse(away.Step(stale.Sample(pinch), g.Roll, g.RollLever), "the old place is empty");

            var driver = new HandDriver();
            var hand = SimulatedHand.AtKnob(g.transform, g.RollLever.Knob);
            hand.Tips = 0.07f;
            driver.Step(hand.Sample(pinch), g.Roll, g.RollLever);
            hand.Tips = 0.02f;
            Assert.IsTrue(driver.Step(hand.Sample(pinch), g.Roll, g.RollLever), "the knob grabs where it is now");
            hand.Move(g.transform.right * 0.04f);
            driver.Step(hand.Sample(pinch), g.Roll, g.RollLever);
            Assert.AreEqual(0.04f / g.Roll.Travel, g.Roll.Value, 0.02f, "same travel per metre as before");
            g.Advance(1f);
            Assert.Less(g.Board.transform.right.y, -0.03f, "right edge down");
        }

        [Test]
        public void AdjustingWhileAHandleIsHeld_LetsGoInPlace_NoJolt_NoRegrab()
        {
            var pinch = new HandPinch();
            var driver = new HandDriver();
            var hand = SimulatedHand.AtKnob(g.transform, g.RollLever.Knob);
            hand.Tips = 0.07f;
            driver.Step(hand.Sample(pinch), g.Roll, g.RollLever);
            hand.Tips = 0.02f;
            driver.Step(hand.Sample(pinch), g.Roll, g.RollLever);
            hand.Move(g.transform.right * 0.04f);
            driver.Step(hand.Sample(pinch), g.Roll, g.RollLever);
            g.Advance(1.5f);
            var value = g.Roll.Value;
            var degrees = g.RollAxis.Degrees;

            g.SetFit(0.8f, 0.22f, "test"); // another hand, the stick or the keys
            Assert.IsFalse(g.Roll.Held, "let go");
            Assert.AreEqual(value, g.Roll.Value, 1e-5f, "the handle stays where it was");
            for (var i = 0; i < 30; i++)
            {
                hand = SimulatedHand.AtKnob(g.transform, g.RollLever.Knob); // the hand is right at the moved knob, still pinched
                hand.Tips = 0.02f;
                Assert.IsFalse(driver.Step(hand.Sample(pinch), g.Roll, g.RollLever), "no regrab without opening");
                g.Advance(0.011f);
                Assert.AreEqual(degrees, g.RollAxis.Degrees, 0.05f, "the board does not jolt");
            }
        }

        [Test]
        public void Fit_SurvivesRestartLevelChangeAndRecenter()
        {
            g.SetFit(0.72f, 0.2f, "test");
            g.Restart();
            g.LoadLevel(3);
            var at = new Vector3(0.4f, 0f, -0.3f);
            g.Place(at, Quaternion.Euler(0, 30f, 0));
            Assert.AreEqual(0.72f, g.Fit.TableHeight, 1e-6f);
            Assert.AreEqual(0.2f, g.Fit.LeverSpacing, 1e-6f);
            Assert.AreEqual(at.y + 0.72f, g.Board.position.y, 1e-4f, "surface height kept after recentering");
            Assert.AreEqual(0.2f, Local(g.RollLever.transform).x, 1e-4f);
            Assert.AreEqual(at.y, g.FloorY, 1e-6f);
            foreach (var leg in g.Legs) Assert.AreEqual(at.y, leg.GetComponent<Renderer>().bounds.min.y, 2e-3f);
            g.Advance(0.2f);
            Assert.AreEqual(RoundState.Playing, g.Round.State, "the ball sits on the start of the new level");
        }

        [Test]
        public void SaveLoadAndReset_ThroughTheStore()
        {
            var store = new MemoryFitStore();
            g.FitStore = store;
            g.SetFit(0.7f, 0.17f, "test");
            g.SaveFit();
            Assert.IsTrue(store.Has);
            g.ResetFit();
            Assert.IsTrue(g.Fit.IsDefault);
            Assert.IsFalse(store.Has, "reset forgets the saved fit");
            Assert.AreEqual(0.36f, Local(g.RollLever.transform).x, 1e-4f);

            store.Save(0.68f, 0.19f);
            g.LoadFit();
            Assert.AreEqual(0.68f, g.Fit.TableHeight, 1e-6f);
            Assert.AreEqual(0.68f, g.Board.position.y - g.FloorY, 1e-4f);
            Assert.AreEqual(0.19f, Local(g.RollLever.transform).x, 1e-4f);
        }

        // Bare hands: pinch a table-height grip and move down; pinch a handle base and move inwards; hold the reset button.
        [Test]
        public void BareHand_PinchHeightGrip_LowersTheTable_SavedOnRelease()
        {
            var store = new MemoryFitStore();
            g.FitStore = store;
            var pinch = new HandPinch();
            var driver = new HandDriver();
            var hand = new SimulatedHand(g.transform, g.HeightGrips[1].position);
            hand.Tips = 0.07f;
            driver.Step(hand.Sample(pinch), g.Roll, g.RollLever, g, 0.011f);
            Assert.IsTrue(driver.HeightGrip.Hover);
            hand.Tips = 0.02f;
            Assert.IsTrue(driver.Step(hand.Sample(pinch), g.Roll, g.RollLever, g, 0.011f).GrabbedFit);
            for (var i = 0; i < 20; i++)
            {
                hand.Move(Vector3.down * 0.01f);
                driver.Step(hand.Sample(pinch), g.Roll, g.RollLever, g, 0.011f);
            }
            Assert.AreEqual(0.75f, g.Fit.TableHeight, 1e-3f, "20 cm down");
            Assert.AreEqual(hand.PinchPoint.y, g.HeightGrips[1].position.y, 1e-3f, "the grip stays in the hand");
            Assert.IsFalse(g.Roll.Held, "the knob was not grabbed");
            hand.Tips = 0.06f;
            var step = driver.Step(hand.Sample(pinch), g.Roll, g.RollLever, g, 0.011f);
            Assert.IsTrue(step.FitReleased);
            if (step.FitReleased) g.SaveFit();
            Assert.AreEqual(0.75f, store.TableHeight, 1e-3f);
        }

        [Test]
        public void BareHand_PinchHandleBase_MovesBothHandlesInwards()
        {
            var pinch = new HandPinch();
            var driver = new HandDriver();
            var hand = new SimulatedHand(g.transform, g.SpacingGrips[1].position + Vector3.up * 0.02f);
            hand.Tips = 0.07f;
            driver.Step(hand.Sample(pinch), g.Roll, g.RollLever, g, 0.011f);
            hand.Tips = 0.02f;
            var step = driver.Step(hand.Sample(pinch), g.Roll, g.RollLever, g, 0.011f);
            Assert.IsTrue(step.GrabbedFit, "the base, not the knob 16 cm above it");
            Assert.IsFalse(g.Roll.Held);
            for (var i = 0; i < 10; i++)
            {
                hand.Move(-g.transform.right * 0.01f);
                driver.Step(hand.Sample(pinch), g.Roll, g.RollLever, g, 0.011f);
            }
            Assert.AreEqual(0.26f, g.Fit.LeverSpacing, 1e-3f);
            Assert.AreEqual(0.26f, Local(g.RollLever.transform).x, 1e-3f);
            Assert.AreEqual(-0.26f, Local(g.PitchLever.transform).x, 1e-3f, "the other handle moves the same amount");
        }

        [Test]
        public void ResetButton_NeedsAOneSecondHold()
        {
            g.SetFit(0.7f, 0.2f, "test");
            var driver = new HandDriver();
            var hand = new SimulatedHand(g.transform, g.ResetButton.position);
            var pinch = new HandPinch();
            var poke = hand.Sample(pinch);
            poke.PokePoint = g.ResetButton.position;
            for (var i = 0; i < 45; i++) Assert.IsFalse(driver.Step(poke, g.Roll, g.RollLever, g, 0.011f).Reset, "0.5 s: not yet");
            var away = poke;
            away.PokePoint += Vector3.back * 0.2f;
            driver.Step(away, g.Roll, g.RollLever, g, 0.011f);
            Assert.AreEqual(0f, driver.ResetProgress, "taking the finger away starts over");
            var resets = 0;
            for (var i = 0; i < 120; i++)
                if (driver.Step(poke, g.Roll, g.RollLever, g, 0.011f).Reset)
                {
                    resets++;
                    g.ResetFit();
                }
            Assert.AreEqual(1, resets, "once per touch");
            Assert.IsTrue(g.Fit.IsDefault);
        }

        [Test]
        public void TwoHands_CannotBothHoldTheTableHeight()
        {
            var pinch = new HandPinch();
            var a = new HandDriver();
            var b = new HandDriver();
            var ha = new SimulatedHand(g.transform, g.HeightGrips[0].position) { Tips = 0.02f };
            var hb = new SimulatedHand(g.transform, g.HeightGrips[1].position) { Tips = 0.07f };
            Assert.IsTrue(a.Step(ha.Sample(pinch), g.Pitch, g.PitchLever, g, 0.011f).GrabbedFit);
            b.Step(hb.Sample(pinch), g.Roll, g.RollLever, g, 0.011f);
            hb.Tips = 0.02f;
            Assert.IsFalse(b.Step(hb.Sample(pinch), g.Roll, g.RollLever, g, 0.011f).GrabbedFit, "held by the other hand");
            hb.Move(Vector3.up * 0.1f);
            b.Step(hb.Sample(pinch), g.Roll, g.RollLever, g, 0.011f);
            Assert.AreEqual(PlayerFit.DefaultTableHeight, g.Fit.TableHeight, 1e-4f, "b moves nothing");
        }
    }
}
