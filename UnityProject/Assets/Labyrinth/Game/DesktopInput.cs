// Development adapter without a headset: W/S (or Up/Down) deflect the pitch handle, A/D (or Left/Right) the roll
// handle while held (released keys ease back to neutral), R or Space restarts (the level; after Level 5, from Level 1),
// 1 or Backspace goes back to Level 1. Fit (as Market VR's counter height): + / - (or PageUp / PageDown) raise and
// lower the table, ] / [ move the handles apart / together, Home resets both; saved when the key is let go. Inactive while an XR device is active
// and in batch mode (tests drive the handles directly).
using Labyrinth.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Labyrinth.Game
{
    public sealed class DesktopInput : MonoBehaviour
    {
        [SerializeField] float rate = 2.5f; // handle deflection per second while a key is held

        public static bool Active => !XRSettings.isDeviceActive && !Application.isBatchMode;

        LabyrinthGame game;
        bool fitting;

        void Awake() => game = GetComponent<LabyrinthGame>();

        void Update()
        {
            if (!Active) return;
            var p = Axis(KeyCode.W, KeyCode.UpArrow, KeyCode.S, KeyCode.DownArrow);
            var r = Axis(KeyCode.D, KeyCode.RightArrow, KeyCode.A, KeyCode.LeftArrow);
            game.Pitch.Set(Mathf.MoveTowards(game.Pitch.Value, p, rate * Time.deltaTime));
            game.Roll.Set(Mathf.MoveTowards(game.Roll.Value, r, rate * Time.deltaTime));
            if (p != 0f || r != 0f) game.NoteGrab();
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space)) game.Restart();
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Backspace)) game.RestartFromFirstLevel();

            var height = Axis(KeyCode.Equals, KeyCode.PageUp, KeyCode.Minus, KeyCode.PageDown)
                         + Axis(KeyCode.KeypadPlus, KeyCode.KeypadPlus, KeyCode.KeypadMinus, KeyCode.KeypadMinus);
            var spacing = Axis(KeyCode.RightBracket, KeyCode.RightBracket, KeyCode.LeftBracket, KeyCode.LeftBracket);
            if (height != 0f || spacing != 0f)
            {
                var step = PlayerFit.StickSpeed * Time.deltaTime;
                game.SetFit(game.Fit.TableHeight + Mathf.Clamp(height, -1f, 1f) * step, game.Fit.LeverSpacing + spacing * step, "keys");
                fitting = true;
            }
            else if (fitting)
            {
                fitting = false;
                game.SaveFit();
            }
            if (Input.GetKeyDown(KeyCode.Home)) game.ResetFit();
        }

        static float Axis(KeyCode pos, KeyCode pos2, KeyCode neg, KeyCode neg2) =>
            (Input.GetKey(pos) || Input.GetKey(pos2) ? 1f : 0f) - (Input.GetKey(neg) || Input.GetKey(neg2) ? 1f : 0f);
    }
}
