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
    }
}
