using Labyrinth.Core;
using NUnit.Framework;

namespace Labyrinth.Tests
{
    public class CampaignTests
    {
        [Test]
        public void StartsAtLevelOne()
        {
            var c = new Campaign();
            Assert.AreEqual(0, c.Index);
            Assert.AreEqual(5, c.Count);
        }

        [Test]
        public void WonLevel_AdvancesOnce_AfterThePause()
        {
            var c = new Campaign { AdvanceSeconds = 2f };
            Assert.IsFalse(c.Tick(1.5f, true));
            Assert.AreEqual(0, c.Index);
            Assert.IsTrue(c.Tick(0.6f, true));
            Assert.AreEqual(1, c.Index);
            Assert.IsFalse(c.Tick(0.1f, true), "only once");
        }

        [Test]
        public void NotWon_NeverAdvances_AndResetsThePause()
        {
            var c = new Campaign { AdvanceSeconds = 2f };
            c.Tick(1.5f, true);
            c.Tick(0.1f, false); // e.g. a restart in between
            Assert.IsFalse(c.Tick(1.0f, true));
            for (var i = 0; i < 100; i++) Assert.IsFalse(c.Tick(0.1f, false));
            Assert.AreEqual(0, c.Index);
        }

        [Test]
        public void LastLevel_StaysWon_AsTheCompletionState()
        {
            var c = new Campaign { AdvanceSeconds = 1f };
            c.GoTo(4);
            Assert.IsTrue(c.IsLast);
            for (var i = 0; i < 50; i++) Assert.IsFalse(c.Tick(0.1f, true));
            Assert.AreEqual(4, c.Index);
            Assert.IsTrue(c.Completed(true));
            Assert.IsFalse(c.Completed(false));
        }

        [Test]
        public void ProgressesThroughAllLevelsInOrder()
        {
            var c = new Campaign { AdvanceSeconds = 1f };
            for (var level = 1; level < 5; level++)
            {
                Assert.IsTrue(c.Tick(1.01f, true));
                Assert.AreEqual(level, c.Index);
            }
            Assert.IsFalse(c.Tick(5f, true));
        }

        [Test]
        public void GoTo_IsClamped()
        {
            var c = new Campaign();
            c.GoTo(9);
            Assert.AreEqual(4, c.Index);
            c.GoTo(-3);
            Assert.AreEqual(0, c.Index);
        }
    }
}
