// The five-level progression, as pure logic. A won level holds for AdvanceSeconds (the "level complete" moment), then
// the next level is due; the last level stays won (the completion state) until the player restarts from Level 1.
// A fall never changes the level (Round handles it).
namespace Labyrinth.Core
{
    public sealed class Campaign
    {
        public float AdvanceSeconds = 2.5f;

        public int Count { get; }
        public int Index { get; private set; }
        public bool IsLast => Index == Count - 1;
        float wonFor;

        public Campaign(int count = Levels.Count) => Count = count;

        // Call every step with whether the current level is won. Returns true once, when the next level is due
        // (Index has then moved on; the caller loads it).
        public bool Tick(float dt, bool won)
        {
            if (!won || IsLast)
            {
                wonFor = 0f;
                return false;
            }
            wonFor += dt;
            if (wonFor < AdvanceSeconds) return false;
            wonFor = 0f;
            Index++;
            return true;
        }

        public bool Completed(bool won) => won && IsLast;

        public void GoTo(int index)
        {
            Index = System.Math.Clamp(index, 0, Count - 1);
            wonFor = 0f;
        }
    }
}
