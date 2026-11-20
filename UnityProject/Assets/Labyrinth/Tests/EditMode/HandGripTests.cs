using Labyrinth.Core;
using NUnit.Framework;

namespace Labyrinth.Tests
{
    public class HandGripTests
    {
        [Test]
        public void FreshPressNearKnob_GrabsAndDrags()
        {
            var lever = new Lever { Travel = 0.08f };
            var hand = new HandGrip();
            Assert.IsFalse(hand.Update(lever, true, 0f, 0.05f, 0f));
            Assert.IsTrue(hand.Hover);
            Assert.IsTrue(hand.Update(lever, true, 1f, 0.05f, 0f));
            Assert.IsTrue(lever.Held);
            hand.Update(lever, true, 1f, 0.05f, 0.04f);
            Assert.AreEqual(0.5f, lever.Value, 1e-4f);
        }

        [Test]
        public void PressFarFromKnob_DoesNotGrab()
        {
            var lever = new Lever();
            var hand = new HandGrip();
            hand.Update(lever, true, 1f, 0.3f, 0f);
            Assert.IsFalse(lever.Held);
        }

        [Test]
        public void HoldingGripThenReaching_DoesNotGrab()
        {
            var lever = new Lever();
            var hand = new HandGrip();
            hand.Update(lever, true, 1f, 0.3f, 0f);
            hand.Update(lever, true, 1f, 0.02f, 0f);
            Assert.IsFalse(lever.Held);
        }

        [Test]
        public void GripHysteresis_AndRelease()
        {
            var lever = new Lever();
            var hand = new HandGrip();
            hand.Update(lever, true, 0.6f, 0.02f, 0f);
            hand.Update(lever, true, 0.45f, 0.02f, 0f); // between thresholds: still held
            Assert.IsTrue(lever.Held);
            hand.Update(lever, true, 0.2f, 0.02f, 0f);
            Assert.IsFalse(lever.Held);
        }

        [Test]
        public void LosingTracking_Releases()
        {
            var lever = new Lever();
            var hand = new HandGrip();
            hand.Update(lever, true, 1f, 0.02f, 0f);
            hand.Update(lever, false, 1f, 0.02f, 0f);
            Assert.IsFalse(lever.Held);
        }

        [Test]
        public void TwoHands_OperateTheirHandlesIndependently()
        {
            var pitch = new Lever { Travel = 0.08f };
            var roll = new Lever { Travel = 0.08f };
            var left = new HandGrip();
            var right = new HandGrip();
            left.Update(pitch, true, 1f, 0.02f, 0f);
            right.Update(roll, true, 1f, 0.02f, 0f);
            left.Update(pitch, true, 1f, 0.02f, 0.08f);
            right.Update(roll, true, 1f, 0.02f, -0.04f);
            Assert.AreEqual(1f, pitch.Value, 1e-4f);
            Assert.AreEqual(-0.5f, roll.Value, 1e-4f);
        }

        // Regression (Spike 1): tracking returns with the grip/pinch still closed near the knob. HandGrip used to forget
        // the grip on tracking loss, so the closed grip counted as a fresh press and grabbed again.
        [Test]
        public void TrackingReturnsWithGripClosed_DoesNotRegrab_UntilOpenedAndPressedAgain()
        {
            var lever = new Lever { Travel = 0.08f };
            var hand = new HandGrip();
            hand.Update(lever, true, 1f, 0.02f, 0f);
            hand.Update(lever, true, 1f, 0.02f, 0.04f);
            hand.Update(lever, false, 1f, 0.02f, 0.04f);
            Assert.IsFalse(lever.Held);
            Assert.IsFalse(hand.Update(lever, true, 1f, 0.02f, 0.06f), "closed grip on return must not grab");
            Assert.IsFalse(lever.Held);
            Assert.AreEqual(0.5f, lever.Value, 1e-4f, "the handle stays where it was left");
            hand.Update(lever, true, 0f, 0.02f, 0.06f);
            Assert.IsTrue(hand.Update(lever, true, 1f, 0.02f, 0.06f), "open, then pinch again grabs");
        }

        [Test]
        public void SourceSwitchWhileHeld_Releases_KeepsValue_AndNeedsAFreshPress()
        {
            var lever = new Lever { Travel = 0.08f };
            var hand = new HandGrip();
            Assert.IsTrue(hand.Update(lever, InputSource.Hand, 1f, 0.02f, 0f));
            hand.Update(lever, InputSource.Hand, 1f, 0.02f, -0.04f);
            Assert.AreEqual(-0.5f, lever.Value, 1e-4f);

            // The controller is picked up with its grip already squeezed: release, no grab, the handle stays.
            Assert.IsFalse(hand.Update(lever, InputSource.Controller, 1f, 0.02f, 0.03f));
            Assert.IsFalse(lever.Held);
            Assert.AreEqual(-0.5f, lever.Value, 1e-4f);
            Assert.AreEqual(InputSource.Controller, hand.Source);
            hand.Update(lever, InputSource.Controller, 0.1f, 0.02f, 0.03f);
            Assert.IsTrue(hand.Update(lever, InputSource.Controller, 0.9f, 0.02f, 0.03f));
        }

