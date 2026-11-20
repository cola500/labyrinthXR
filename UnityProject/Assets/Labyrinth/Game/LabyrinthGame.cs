// The labyrinth at runtime. Two handles (Pitch, Roll) drive two TiltAxes; the board is a kinematic Rigidbody turned
// with MoveRotation every physics step, and the steel ball is an ordinary dynamic Rigidbody: it is never moved by input,
// only by gravity and its contacts with the tilting board. Round decides falls, resets and the goal from the ball's
// position in board space. VR (XrHands), the desktop keys (DesktopInput) and the tests all go through Pitch/Roll.
// Levels: Campaign holds the level index; LoadLevel swaps only the maze under the board (MazeBuilder), so every level
// shares the same ball, board, handles and physics. A won level advances after Campaign.AdvanceSeconds.
// Board convention: Pitch +1 = handle pushed away from the player = far edge down; Roll +1 = handle pushed right =
// right edge down. So the ball rolls the way the handle is pushed.
using System;
using Labyrinth.Core;
using UnityEngine;

namespace Labyrinth.Game
{
    public sealed class LabyrinthGame : MonoBehaviour
    {
        [Header("Tilt")]
        [Tooltip("Board tilt at a handle's end stop (degrees).")]
        [SerializeField, Range(1f, 15f)] float maxTiltDegrees = 6f;
        [SerializeField, Range(5f, 120f)] float maxTiltDegreesPerSecond = 40f;
        [SerializeField, Range(0f, 0.3f)] float tiltSmoothingSeconds = 0.06f;
        [Tooltip("1 = linear; above 1 gives finer control near neutral.")]
        [SerializeField, Range(1f, 3f)] float responseExponent = 1f;

        [Header("Handles")]
        [Tooltip("Handle swing at its end stop (degrees). Hand travel = shaft length x sin(this).")]
        [SerializeField, Range(10f, 45f)] float leverMaxAngle = 30f;
        [SerializeField] bool returnToNeutralOnRelease;

        [Header("Physics")]
        [SerializeField] float physicsStep = 1f / 90f;
        [SerializeField] float fallPauseSeconds = 0.9f;

        [Header("Scene (set by BoardBuilder)")]
        [SerializeField] Rigidbody board;
        [SerializeField] Rigidbody ball;
        [SerializeField] LeverView pitchLever;
        [SerializeField] LeverView rollLever;
        [SerializeField] float surfaceHeight = 0.95f;
        [SerializeField] MazeMaterials mazeMaterials;
        [Tooltip("The maze currently under the board; the scene holds Level 1, built by SceneSetup.")]
        [SerializeField] GameObject maze;
        [SerializeField] Renderer goal;

        public MazeLayout Layout { get; private set; } = Levels.Create(0);
        public readonly Campaign Campaign = new();
        public readonly Lever Pitch = new();
        public readonly Lever Roll = new();
        public readonly TiltAxis PitchAxis = new();
        public readonly TiltAxis RollAxis = new();
        public readonly Round Round = new();

        public event Action<RoundEvent> RoundChanged;
        public event Action<int> LevelLoaded;

        public Rigidbody Board => board;
        public Rigidbody Ball => ball;
        public LeverView PitchLever => pitchLever;
        public LeverView RollLever => rollLever;
        public float SurfaceHeight => surfaceHeight;
        public bool EverGrabbed { get; private set; }
        public GameObject Maze => maze;
        public Renderer GoalRenderer => goal;
        public bool Won => Round.State == RoundState.Won;
        // All five levels done: the final completion state, until a restart from Level 1.
        public bool CampaignComplete => Campaign.Completed(Won);
        readonly System.Collections.Generic.List<Mesh> runtimeMeshes = new();

        // The ball's centre in board space (metres; y = 0 is the floor's top surface).
        public Vector3 BallOnBoard => board.transform.InverseTransformPoint(ball.position);
        public bool BallFell
        {
            get
            {
                var p = BallOnBoard;
                return p.y < -(FloorMesh.HoleDepth + Layout.BallRadius * 0.5f)
                       || ball.position.y < transform.position.y + surfaceHeight - 1.5f;
            }
        }
        public bool BallInGoal
        {
            get
            {
                var p = BallOnBoard;
                var g = Layout.ToBoard(Layout.Goal);
                return p.y > -0.01f && new Vector2(p.x - g.x, p.z - g.z).magnitude < Layout.GoalRadius;
            }
        }

        public void Wire(Rigidbody boardBody, Rigidbody ballBody, LeverView pitch, LeverView roll, float surface,
                         MazeMaterials materials, GameObject mazeRoot, Renderer goalRenderer)
        {
            mazeMaterials = materials;
            maze = mazeRoot;
            goal = goalRenderer;
            board = boardBody;
            ball = ballBody;
            pitchLever = pitch;
            rollLever = roll;
            surfaceHeight = surface;
        }

        void Awake()
        {
            Time.fixedDeltaTime = physicsStep;
            ApplySettings();
            ball.maxAngularVelocity = 400f; // the default would cap the rolling speed of a 28 mm ball near 1.4 m/s
        }

        void Start() => ResetBall();

        void OnValidate()
        {
            if (Application.isPlaying) ApplySettings();
        }

