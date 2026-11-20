// The player's fit of the labyrinth, as pure logic: the board surface's height above the floor and how far each
// handle sits from the centre line. Same pattern as Market VR's counter height (one clamped value with Min/Max/Default,
// applied to the whole assembly at once, changed at a fixed speed from a stick or keys), plus direct manipulation
// for bare hands: each value has a Lever whose travel equals half its range, so a hand dragging it moves the value
// 1:1 in metres and slips at the limits like a handle at its end stop. Saved through an IFitStore.
namespace Labyrinth.Core
{
    public interface IFitStore
    {
        bool TryLoad(out float tableHeight, out float leverSpacing);
        void Save(float tableHeight, float leverSpacing);
        void Clear();
    }

    public sealed class MemoryFitStore : IFitStore
    {
        public bool Has;
        public float TableHeight, LeverSpacing;
        public int Saves;

        public bool TryLoad(out float tableHeight, out float leverSpacing)
        {
            tableHeight = TableHeight;
            leverSpacing = LeverSpacing;
            return Has;
        }

        public void Save(float tableHeight, float leverSpacing)
        {
            Has = true;
            TableHeight = tableHeight;
            LeverSpacing = leverSpacing;
            Saves++;
        }

        public void Clear() => Has = false;
    }

    public sealed class PlayerFit
    {
        // Board surface above the floor. 0.95 m suits a standing adult; 0.60 m a small child (and seated play).
        public const float MinTableHeight = 0.60f, MaxTableHeight = 1.10f, DefaultTableHeight = 0.95f;
        // Each handle's distance from the centre line. 0.36 m (72 cm apart) is the original layout; 0.15 m (30 cm
        // apart, about a child's shoulder width) is the narrowest that keeps both handles clear of each other.
        public const float MinLeverSpacing = 0.15f, MaxLeverSpacing = 0.36f, DefaultLeverSpacing = 0.36f;
        public const float StickSpeed = 0.25f; // m/s at full stick or while a key is held (Market VR's CounterSpeed)

        public float TableHeight { get; private set; } = DefaultTableHeight;
        public float LeverSpacing { get; private set; } = DefaultLeverSpacing;
        public bool IsDefault => TableHeight == DefaultTableHeight && LeverSpacing == DefaultLeverSpacing;

        public readonly Lever HeightLever = new() { Travel = (MaxTableHeight - MinTableHeight) / 2f };
        public readonly Lever SpacingLever = new() { Travel = (MaxLeverSpacing - MinLeverSpacing) / 2f };

        public PlayerFit() => SyncLevers();

        public static float ClampHeight(float h) => Clamp(h, MinTableHeight, MaxTableHeight);
        public static float ClampSpacing(float s) => Clamp(s, MinLeverSpacing, MaxLeverSpacing);

        // Returns whether anything changed.
        public bool Set(float tableHeight, float leverSpacing)
        {
            var h = ClampHeight(tableHeight);
            var s = ClampSpacing(leverSpacing);
            if (float.IsNaN(h)) h = DefaultTableHeight;
            if (float.IsNaN(s)) s = DefaultLeverSpacing;
            var changed = h != TableHeight || s != LeverSpacing;
            TableHeight = h;
            LeverSpacing = s;
            SyncLevers();
            return changed;
        }

        public bool Reset() => Set(DefaultTableHeight, DefaultLeverSpacing);

        // After a hand dragged HeightLever or SpacingLever: take the values from them. Returns whether anything changed.
        public bool FromLevers()
        {
            var h = FromValue(HeightLever.Value, MinTableHeight, MaxTableHeight);
            var s = FromValue(SpacingLever.Value, MinLeverSpacing, MaxLeverSpacing);
            var changed = h != TableHeight || s != LeverSpacing;
            TableHeight = h;
            LeverSpacing = s;
            return changed;
        }

        // Returns whether the store had values (they are clamped to today's limits).
        public bool Load(IFitStore store)
        {
            if (store == null || !store.TryLoad(out var h, out var s)) return false;
            Set(h, s);
            return true;
        }

        public void Save(IFitStore store) => store?.Save(TableHeight, LeverSpacing);

        void SyncLevers()
        {
            HeightLever.Set(ToValue(TableHeight, MinTableHeight, MaxTableHeight));
            SpacingLever.Set(ToValue(LeverSpacing, MinLeverSpacing, MaxLeverSpacing));
        }

        static float ToValue(float x, float min, float max) => (x - min) / (max - min) * 2f - 1f;
        static float FromValue(float v, float min, float max) => min + (v + 1f) * 0.5f * (max - min);
        static float Clamp(float x, float min, float max) => x < min ? min : x > max ? max : x;
    }

    // Which of a hand's grab targets a fresh press may take: the nearest one in reach (distance relative to its own
    // radius), skipping targets another hand holds. The others get an infinite distance for this frame, so one press
    // never grabs two things.
    public static class GrabTargets
    {
        public static int Gate(float[] distances, float[] radii, bool[] available)
        {
            var best = -1;
            var bestScore = float.PositiveInfinity;
            for (var i = 0; i < distances.Length; i++)
            {
                if (!available[i] || distances[i] >= radii[i]) continue;
                var score = distances[i] / radii[i];
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            for (var i = 0; i < distances.Length; i++)
                if (i != best) distances[i] = float.PositiveInfinity;
            return best;
        }
    }
}
