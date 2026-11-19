// VR input for Quest Touch controllers through UnityEngine.XR.InputDevices (as in Lilla fruktståndet and CampfireVR:
// no XR Interaction Toolkit, no Input System). The left hand works the pitch handle, the right hand the roll handle,
// each through its own HandGrip, so both work at the same time and neither can take the other's handle.
// A/X restarts. On start and after a recenter, the labyrinth is placed in front of the player (yaw only, floor level).
// Lost focus/pause releases both handles (the board keeps its tilt, like a real labyrinth).
using System.Collections.Generic;
using Labyrinth.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Labyrinth.Game
{
    public sealed class XrRig : MonoBehaviour
    {
        [SerializeField] LabyrinthGame game;
        [SerializeField] Camera head;
        [SerializeField] Material handMaterial;
        [Tooltip("Grab point relative to the controller's pose (controller space, metres).")]
        [SerializeField] Vector3 handOffset = new(0, -0.015f, 0.03f);
        [Tooltip("Horizontal distance from the eyes to the board's centre when the labyrinth is placed.")]
        [SerializeField] float boardDistance = 0.80f;
        [SerializeField] float grabRadius = 0.10f;

        readonly HandGrip left = new();
        readonly HandGrip right = new();
        readonly List<XRInputSubsystem> subsystems = new();
        Transform leftHand, rightHand;
        bool placed, primaryWasDown;

        public void Wire(LabyrinthGame g, Camera camera, Material hands)
        {
            game = g;
            head = camera;
            handMaterial = hands;
        }

        void Start()
        {
            if (!XRSettings.isDeviceActive)
            {
                enabled = false; // desktop: the camera keeps its authored standing viewpoint
                return;
            }
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // tracking space == world space
            head.transform.SetParent(transform, false);
            head.nearClipPlane = 0.02f;
            leftHand = HandVisual("LeftHand");
            rightHand = HandVisual("RightHand");
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems)
            {
                s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                s.trackingOriginUpdated += OnRecenter;
            }
            Application.onBeforeRender += UpdateHead;
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= UpdateHead;
            foreach (var s in subsystems) s.trackingOriginUpdated -= OnRecenter;
        }

        void OnRecenter(XRInputSubsystem _) => placed = false;

        void OnApplicationFocus(bool focus)
        {
            if (!focus) ReleaseAll();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) ReleaseAll();
        }

        void ReleaseAll()
        {
            game.Pitch.Release();
            game.Roll.Release();
        }

        void Update()
        {
            UpdateHead();
            if (!placed) placed = TryPlace();

            var restart = false;
            restart |= Hand(XRNode.LeftHand, left, game.Pitch, game.PitchLever, leftHand);
            restart |= Hand(XRNode.RightHand, right, game.Roll, game.RollLever, rightHand);
            if (restart && !primaryWasDown) game.Restart();
            primaryWasDown = restart;
        }

        // Updates one hand; returns whether its primary button (X or A) is down.
        bool Hand(XRNode node, HandGrip grip, Lever lever, LeverView view, Transform visual)
        {
            var d = InputDevices.GetDeviceAtXRNode(node);
            var p = Vector3.zero;
            var r = Quaternion.identity;
            var tracked = d.isValid
                          && d.TryGetFeatureValue(CommonUsages.devicePosition, out p)
                          && d.TryGetFeatureValue(CommonUsages.deviceRotation, out r);
            d.TryGetFeatureValue(CommonUsages.grip, out var gripValue);
            d.TryGetFeatureValue(CommonUsages.primaryButton, out var primary);

            var point = transform.TransformPoint(p + r * handOffset);
            visual.gameObject.SetActive(tracked);
            visual.position = point;
            visual.rotation = transform.rotation * r;

            grip.GrabRadius = grabRadius;
            var distance = Vector3.Distance(point, view.Knob.position);
            if (grip.Update(lever, tracked, gripValue, distance, view.Coordinate(point)))
            {
                game.NoteGrab();
                if (game.TryGetComponent<LabyrinthFeedback>(out var feedback)) feedback.Grabbed(); // the rig is not under the labyrinth
                if (d.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse) d.SendHapticImpulse(0, 0.35f, 0.04f);
            }
            view.ShowState(grip.Hover, lever.Held);
            visual.localScale = Vector3.one * (lever.Held ? 0.040f : 0.048f);
            return tracked && primary;
        }

        void UpdateHead()
        {
            var d = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!d.isValid) return;
            if (d.TryGetFeatureValue(CommonUsages.devicePosition, out var p)) head.transform.localPosition = p;
            if (d.TryGetFeatureValue(CommonUsages.deviceRotation, out var r)) head.transform.localRotation = r;
        }

        // The labyrinth in front of the player's eyes, facing them, on the floor.
        bool TryPlace()
        {
            var d = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!d.isValid || !d.TryGetFeatureValue(CommonUsages.devicePosition, out var p) || p == Vector3.zero) return false;
            var eye = head.transform.position;
            var fwd = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var at = new Vector3(eye.x, transform.position.y, eye.z) + fwd * boardDistance;
            game.Place(at, Quaternion.LookRotation(fwd, Vector3.up));
            return true;
        }

        Transform HandVisual(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.048f;
            go.GetComponent<MeshRenderer>().sharedMaterial = handMaterial;
            return go.transform;
        }
    }
}
