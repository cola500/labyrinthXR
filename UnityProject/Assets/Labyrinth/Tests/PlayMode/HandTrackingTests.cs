// Spike 1: a simulated bare right hand works the real roll handle in the real scene through the same HandDriver /
// HandGrip / Lever path XrRig uses (XR Hands itself is not running off-headset; the samples stand in for it).
using System.Collections;
using Labyrinth.Core;
using Labyrinth.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Labyrinth.Tests
{
    public class HandTrackingTests
    {
        LabyrinthGame g;
        HandDriver driver;
        HandPinch pinch;
        SimulatedHand hand;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneLoad.Fresh(x => g = x);
            g.LoadLevel(1);
            g.Pitch.ResetToNeutral();
            g.Roll.ResetToNeutral();
            driver = new HandDriver();
            pinch = new HandPinch();
            hand = SimulatedHand.AtKnob(g.transform, g.RollLever.Knob);
        }

        [TearDown]
        public void Restore() => SceneLoad.Restore();

        bool Step() => driver.Step(hand.Sample(pinch), g.Roll, g.RollLever);

        // Closes the pinch over several frames like a real hand; returns how many grabs started.
        int ClosePinch()
        {
            var grabs = 0;
            foreach (var tips in new[] { 0.06f, 0.05f, 0.04f, 0.034f, 0.029f, 0.024f, 0.02f })
            {
                hand.Tips = tips;
                if (Step()) grabs++;
            }
            return grabs;
        }

        void MoveRight(float metres, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                hand.Move(g.transform.right * (metres / frames));
                Step();
                g.Advance(0.011f);
            }
        }

        [Test]
        public void PinchAtRollKnob_MoveRight_TiltsRightEdgeDown_OpenKeepsTheHandle()
        {
            hand.Tips = 0.07f;
            Step();
            Assert.IsTrue(driver.Grip.Hover, "open hand at the knob: hover");
            Assert.IsFalse(g.Roll.Held);
            Assert.AreEqual(1, ClosePinch(), "one grab as the pinch closes");
            Assert.IsTrue(g.Roll.Held);
            Assert.AreEqual(0f, g.Roll.Value, 1e-4f, "grabbing does not move the handle");

            MoveRight(0.04f, 40);
            Assert.AreEqual(0.04f / g.Roll.Travel, g.Roll.Value, 0.02f, "the palm's sideways travel drives the handle");
            g.Advance(1f);
            Assert.Less(g.Board.transform.right.y, -0.03f, "right edge down");
            Assert.AreEqual(0f, g.Pitch.Value, "the pitch handle is untouched");

            var held = g.Roll.Value;
            hand.Tips = 0.05f;
            Step();
            Assert.IsFalse(g.Roll.Held, "opening the pinch releases");
            MoveRight(0.03f, 10);
            g.Advance(1f);
            Assert.AreEqual(held, g.Roll.Value, 1e-4f, "released: the handle stays and the open hand no longer moves it");
            Assert.Less(g.Board.transform.right.y, -0.03f, "the board keeps its tilt");
        }

        [Test]
        public void SmallPalmJitterWhileHolding_NoDrop_AndTheHandleBarelyMoves()
        {
            ClosePinch();
            var start = g.Roll.Value;
            var rng = new System.Random(7);
            for (var i = 0; i < 90; i++)
            {
                var j = g.transform.right * ((float)rng.NextDouble() - 0.5f) * 0.002f; // +-1 mm
                hand.Move(j);
                hand.Tips = 0.022f + (float)rng.NextDouble() * 0.020f; // 2.2..4.2 cm: inside the closed band
                Step();
                Assert.IsTrue(g.Roll.Held, $"dropped at frame {i}, tips {hand.Tips}");
                hand.Move(-j);
                g.Advance(0.011f);
            }
            Assert.AreEqual(start, g.Roll.Value, 0.002f / g.Roll.Travel);
        }

        [Test]
        public void TrackingLostWhileHeld_ReturnsPinched_NoJumpNoRegrab_UntilReopened()
        {
            ClosePinch();
            MoveRight(0.02f, 20);
            var held = g.Roll.Value;

            for (var i = 0; i < 10; i++)
            {
                driver.Step(HandSample.None, g.Roll, g.RollLever);
                g.Advance(0.011f);
            }
            Assert.IsFalse(g.Roll.Held, "tracking loss releases");

            hand.Move(g.transform.right * 0.03f); // the hand comes back elsewhere, still pinched
            Assert.IsFalse(Step(), "a hand that returns pinched does not grab");
            MoveRight(0.02f, 10);
            Assert.IsFalse(g.Roll.Held);
            Assert.AreEqual(held, g.Roll.Value, 1e-4f, "no jump");

            hand.Move(-g.transform.right * 0.05f); // back at the knob
            Assert.AreEqual(1, ClosePinch(), "open and pinch again grabs");
            Assert.AreEqual(held, g.Roll.Value, 1e-4f, "re-grab does not jump");
        }

        [Test]
        public void ControllerPickedUpWhileHandHolds_ReleasesAndKeepsTheHandle()
        {
            ClosePinch();
            MoveRight(-0.03f, 20);
            var held = g.Roll.Value;
            Assert.Less(held, -0.2f);

            var knob = g.RollLever.Knob.position;
            Assert.IsFalse(driver.Step(HandSample.Controller(knob, 1f), g.Roll, g.RollLever), "squeezed controller does not grab");
            Assert.IsFalse(g.Roll.Held);
            Assert.AreEqual(held, g.Roll.Value, 1e-4f);
            driver.Step(HandSample.Controller(knob, 0f), g.Roll, g.RollLever);
            Assert.IsTrue(driver.Step(HandSample.Controller(knob, 1f), g.Roll, g.RollLever), "a fresh controller press grabs");
            Assert.AreEqual(held, g.Roll.Value, 1e-4f);

            // And back: the controller is put down, the hand takes over and must pinch fresh.
            Assert.IsFalse(Step());
            Assert.IsFalse(g.Roll.Held);
        }

        [Test]
        public void PinchAwayFromTheKnob_DoesNotGrab()
        {
            hand.Move(g.transform.forward * -0.2f);
            Assert.AreEqual(0, ClosePinch());
            Assert.IsFalse(g.Roll.Held);
        }
    }
}
