// The game loop as a pure state machine. Playing -> (ball fell) Falling -> after FallPauseSeconds the ball is reset
// -> Playing. Playing -> (ball in goal) Won, which holds until Restart(). Restart() works in every state.
namespace Labyrinth.Core
{
    public enum RoundState { Playing, Falling, Won }

    public enum RoundEvent { None, Fell, ResetBall, Won }

    public sealed class Round
    {
        public float FallPauseSeconds = 0.9f;

        public RoundState State { get; private set; } = RoundState.Playing;
        public int Falls { get; private set; }
        public float Elapsed { get; private set; }
        public float WonSeconds { get; private set; }
        float fallTimer;

        public RoundEvent Tick(float dt, bool ballFell, bool ballInGoal)
        {
            switch (State)
            {
                case RoundState.Playing:
                    Elapsed += dt;
                    if (ballFell)
                    {
                        State = RoundState.Falling;
                        fallTimer = 0f;
                        Falls++;
                        return RoundEvent.Fell;
                    }
                    if (ballInGoal)
                    {
                        State = RoundState.Won;
                        WonSeconds = Elapsed;
                        return RoundEvent.Won;
                    }
                    return RoundEvent.None;
                case RoundState.Falling:
                    Elapsed += dt;
                    fallTimer += dt;
                    if (fallTimer < FallPauseSeconds) return RoundEvent.None;
                    State = RoundState.Playing;
                    return RoundEvent.ResetBall;
                default:
                    return RoundEvent.None;
            }
        }

        // A new attempt from the start. The caller resets the ball.
        public void Restart()
        {
            State = RoundState.Playing;
            Falls = 0;
            Elapsed = 0f;
            WonSeconds = 0f;
            fallTimer = 0f;
        }
    }
}
