// Small synthesised sound clips (no audio files in the project).
using UnityEngine;

namespace Labyrinth.Game
{
    public static class SoundSynth
    {
        const int Rate = 44100;

        static AudioClip Make(string name, float seconds, System.Func<float, float> f)
        {
            var n = (int)(seconds * Rate);
            var data = new float[n];
            for (var i = 0; i < n; i++) data[i] = Mathf.Clamp(f((float)i / Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // A steel ball dropping into a wooden hole: a low, falling knock.
        public static AudioClip Plunk() => Make("plunk", 0.35f, t =>
        {
            var freq = 180f * Mathf.Exp(-t * 4f) + 70f;
            return Mathf.Sin(2 * Mathf.PI * freq * t) * Mathf.Exp(-t * 14f) * 0.9f;
        });

        // Two bright bell tones.
        public static AudioClip Chime() => Make("chime", 1.2f, t =>
        {
            float Bell(float f, float start) => t < start ? 0f
                : (Mathf.Sin(2 * Mathf.PI * f * (t - start)) + 0.4f * Mathf.Sin(2 * Mathf.PI * f * 2.76f * (t - start))) * Mathf.Exp(-(t - start) * 4f);
            return 0.35f * (Bell(880f, 0f) + Bell(1318.5f, 0.15f));
        });

        // A short wooden click for grabbing a handle.
        public static AudioClip Click() => Make("click", 0.05f, t =>
            (Mathf.Sin(2 * Mathf.PI * 1400f * t) * 0.6f + (Random.value - 0.5f) * 0.6f) * Mathf.Exp(-t * 120f));

        // A seamless loop of low, filtered noise: a ball rolling on wood.
        public static AudioClip RollNoise()
        {
            var state = 0f;
            var rng = new System.Random(7);
            return Make("roll", 2f, t =>
            {
                state += ((float)rng.NextDouble() - 0.5f) * 0.25f;
                state *= 0.985f;
                var fade = Mathf.Min(1f, Mathf.Min(t, 2f - t) * 20f); // soft loop seam
                return state * 1.6f * fade;
            });
        }
    }
}
