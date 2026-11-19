// A test "player" that only moves the two handles (never the ball) to follow the maze's route: a velocity controller
// on the ball's board-space position. If it reaches the goal, the maze is physically completable with these handles,
// tilt limits and physics.
using Labyrinth.Core;
using Labyrinth.Game;
using UnityEngine;

namespace Labyrinth.Tests
{
    public sealed class Autopilot
    {
        public float MaxSpeed = 0.22f;    // m/s
        public float ApproachGain = 2.5f; // 1/s: desired speed per metre to the waypoint
        public float VelocityGain = 4f;   // 1/s
        public float Reach = 0.03f;       // m: waypoint reached

        readonly LabyrinthGame game;
        public int Waypoint { get; private set; } = 1;

        public Autopilot(LabyrinthGame g) => game = g;

        public void Reset() => Waypoint = 1;

        public void Step()
        {
            var m = game.Layout;
            if (Waypoint >= m.Route.Count) return;
            var b = game.Board.transform;
            var p = game.BallOnBoard;
            var v = b.InverseTransformDirection(game.Ball.linearVelocity);
            var target = m.ToBoard(m.Route[Waypoint]);
            var to = new Vector2(target.x - p.x, target.z - p.z);
            var last = Waypoint == m.Route.Count - 1;
            if (to.magnitude < Reach && !last)
            {
                Waypoint++;
                Step();
                return;
            }
            var desired = to.normalized * Mathf.Min(MaxSpeed, to.magnitude * ApproachGain);
            var accel = (desired - new Vector2(v.x, v.z)) * VelocityGain;
            // a = 5/7 g sin(tilt) for a rolling ball: lever value = a / (5/7 g sin(max tilt)).
            var full = 5f / 7f * 9.81f * Mathf.Sin(game.PitchAxis.MaxDegrees * Mathf.Deg2Rad);
            game.Roll.Set(accel.x / full);
            game.Pitch.Set(accel.y / full);
        }
    }
}
