using Labyrinth.Core;
using NUnit.Framework;

namespace Labyrinth.Tests
{
    public class PlayerFitTests
    {
        [Test]
        public void Defaults_AreTodaysLayout()
        {
            var f = new PlayerFit();
            Assert.AreEqual(0.95f, f.TableHeight);
            Assert.AreEqual(0.36f, f.LeverSpacing);
            Assert.IsTrue(f.IsDefault);
        }

        [Test]
        public void Set_ClampsToLimits_AndReportsChange()
        {
            var f = new PlayerFit();
            Assert.IsTrue(f.Set(0.2f, 0.9f));
            Assert.AreEqual(PlayerFit.MinTableHeight, f.TableHeight);
            Assert.AreEqual(PlayerFit.MaxLeverSpacing, f.LeverSpacing);
            Assert.IsFalse(f.Set(0.1f, 2f), "already at the limits: no change");
            f.Set(float.NaN, float.NaN);
            Assert.IsTrue(f.IsDefault, "garbage falls back to the defaults");
        }

        [Test]
        public void Reset_GoesBackToDefaults()
        {
            var f = new PlayerFit();
            f.Set(0.7f, 0.2f);
            Assert.IsTrue(f.Reset());
            Assert.IsTrue(f.IsDefault);
        }

        [Test]
        public void Store_SaveLoadClear_AndClampsOldValues()
        {
            var store = new MemoryFitStore();
            var f = new PlayerFit();
            Assert.IsFalse(f.Load(store), "nothing saved yet");
            f.Set(0.72f, 0.18f);
            f.Save(store);
            var g = new PlayerFit();
            Assert.IsTrue(g.Load(store));
            Assert.AreEqual(0.72f, g.TableHeight, 1e-6f);
            Assert.AreEqual(0.18f, g.LeverSpacing, 1e-6f);
            store.TableHeight = 3f; // e.g. saved under wider limits
            var h = new PlayerFit();
            h.Load(store);
            Assert.AreEqual(PlayerFit.MaxTableHeight, h.TableHeight);
            store.Clear();
            Assert.IsFalse(new PlayerFit().Load(store));
        }

        // Dragging the fit levers moves the values 1:1 with the hand, in metres, and stops at the limits.
        [Test]
        public void DragHeightAndSpacing_OneToOneInMetres_SlipAtLimits()
        {
            var f = new PlayerFit();
            var grip = new HandGrip();
            Assert.IsTrue(grip.Update(f.HeightLever, InputSource.Hand, 1f, 0.01f, 1.20f));
            grip.Update(f.HeightLever, InputSource.Hand, 1f, 0.01f, 1.00f); // hand 20 cm lower
            f.FromLevers();
            Assert.AreEqual(0.75f, f.TableHeight, 1e-4f);
            grip.Update(f.HeightLever, InputSource.Hand, 1f, 0.01f, 0.50f); // far below the limit
            f.FromLevers();
            Assert.AreEqual(PlayerFit.MinTableHeight, f.TableHeight, 1e-4f);
            grip.Update(f.HeightLever, InputSource.Hand, 1f, 0.01f, 0.55f); // 5 cm back up: moves at once
            f.FromLevers();
            Assert.AreEqual(PlayerFit.MinTableHeight + 0.05f, f.TableHeight, 1e-4f);

            var s = new HandGrip();
            s.Update(f.SpacingLever, InputSource.Hand, 1f, 0.01f, 0.36f);
            s.Update(f.SpacingLever, InputSource.Hand, 1f, 0.01f, 0.26f); // 10 cm inwards
            f.FromLevers();
            Assert.AreEqual(0.26f, f.LeverSpacing, 1e-4f);
        }

        [Test]
        public void GrabTargets_NearestInReachWins_HeldByOtherIsSkipped()
        {
            var d = new[] { 0.08f, 0.03f, 0.5f };
            var r = new[] { 0.10f, 0.06f, 0.07f };
            Assert.AreEqual(1, GrabTargets.Gate(d, r, new[] { true, true, true }), "0.03/0.06 beats 0.08/0.10");
            Assert.AreEqual(float.PositiveInfinity, d[0]);
            Assert.AreEqual(0.03f, d[1]);

            d = new[] { 0.08f, 0.03f, 0.5f };
            Assert.AreEqual(0, GrabTargets.Gate(d, r, new[] { true, false, true }));
            d = new[] { 0.2f, 0.2f, 0.2f };
            Assert.AreEqual(-1, GrabTargets.Gate(d, r, new[] { true, true, true }));
        }

        // Two hands on one shared lever (the table height): only the hand that grabbed it moves it.
        [Test]
        public void SharedLever_OnlyTheHolderDrags_OtherCannotSteal()
        {
            var lever = new Lever { Travel = 0.25f };
            var a = new HandGrip();
            var b = new HandGrip();
            Assert.IsTrue(a.Update(lever, InputSource.Hand, 1f, 0.01f, 1.0f));
            Assert.IsFalse(b.Update(lever, InputSource.Controller, 1f, 0.01f, 0.5f), "already held: no second grab");
            b.Update(lever, InputSource.Controller, 1f, 0.01f, 0.4f);
            a.Update(lever, InputSource.Hand, 1f, 0.01f, 1.05f);
            Assert.AreEqual(0.2f, lever.Value, 1e-4f, "only a's 5 cm count");
            b.Update(lever, InputSource.Controller, 0f, 0.01f, 0.4f); // b opens: must not release a's grab
            Assert.IsTrue(lever.Held);
            Assert.IsTrue(a.Holding);
            Assert.IsFalse(b.Holding);
        }

        // An adjustment (or lost focus) releases a held handle from outside: the hand must open before grabbing again.
        [Test]
        public void ReleasedFromOutside_NeedsOpenBeforeRegrab()
        {
            var lever = new Lever { Travel = 0.08f };
            var hand = new HandGrip();
            hand.Update(lever, InputSource.Hand, 1f, 0.01f, 0f);
            hand.Update(lever, InputSource.Hand, 1f, 0.01f, 0.04f);
            lever.Release();
            Assert.IsFalse(hand.Update(lever, InputSource.Hand, 1f, 0.01f, 0.06f));
            Assert.IsFalse(lever.Held);
            Assert.IsFalse(hand.Holding);
            Assert.AreEqual(0.5f, lever.Value, 1e-4f, "the handle stays where it was");
            hand.Update(lever, InputSource.Hand, 0f, 0.01f, 0.06f);
            Assert.IsTrue(hand.Update(lever, InputSource.Hand, 1f, 0.01f, 0.06f));
        }
    }
}
