// Reads bare-hand joints from XR Hands (OpenXR Hand Tracking feature). Poses are in tracking space, like
// InputDevices poses, so XrRig transforms them the same way. Without a running hand subsystem (desktop, tests,
// hand tracking off) TryGetRight returns false.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Labyrinth.Game
{
    public sealed class XrHands
    {
        static readonly List<XRHandSubsystem> Found = new();
        XRHandSubsystem subsystem;

        public bool Running => Find() != null;

        public bool TryGetRight(out Vector3 palm, out Vector3 thumbTip, out Vector3 indexTip)
        {
            palm = thumbTip = indexTip = default;
            var s = Find();
            if (s == null) return false;
            var hand = s.rightHand;
            if (!hand.isTracked) return false;
            if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var p)) return false;
            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var t)) return false;
            if (!hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var i)) return false;
            palm = p.position;
            thumbTip = t.position;
            indexTip = i.position;
            return true;
        }

        XRHandSubsystem Find()
        {
            if (subsystem != null && subsystem.running) return subsystem;
            subsystem = null;
            SubsystemManager.GetSubsystems(Found);
            foreach (var s in Found)
                if (s.running) return subsystem = s;
            return null;
        }
    }
}