        public void ApplySettings()
        {
            foreach (var a in new[] { PitchAxis, RollAxis })
            {
                a.MaxDegrees = maxTiltDegrees;
                a.MaxDegreesPerSecond = maxTiltDegreesPerSecond;
                a.SmoothingSeconds = tiltSmoothingSeconds;
                a.ResponseExponent = responseExponent;
            }
            Round.FallPauseSeconds = fallPauseSeconds;
            foreach (var (lever, view) in new[] { (Pitch, pitchLever), (Roll, rollLever) })
            {
                view.MaxAngle = leverMaxAngle;
                lever.Travel = view.ShaftLength * Mathf.Sin(leverMaxAngle * Mathf.Deg2Rad);
                lever.ReturnToNeutralOnRelease = returnToNeutralOnRelease;
            }
        }

        public void NoteGrab() => EverGrabbed = true;

        void FixedUpdate()
        {
            if (Physics.simulationMode == SimulationMode.FixedUpdate) Tick(Time.fixedDeltaTime);
        }

        void LateUpdate()
        {
            pitchLever.SetValue(Pitch.Value);
            rollLever.SetValue(Roll.Value);
        }

        // One physics step of game logic (before the physics simulation of that step).
        public void Tick(float dt)
        {
            Pitch.Tick(dt);
            Roll.Tick(dt);
            PitchAxis.Step(Pitch.Value, dt);
            RollAxis.Step(Roll.Value, dt);
            board.MoveRotation(BoardRotation(PitchAxis.Degrees, RollAxis.Degrees));
            board.MovePosition(transform.TransformPoint(new Vector3(0, surfaceHeight, 0)));

            if (Round.State == RoundState.Won)
            {
                // Settled in the goal: the ball rides along with the board, until the next level is due.
                ball.MovePosition(board.transform.TransformPoint(Layout.ToBoard(Layout.Goal) + Vector3.up * Layout.BallRadius));
                if (Campaign.Tick(dt, true)) LoadLevel(Campaign.Index);
                return;
            }
            var e = Round.Tick(dt, BallFell, BallInGoal);
            if (e == RoundEvent.ResetBall) ResetBall();
            if (e == RoundEvent.Won)
            {
                ball.linearVelocity = Vector3.zero;
                ball.angularVelocity = Vector3.zero;
                ball.isKinematic = true;
            }
            if (e != RoundEvent.None) RoundChanged?.Invoke(e);
        }

        public Quaternion BoardRotation(float pitchDegrees, float rollDegrees) =>
            transform.rotation * Quaternion.Euler(pitchDegrees, 0f, -rollDegrees);

        // A/X, R: after the last level, start again from Level 1; otherwise a fresh attempt at the current level.
        public void Restart()
        {
            if (CampaignComplete)
            {
                LoadLevel(0);
                return;
            }
            Round.Restart();
            Campaign.GoTo(Campaign.Index);
            ResetBall();
            RoundChanged?.Invoke(RoundEvent.ResetBall);
        }

        public void RestartFromFirstLevel() => LoadLevel(0);

        // Replaces the maze with level `index` (0-based) and puts the ball on its start. The old maze is deactivated
        // first, which removes its colliders from the physics scene at once (Destroy only happens at the end of the
        // frame), and the meshes generated for it are destroyed with it. Ball, board, handles and physics stay as they are.
        public void LoadLevel(int index)
        {
            Campaign.GoTo(index);
            Layout = Levels.Create(Campaign.Index);
            if (maze != null)
            {
                maze.SetActive(false);
                maze.transform.SetParent(null, false);
                Destroy(maze);
            }
            foreach (var mesh in runtimeMeshes) Destroy(mesh);
            runtimeMeshes.Clear();
            var view = MazeBuilder.Build(board.transform, Layout, mazeMaterials);
            maze = view.Root;
            goal = view.Goal;
            runtimeMeshes.AddRange(view.GeneratedMeshes);
            Round.Restart();
            ResetBall();
            LevelLoaded?.Invoke(Campaign.Index);
            RoundChanged?.Invoke(RoundEvent.ResetBall);
        }

        public void ResetBall() => PlaceBall(Layout.Start);

        // Puts the ball at rest on the board at a point in cell units (the start, or anywhere for tests).
        public void PlaceBall(Vector2 cell)
        {
            // Place the ball from the board pose this step will have (MoveRotation is applied in the simulation).
            var pose = BoardRotation(PitchAxis.Degrees, RollAxis.Degrees);
            var centre = transform.TransformPoint(new Vector3(0, surfaceHeight, 0));
            var p = centre + pose * (Layout.ToBoard(cell) + Vector3.up * (Layout.BallRadius + 0.001f));
            ball.isKinematic = false;
            ball.position = p;
            ball.transform.position = p;
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            ball.WakeUp();
        }

        // Tests: run seconds of game time with script-driven physics (deterministic and faster than real time).
        public void Advance(float seconds, Action<float> beforeStep = null)
        {
            var dt = Time.fixedDeltaTime;
            for (var t = 0f; t < seconds - 1e-6f; t += dt)
            {
                beforeStep?.Invoke(t);
                Tick(dt);
                Physics.Simulate(dt);
            }
        }

        // Moves the whole labyrinth (e.g. in front of the VR player) and puts the ball back on the start.
        public void Place(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            board.position = transform.TransformPoint(new Vector3(0, surfaceHeight, 0));
            board.rotation = BoardRotation(PitchAxis.Degrees, RollAxis.Degrees);
            board.transform.SetPositionAndRotation(board.position, board.rotation);
            if (Round.State != RoundState.Won) ResetBall();
        }
    }
}
