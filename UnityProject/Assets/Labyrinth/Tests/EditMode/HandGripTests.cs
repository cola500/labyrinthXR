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

        // The pinch is set by its two distances; strength is mapped so HandGrip's thresholds fall exactly on them.
        [Test]
        public void PinchStrength_HitsHandGripThresholds_AtPressAndReleaseDistances()
        {
            var grip = new HandGrip();
            var p = new HandPinch();
            Assert.AreEqual(grip.PressThreshold, p.Strength(p.PressDistance), 1e-4f);
            Assert.AreEqual(grip.ReleaseThreshold, p.Strength(p.ReleaseDistance), 1e-4f);
            Assert.Greater(p.Strength(0f), 0.95f, "tips touching: (nearly) full strength");
            Assert.AreEqual(0f, p.Strength(0.12f));
            Assert.Greater(p.Strength(0.02f), p.Strength(0.03f), "closer tips = stronger pinch");
        }

        // Thresholds as the player meets them (Spike 1 headset: holds peaked at 3.7 cm, so release moved from 3.8 to
        // 4.5 cm): a pinch closes below 3.1 cm and stays closed until 4.5 cm.
        [Test]
        public void PinchThroughHandGrip_ClosesAt31_OpensAt45()
        {
            var p = new HandPinch();
            var lever = new Lever();
            var hand = new HandGrip();
            Assert.AreEqual(0.031f, p.PressDistance, 1e-5f);
            Assert.AreEqual(0.045f, p.ReleaseDistance, 1e-5f);

            bool Step(float tips) => hand.Update(lever, InputSource.Hand, p.Strength(tips), 0.02f, 0f);
            Step(0.07f);
            Step(0.033f);
            Assert.IsFalse(lever.Held, "3.3 cm: not yet a pinch");
            Assert.IsTrue(Step(0.030f), "3.0 cm: pinch grabs");
            Step(0.037f);
            Assert.IsTrue(lever.Held, "3.7 cm (the headset's peak while holding): still held");
            Step(0.044f);
            Assert.IsTrue(lever.Held, "4.4 cm: still held");
            Step(0.046f);
            Assert.IsFalse(lever.Held, "4.6 cm: opened, released");
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
            foreach (var d in new[] { 0.029f, 0.032f, 0.030f, 0.037f, 0.031f, 0.0435f, 0.030f })
            {
                if (hand.Update(lever, InputSource.Hand, p.Strength(d), 0.02f, 0f)) grabs++;
                Assert.IsTrue(lever.Held, $"dropped at {d}");
            }
            Assert.AreEqual(1, grabs);
        }
    }
}
