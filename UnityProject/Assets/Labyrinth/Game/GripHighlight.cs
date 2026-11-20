// The fit controls' look: idle (their material), hover (a hand near enough to grab), held, and for the reset button
// how far a hold has got (0..1, blending to green). Per-renderer colour through a MaterialPropertyBlock.
using UnityEngine;

namespace Labyrinth.Game
{
    public sealed class GripHighlight : MonoBehaviour
    {
        static readonly Color Hover = new(1.0f, 0.78f, 0.35f);
        static readonly Color Held = new(1.0f, 0.92f, 0.55f);
        static readonly Color Done = new(0.35f, 0.85f, 0.40f);

        Renderer rend;
        MaterialPropertyBlock block;
        int lastState = -1;
        float lastProgress = -1f;

        public void Show(bool hover, bool held, float progress = 0f)
        {
            rend ??= GetComponent<Renderer>();
            if (rend == null) return;
            var state = held ? 2 : hover ? 1 : 0;
            progress = Mathf.Clamp01(progress);
            if (state == lastState && Mathf.Abs(progress - lastProgress) < 0.01f) return;
            lastState = state;
            lastProgress = progress;
            if (state == 0 && progress <= 0f)
            {
                rend.SetPropertyBlock(null);
                return;
            }
            block ??= new MaterialPropertyBlock();
            var baseColor = rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_Color") ? rend.sharedMaterial.color : Color.white;
            var c = state == 2 ? Held : state == 1 ? Hover : baseColor;
            block.SetColor("_Color", Color.Lerp(c, Done, progress));
            rend.SetPropertyBlock(block);
        }
    }
}
