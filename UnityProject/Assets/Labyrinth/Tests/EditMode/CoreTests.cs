using Labyrinth.Core;
using NUnit.Framework;
using UnityEngine;

namespace Labyrinth.Tests
{
    public class LeverTests
    {
        [Test]
        public void Grab_DoesNotMoveTheHandle()
        {
            var l = new Lever();
            l.Set(0.3f);
            l.Grab(5f);
            l.Drag(5f);
            Assert.AreEqual(0.3f, l.Value, 1e-5f);
        }

        [Test]
        public void Drag_FollowsHandAlongTravel()
        {
            var l = new Lever { Travel = 0.08f };
            l.Grab(1f);
            l.Drag(1.04f);
            Assert.AreEqual(0.5f, l.Value, 1e-4f);
            l.Drag(0.96f);
            Assert.AreEqual(-0.5f, l.Value, 1e-4f);
        }

        [Test]
        public void Drag_StopsAtEndStops_AndRespondsImmediatelyWhenComingBack()
        {
            var l = new Lever { Travel = 0.08f };
            l.Grab(0f);
            l.Drag(0.30f); // far past the stop
            Assert.AreEqual(1f, l.Value);
            l.Drag(0.26f); // 4 cm back
            Assert.AreEqual(0.5f, l.Value, 1e-4f);
        }

        [Test]
        public void Release_KeepsValue_ByDefault()
        {
            var l = new Lever();
            l.Grab(0f);
            l.Drag(0.04f);
            l.Release();
            for (var i = 0; i < 100; i++) l.Tick(0.02f);
            Assert.AreEqual(0.5f, l.Value, 1e-4f);
        }

        [Test]
        public void Release_ReturnsToNeutral_WhenConfigured()
        {
            var l = new Lever { ReturnToNeutralOnRelease = true };
            l.Grab(0f);
            l.Drag(0.08f);
            l.Release();
            for (var i = 0; i < 100; i++) l.Tick(0.02f);
            Assert.AreEqual(0f, l.Value);
        }

        [Test]
        public void Set_IsIgnoredWhileHeld_AndClamped()
        {
            var l = new Lever();
            l.Set(5f);
            Assert.AreEqual(1f, l.Value);
            l.Grab(0f);
            l.Set(-1f);
            Assert.AreEqual(1f, l.Value);
        }
    }

    public class TiltAxisTests
    {
        [Test]
        public void Target_IsLinearAndLimited()
        {
            var a = new TiltAxis { MaxDegrees = 6f };
            Assert.AreEqual(3f, a.Target(0.5f), 1e-4f);
            Assert.AreEqual(-6f, a.Target(-3f), 1e-4f);
            Assert.AreEqual(0f, a.Target(0f));
        }

        [Test]
        public void Step_ConvergesToTarget_AndNeverExceedsMax()
        {
            var a = new TiltAxis { MaxDegrees = 6f };
            for (var i = 0; i < 200; i++)
            {
                a.Step(1f, 1f / 90f);
                Assert.LessOrEqual(a.Degrees, 6f);
            }
            Assert.AreEqual(6f, a.Degrees, 0.01f);
        }

        [Test]
        public void Step_IsRateLimited()
        {
            var a = new TiltAxis { MaxDegrees = 6f, MaxDegreesPerSecond = 40f };
            var dt = 1f / 90f;
            var last = 0f;
            for (var i = 0; i < 90; i++)
            {
                a.Step(i % 20 < 10 ? 1f : -1f, dt); // violent back-and-forth
                Assert.LessOrEqual(Mathf.Abs(a.Degrees - last), 40f * dt + 1e-4f);
                last = a.Degrees;
            }
        }

        [Test]
        public void ResponseExponent_GivesFinerControlNearNeutral()
        {
            var a = new TiltAxis { MaxDegrees = 6f, ResponseExponent = 2f };
            Assert.AreEqual(1.5f, a.Target(0.5f), 1e-4f);
            Assert.AreEqual(-1.5f, a.Target(-0.5f), 1e-4f);
            Assert.AreEqual(6f, a.Target(1f), 1e-4f);
        }
    }

    public class RoundTests
    {
        [Test]
        public void Fall_PausesThenResets()
        {
            var r = new Round { FallPauseSeconds = 0.5f };
            Assert.AreEqual(RoundEvent.Fell, r.Tick(0.1f, true, false));
            Assert.AreEqual(RoundState.Falling, r.State);
            Assert.AreEqual(1, r.Falls);
            Assert.AreEqual(RoundEvent.None, r.Tick(0.3f, true, false));
            Assert.AreEqual(RoundEvent.ResetBall, r.Tick(0.3f, false, false));
            Assert.AreEqual(RoundState.Playing, r.State);
        }

        [Test]
        public void Goal_Wins_AndHoldsUntilRestart()
        {
            var r = new Round();
            r.Tick(2f, false, false);
            Assert.AreEqual(RoundEvent.Won, r.Tick(0.5f, false, true));
            Assert.AreEqual(2.5f, r.WonSeconds, 1e-4f);
            Assert.AreEqual(RoundEvent.None, r.Tick(1f, true, false)); // a fall after winning is ignored
            Assert.AreEqual(RoundState.Won, r.State);
            r.Restart();
            Assert.AreEqual(RoundState.Playing, r.State);
            Assert.AreEqual(0f, r.Elapsed);
            Assert.AreEqual(0, r.Falls);
        }

        [Test]
        public void FallingBall_CannotWin()
        {
            var r = new Round();
            r.Tick(0.1f, true, false);
            Assert.AreEqual(RoundEvent.None, r.Tick(0.1f, false, true));
            Assert.AreEqual(RoundState.Falling, r.State);
        }
    }
}