        [Test]
        public void HandLost_ThenControllerWithGripOpen_GrabsOnPress()
        {
            var lever = new Lever();
            var hand = new HandGrip();
            hand.Update(lever, InputSource.Hand, 1f, 0.02f, 0f);
            hand.Update(lever, InputSource.None, 0f, 0.02f, 0f);
            Assert.IsFalse(lever.Held);
            hand.Update(lever, InputSource.Controller, 0f, 0.02f, 0f);
            Assert.IsTrue(hand.Update(lever, InputSource.Controller, 1f, 0.02f, 0f));
        }

        [Test]
        public void FirstSourceAtStart_DoesNotNeedAnOpenFirst()
        {
            var lever = new Lever();
            Assert.IsTrue(new HandGrip().Update(lever, InputSource.Hand, 1f, 0.02f, 0f));
        }

        [Test]
        public void TrackingLostWhileOpen_ReturnPinchGrabsNormally()
        {
            var lever = new Lever();
            var hand = new HandGrip();
            hand.Update(lever, InputSource.Hand, 0f, 0.02f, 0f);
            hand.Update(lever, InputSource.None, 0f, 0.02f, 0f);
            hand.Update(lever, InputSource.Hand, 0f, 0.02f, 0f); // back, open
            Assert.IsTrue(hand.Update(lever, InputSource.Hand, 1f, 0.02f, 0f));
        }
    }

    public class HandInputTests
    {
        [Test]
        public void Source_ControllerWins_HandOtherwise()
        {
            Assert.AreEqual(InputSource.Controller, SourceChoice.Choose(true, true));
            Assert.AreEqual(InputSource.Controller, SourceChoice.Choose(true, false));
            Assert.AreEqual(InputSource.Hand, SourceChoice.Choose(false, true));
            Assert.AreEqual(InputSource.None, SourceChoice.Choose(false, false));
            Assert.AreEqual(InputSource.None, SourceChoice.Choose(false, true, handsEnabled: false));
        }

        [Test]
        public void PinchStrength_Endpoints_AndClamping()
        {
            var p = new HandPinch { ClosedDistance = 0.015f, OpenDistance = 0.05f };
            Assert.AreEqual(1f, p.Strength(0.005f));
            Assert.AreEqual(1f, p.Strength(0.015f), 1e-5f);
            Assert.AreEqual(0f, p.Strength(0.05f), 1e-5f);
            Assert.AreEqual(0f, p.Strength(0.12f));
            Assert.AreEqual(0.5f, p.Strength(0.0325f), 1e-4f);
        }

        // Thresholds as the player meets them: a pinch closes below ~3.1 cm tip distance, stays closed until ~3.8 cm.
        [Test]
        public void PinchThroughHandGrip_ClosesAndOpensWithHysteresis()
        {
            var p = new HandPinch();
            var lever = new Lever();
            var hand = new HandGrip();
            Assert.AreEqual(0.0308f, p.DistanceAt(hand.PressThreshold), 1e-4f);
            Assert.AreEqual(0.0378f, p.DistanceAt(hand.ReleaseThreshold), 1e-4f);

            bool Step(float tips) => hand.Update(lever, InputSource.Hand, p.Strength(tips), 0.02f, 0f);
            Step(0.07f);
            Step(0.033f);
            Assert.IsFalse(lever.Held, "3.3 cm: not yet a pinch");
            Assert.IsTrue(Step(0.029f), "2.9 cm: pinch grabs");
            Step(0.036f);
            Assert.IsTrue(lever.Held, "3.6 cm: inside the hysteresis band, still held");
            Step(0.040f);
            Assert.IsFalse(lever.Held, "4.0 cm: opened, released");
        }

        // Pinch jitter around the press threshold must not grab twice or flicker the grab.
        [Test]
        public void PinchJitterInsideBand_NoFlicker()
        {
            var p = new HandPinch();
            var lever = new Lever();
            var hand = new HandGrip();
            var grabs = 0;
            hand.Update(lever, InputSource.Hand, 0f, 0.02f, 0f);
            foreach (var d in new[] { 0.029f, 0.032f, 0.030f, 0.034f, 0.031f, 0.0365f, 0.030f })
            {
                if (hand.Update(lever, InputSource.Hand, p.Strength(d), 0.02f, 0f)) grabs++;
                Assert.IsTrue(lever.Held, $"dropped at {d}");
            }
            Assert.AreEqual(1, grabs);
        }
    }
}
