// Both bare hands in the real scene, through the same HandDriver path XrRig uses (with the fit controls in play):
// the left hand works only the pitch handle (forward/back), the right only the roll handle (sideways), both at once,
// one hand losing tracking leaves the other in control, a hand returning pinched must re-pinch; and the same for
// Touch controllers, at the default fit and at a low/narrow fit.
using System.Collections;
using Labyrinth.Core;
using Labyrinth.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Labyrinth.Tests
{
    public class TwoHandTests
    {
        LabyrinthGame g;
        HandDriver left, right;
        HandPinch pinch;
        SimulatedHand lh, rh;
        const float Dt = 0.011f;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneLoad.Fresh(x => g = x);
            g.LoadLevel(1);
            g.Pitch.ResetToNeutral();
            g.Roll.ResetToNeutral();
            Hands();
        }

        void Hands()
        {
            left = new HandDriver();
            right = new HandDriver();
            pinch = new HandPinch();
            lh = SimulatedHand.AtKnob(g.transform, g.PitchLever.Knob, left: true);
            rh = SimulatedHand.AtKnob(g.transform, g.RollLever.Knob);
        }

        [TearDown]
        public void Restore() => SceneLoad.Restore();

        HandStep StepLeft(InputSource source = InputSource.Hand) =>
            left.Step(source == InputSource.None ? HandSample.None : lh.Sample(pinch), g.Pitch, g.PitchLever, g, Dt);

        HandStep StepRight(InputSource source = InputSource.Hand) =>
            right.Step(source == InputSource.None ? HandSample.None : rh.Sample(pinch), g.Roll, g.RollLever, g, Dt);

        void PinchBoth()
        {
            lh.Tips = rh.Tips = 0.07f;
            StepLeft();
            StepRight();
            lh.Tips = rh.Tips = 0.02f;
            Assert.IsTrue(StepLeft().GrabbedKnob, "left grabs the pitch knob");
            Assert.IsTrue(StepRight().GrabbedKnob, "right grabs the roll knob");
        }

        void Frames(int n, Vector3 leftMove, Vector3 rightMove, bool leftTracked = true)
        {
            for (var i = 0; i < n; i++)
            {
                lh.Move(leftMove / n);
                rh.Move(rightMove / n);
                StepLeft(leftTracked ? InputSource.Hand : InputSource.None);
                StepRight();
                g.Advance(Dt);
            }
        }

        [Test]
        public void LeftHand_PinchPitchKnob_ForwardTiltsFarEdgeDown_RollUntouched()
        {
            lh.Tips = 0.07f;
            StepLeft();
            lh.Tips = 0.02f;
            Assert.IsTrue(StepLeft().GrabbedKnob);
            for (var i = 0; i < 40; i++)
            {
                lh.Move(g.transform.forward * 0.001f);
                StepLeft();
                g.Advance(Dt);
            }
            Assert.AreEqual(0.04f / g.Pitch.Travel, g.Pitch.Value, 0.02f);
            g.Advance(1f);
            Assert.Less(g.Board.transform.forward.y, -0.03f, "far edge down");
            Assert.AreEqual(0f, g.Roll.Value, "the roll handle is untouched");

            lh.Move(-g.transform.forward * 0.06f);
            StepLeft();
            Assert.Less(g.Pitch.Value, -0.2f, "pulling back tilts towards the player");
        }

        [Test]
        public void EachHand_OnlyItsOwnHandle()
        {
            // The left hand pinching at the roll knob takes nothing; sideways moves of the left hand don't pitch.
            lh = SimulatedHand.AtKnob(g.transform, g.RollLever.Knob, left: true);
            lh.Tips = 0.07f;
            StepLeft();
            lh.Tips = 0.02f;
            Assert.IsFalse(StepLeft().GrabbedKnob);
            Assert.IsFalse(g.Roll.Held || g.Pitch.Held);

            lh = SimulatedHand.AtKnob(g.transform, g.PitchLever.Knob, left: true);
            lh.Tips = 0.07f;
            StepLeft();
            lh.Tips = 0.02f;
            StepLeft();
            lh.Move(g.transform.right * 0.05f);
            StepLeft();
            Assert.AreEqual(0f, g.Pitch.Value, 1e-4f, "sideways is not the pitch handle's axis");
        }

        [Test]
        public void BothHands_AtOnce_TiltBothAxes_AndTheBallFollowsTheDiagonal()
        {
            g.PlaceBall(new Vector2(6f, 3f));
            g.Advance(0.05f);
            var a = g.BallOnBoard;
            PinchBoth();
            Frames(30, g.transform.forward * 0.035f, g.transform.right * 0.035f);
            Assert.Greater(g.Pitch.Value, 0.3f);
            Assert.Greater(g.Roll.Value, 0.3f);
            Assert.IsTrue(g.Pitch.Held && g.Roll.Held, "both held at once");
            g.Advance(0.3f);
            var d = g.BallOnBoard - a;
            Assert.Greater(d.x, 0.01f, $"right: {d}");
            Assert.Greater(d.z, 0.01f, $"away: {d}");
        }

        [Test]
        public void LeftLosesTracking_RightKeepsSteering_LeftReturnsPinched_MustRepinch()
        {
            PinchBoth();
            Frames(20, g.transform.forward * 0.02f, g.transform.right * 0.02f);
            var pitch = g.Pitch.Value;

            Frames(30, Vector3.zero, -g.transform.right * 0.04f, leftTracked: false);
            Assert.IsFalse(g.Pitch.Held, "left released");
            Assert.AreEqual(pitch, g.Pitch.Value, 1e-4f, "pitch stays where it was");
            Assert.IsTrue(g.Roll.Held, "right still holds");
            Assert.Less(g.Roll.Value, 0f, "and keeps steering");

            lh.Move(g.transform.forward * 0.03f); // back elsewhere, still pinched
            Assert.IsFalse(StepLeft().GrabbedKnob, "no grab without opening");
            Frames(10, g.transform.forward * 0.02f, Vector3.zero);
            Assert.AreEqual(pitch, g.Pitch.Value, 1e-4f, "no jump");

            lh = SimulatedHand.AtKnob(g.transform, g.PitchLever.Knob, left: true);
            lh.Tips = 0.07f;
            StepLeft();
            lh.Tips = 0.02f;
            Assert.IsTrue(StepLeft().GrabbedKnob, "open and pinch again");
            Assert.AreEqual(pitch, g.Pitch.Value, 1e-4f, "re-grab does not jump");
        }

        [Test]
        public void BothHands_AtLowNarrowFit_StillGrabTheirKnobs()
        {
            g.SetFit(0.70f, 0.15f, "test");
            Hands();
            PinchBoth();
            Frames(20, g.transform.forward * 0.03f, -g.transform.right * 0.03f);
            Assert.Greater(g.Pitch.Value, 0.25f);
            Assert.Less(g.Roll.Value, -0.25f);
        }

        // Touch controllers: the same knobs with the grip, at the default and at a custom fit.
        [Test]
        public void Controllers_WorkAtDefaultAndCustomFit()
        {
            foreach (var (height, spacing) in new[] { (PlayerFit.DefaultTableHeight, PlayerFit.DefaultLeverSpacing), (0.72f, 0.18f) })
            {
                g.SetFit(height, spacing, "test");
                g.Pitch.ResetToNeutral();
                g.Roll.ResetToNeutral();
                left = new HandDriver();
                right = new HandDriver();
                var lp = g.PitchLever.Knob.position;
                var rp = g.RollLever.Knob.position;
                left.Step(HandSample.Controller(lp, 0f), g.Pitch, g.PitchLever, g, Dt);
                right.Step(HandSample.Controller(rp, 0f), g.Roll, g.RollLever, g, Dt);
                Assert.IsTrue(left.Step(HandSample.Controller(lp, 1f), g.Pitch, g.PitchLever, g, Dt).GrabbedKnob, $"left grip at {height}/{spacing}");
                Assert.IsTrue(right.Step(HandSample.Controller(rp, 1f), g.Roll, g.RollLever, g, Dt).GrabbedKnob, $"right grip at {height}/{spacing}");
                left.Step(HandSample.Controller(lp + g.transform.forward * 0.04f, 1f), g.Pitch, g.PitchLever, g, Dt);
                right.Step(HandSample.Controller(rp - g.transform.right * 0.04f, 1f), g.Roll, g.RollLever, g, Dt);
                Assert.AreEqual(0.04f / g.Pitch.Travel, g.Pitch.Value, 0.01f);
                Assert.AreEqual(-0.04f / g.Roll.Travel, g.Roll.Value, 0.01f);
                left.Step(HandSample.Controller(lp, 0f), g.Pitch, g.PitchLever, g, Dt);
                right.Step(HandSample.Controller(rp, 0f), g.Roll, g.RollLever, g, Dt);
            }
        }
    }
}
