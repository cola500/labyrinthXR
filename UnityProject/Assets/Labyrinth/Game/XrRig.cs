// VR input for Quest Touch controllers through UnityEngine.XR.InputDevices (as in Lilla fruktståndet and CampfireVR:
// no XR Interaction Toolkit), plus bare hands through XR Hands (right: Spike 1; left: the fit feature). The left hand works the
// pitch handle, the right hand the roll handle, each through its own HandDriver, so both work at the same time and
// neither can take the other's handle. Per hand a tracked controller always wins; the right handle falls back to the
// tracked bare hand (pinch near the knob; the left hand moves forward/back, the right sideways). Switching source or losing tracking releases
// the handle where it is (HandGrip).
// Fit (PlayerFit): any hand can pinch/grip a brass table-height grip and move up/down, or a handle's wooden base and
// move sideways (both handles follow symmetrically), or hold a fingertip/controller on the reset button for 1 s;
// with controllers also the right stick up/down (table height) and the left stick up/down (handle spacing), at
// Market VR's counter speed. A finished adjustment is saved on the device.
// A/X restarts (the current level; after Level 5, from Level 1). Holding B/Y for HoldToFirstLevel seconds goes back
// to Level 1 (held, so a stray press never throws away progress). On start and after a recenter, the labyrinth is placed in front of the player (yaw only, floor level).
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
        [SerializeField] float holdToFirstLevel = 1.5f;
        [Tooltip("Spike 1: the bare right hand may work the roll handle when the right controller is not tracked.")]
        [SerializeField] bool rightBareHand = true;
        [Tooltip("The bare left hand may work the pitch handle when the left controller is not tracked.")]
        [SerializeField] bool leftBareHand = true;
        [SerializeField] bool showHandMarkers = true;
        [Tooltip("Thumb-tip to index-tip distance (m) below which a pinch closes / above which it opens.")]
        [SerializeField] float pinchPress = 0.031f;
        [SerializeField] float pinchRelease = 0.045f;

        readonly HandDriver left = new();
        readonly HandDriver right = new();
        readonly HandPinch pinch = new(); // HandGrip's default thresholds
        readonly XrHands hands = new();
        readonly HandLog log = new("right");
        readonly HandLog leftLog = new("left");
        readonly List<XRInputSubsystem> subsystems = new();
        Transform leftHand, rightHand;
        HandMarkers rightMarkers, leftMarkers;
        bool placed, primaryWasDown, stickAdjusting;
        float secondaryHeld;
        const float StickDeadZone = 0.25f;

        public Material HandMaterial => handMaterial;

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
            rightMarkers = new HandMarkers(transform, handMaterial, "RightHand");
            leftMarkers = new HandMarkers(transform, handMaterial, "LeftHand");
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems)
            {
                s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                s.trackingOriginUpdated += OnRecenter;
            }
            Application.onBeforeRender += UpdateHead;
            Debug.Log($"[Hands] start: bare left hand={(leftBareHand ? "on" : "off")}, bare right hand={(rightBareHand ? "on" : "off")}, hand subsystem running={hands.Running}, pinch closes <{pinchPress * 100f:0.0} cm, opens >{pinchRelease * 100f:0.0} cm");
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
            pinch.PressDistance = pinchPress;
            pinch.ReleaseDistance = pinchRelease;

            var (restartL, firstL, stickL) = Hand(XRNode.LeftHand, left, game.Pitch, game.PitchLever, leftHand, leftBareHand, leftMarkers, leftLog);
            var (restartR, firstR, stickR) = Hand(XRNode.RightHand, right, game.Roll, game.RollLever, rightHand, rightBareHand, rightMarkers, log);
            Sticks(stickL, stickR);
            ShowFitControls();
            var restart = restartL || restartR;
            if (restart && !primaryWasDown) game.Restart();
            primaryWasDown = restart;
            secondaryHeld = firstL || firstR ? secondaryHeld + Time.deltaTime : 0f;
            if (secondaryHeld >= holdToFirstLevel)
            {
                secondaryHeld = float.NegativeInfinity; // once per hold
                game.RestartFromFirstLevel();
            }
        }

        // Updates one hand; returns its controller's primary (X/A) and secondary (Y/B) buttons and its stick.
        (bool primary, bool secondary, Vector2 stick) Hand(XRNode node, HandDriver driver, Lever lever, LeverView view, Transform visual,
            bool bareHand, HandMarkers markers, HandLog handLog)
        {
            var d = InputDevices.GetDeviceAtXRNode(node);
            var p = Vector3.zero;
            var r = Quaternion.identity;
            var controllerTracked = d.isValid
                                    && d.TryGetFeatureValue(CommonUsages.devicePosition, out p)
                                    && d.TryGetFeatureValue(CommonUsages.deviceRotation, out r)
                                    && (!d.TryGetFeatureValue(CommonUsages.isTracked, out var isTracked) || isTracked);
            d.TryGetFeatureValue(CommonUsages.grip, out var gripValue);
            d.TryGetFeatureValue(CommonUsages.primaryButton, out var primary);
            d.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondary);
            d.TryGetFeatureValue(CommonUsages.primary2DAxis, out var stick);

            Vector3 palm = default, thumb = default, index = default;
            var isLeft = node == XRNode.LeftHand;
            var handTracked = bareHand && !controllerTracked && hands.TryGet(isLeft, out palm, out thumb, out index);
            var source = SourceChoice.Choose(controllerTracked, handTracked, bareHand);

            HandSample sample;
            switch (source)
            {
                case InputSource.Controller:
                    sample = HandSample.Controller(transform.TransformPoint(p + r * handOffset), gripValue);
                    break;
                case InputSource.Hand:
                    sample = HandSample.FromHand(transform.TransformPoint(palm), transform.TransformPoint(thumb), transform.TransformPoint(index), pinch);
                    break;
                default:
                    sample = HandSample.None;
                    break;
            }

            var wasHeld = lever.Held;
            var previous = driver.Grip.Source;
            driver.Grip.GrabRadius = grabRadius;
            var step = driver.Step(sample, lever, view, game, Time.deltaTime);
            if (step.GrabbedKnob || step.GrabbedFit)
            {
                if (step.GrabbedKnob) game.NoteGrab();
                if (game.TryGetComponent<LabyrinthFeedback>(out var feedback)) feedback.Grabbed(); // the rig is not under the labyrinth
                if (source == InputSource.Controller && d.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse) d.SendHapticImpulse(0, 0.35f, 0.04f);
            }
            if (step.FitReleased) game.SaveFit();
            if (step.Reset) game.ResetFit();
            handLog?.Frame(source, previous, sample, step.GrabbedKnob, wasHeld, lever, view);

            view.ShowState(driver.Grip.Hover, lever.Held);
            visual.gameObject.SetActive(source == InputSource.Controller);
            if (source == InputSource.Controller)
            {
                visual.position = sample.GrabPoint;
                visual.rotation = transform.rotation * r;
                visual.localScale = Vector3.one * (lever.Held || driver.HeightGrip.Holding || driver.SpacingGrip.Holding ? 0.040f : 0.048f);
            }
            if (bareHand && markers != null)
                markers.Show(showHandMarkers && source == InputSource.Hand, sample.MovePoint,
                    transform.TransformPoint(thumb), transform.TransformPoint(index), source == InputSource.Hand && sample.Grip >= driver.Grip.PressThreshold);
            var buttons = source == InputSource.Controller;
            return (buttons && primary, buttons && secondary, buttons ? stick : Vector2.zero);
        }

        // Market VR's pattern: a stick moves the value at a fixed speed; saved when the stick is let go.
        void Sticks(Vector2 left, Vector2 right)
        {
            var height = DeadZone(right.y);
            var spacing = DeadZone(left.y);
            if (height != 0f || spacing != 0f)
            {
                var dt = Time.deltaTime * PlayerFit.StickSpeed;
                game.SetFit(game.Fit.TableHeight + height * dt, game.Fit.LeverSpacing + spacing * dt, "stick");
                stickAdjusting = true;
            }
            else if (stickAdjusting)
            {
                stickAdjusting = false;
                game.SaveFit();
            }
        }

        static float DeadZone(float v)
        {
            var a = Mathf.Abs(v);
            return a <= StickDeadZone ? 0f : Mathf.Sign(v) * (a - StickDeadZone) / (1f - StickDeadZone);
        }

        void ShowFitControls()
        {
            var fit = game.Fit;
            var heightHover = left.HeightGrip.Hover || right.HeightGrip.Hover;
            foreach (var g in game.HeightGrips) Highlight(g, heightHover, fit.HeightLever.Held, 0f);
            var spacingHover = left.SpacingGrip.Hover || right.SpacingGrip.Hover;
            foreach (var g in game.SpacingGrips) Highlight(g, spacingHover, fit.SpacingLever.Held, 0f);
            Highlight(game.ResetButton, false, false, Mathf.Max(left.ResetProgress, right.ResetProgress));
        }

        static void Highlight(Transform t, bool hover, bool held, float progress)
        {
            if (t != null && t.TryGetComponent<GripHighlight>(out var h)) h.Show(hover, held, progress);
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

    // Headset evidence for Spike 1, in logcat (tag Unity, prefix "[Hands]"): source changes, grabs and releases with
    // their cause, and once per second while the bare hand holds the handle the palm's spread along the travel
    // (jitter while holding still), the tip distance range (how close the pinch came to opening) and the handle range.
    public sealed class HandLog
    {
        readonly string hand;
        float t, sum, sumSq, min = float.MaxValue, max = float.MinValue, tipMax, valueMin = float.MaxValue, valueMax = float.MinValue;
        int n;

        public HandLog(string hand) => this.hand = hand;

        public void Frame(InputSource source, InputSource previous, in HandSample s, bool grabbed, bool wasHeld, Lever lever, LeverView view)
        {
            if (source != previous) Debug.Log($"[Hands] {hand}: source {previous} -> {source}{(wasHeld ? " (released while held)" : "")}, handle {lever.Value:+0.00;-0.00}");
            if (grabbed)
                Debug.Log($"[Hands] {hand}: grab by {source}, pinch-to-knob {Vector3.Distance(s.GrabPoint, view.Knob.position) * 100f:0.0} cm{(source == InputSource.Hand ? $", tips {s.TipDistance * 100f:0.0} cm" : "")}, handle {lever.Value:+0.00;-0.00}");
            else if (wasHeld && !lever.Held && source == previous)
                Debug.Log($"[Hands] {hand}: release by {source} (grip opened{(source == InputSource.Hand ? $", tips {s.TipDistance * 100f:0.0} cm" : "")}), handle {lever.Value:+0.00;-0.00}");

            if (source != InputSource.Hand || !lever.Held)
            {
                Flush();
                return;
            }
            var c = view.Coordinate(s.MovePoint);
            sum += c;
            sumSq += c * c;
            min = Mathf.Min(min, c);
            max = Mathf.Max(max, c);
            tipMax = Mathf.Max(tipMax, s.TipDistance);
            valueMin = Mathf.Min(valueMin, lever.Value);
            valueMax = Mathf.Max(valueMax, lever.Value);
            n++;
            t += Time.unscaledDeltaTime;
            if (t >= 1f) Flush();
        }

        void Flush()
        {
            if (n >= 10)
            {
                var mean = sum / n;
                var sd = Mathf.Sqrt(Mathf.Max(0f, sumSq / n - mean * mean));
                Debug.Log($"[Hands] {hand} held {t:0.00}s n={n}: palm sd {sd * 1000f:0.0} mm, range {(max - min) * 1000f:0.0} mm, tips max {tipMax * 100f:0.0} cm, handle {valueMin:+0.00;-0.00}..{valueMax:+0.00;-0.00}");
            }
            t = sum = sumSq = tipMax = 0f;
            n = 0;
            min = valueMin = float.MaxValue;
            max = valueMax = float.MinValue;
        }
    }
}
