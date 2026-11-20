// Simple feedback: the level plaque on the frame, a message above the board, the goal disc lighting up, a few synthesised sounds (no audio files),
// and a soft rolling sound whose volume follows the ball's speed on the board.
using Labyrinth.Core;
using UnityEngine;

namespace Labyrinth.Game
{
    public sealed class LabyrinthFeedback : MonoBehaviour
    {
        [SerializeField] LabyrinthGame game;
        [SerializeField] TextMesh message;
        [SerializeField] TextMesh plaque;
        [SerializeField] AudioSource sfx;
        [SerializeField] Color goalIdle = new(0.20f, 0.45f, 0.22f);
        [SerializeField] Color goalWon = new(0.45f, 1.0f, 0.45f);
        [SerializeField, Range(0f, 1f)] float rollVolume = 0.35f;

        public string Hint = "Grab the handles with the grip buttons\nLeft handle: tilt forward and back\nRight handle: tilt left and right";
        public string Text => message != null ? message.text : "";
        public string PlaqueText => plaque != null ? plaque.text : "";
        public const float FitMessageSeconds = 2.5f;
        string fitText = "";
        float fitUntil = float.NegativeInfinity;
        public static string LevelLabel(MazeLayout m) => $"Level {m.Number} / {Levels.Count} — {m.Name}";

        AudioClip plunk, chime, click;
        AudioSource roll;
        MaterialPropertyBlock block;
        float fellAt = -1f;

        public void Wire(LabyrinthGame g, TextMesh text, TextMesh levelPlaque, AudioSource source)
        {
            game = g;
            message = text;
            plaque = levelPlaque;
            sfx = source;
        }

        void Awake()
        {
            plunk = SoundSynth.Plunk();
            chime = SoundSynth.Chime();
            click = SoundSynth.Click();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0.7f;
            // Its own object: it follows the ball (on the root it would drag the whole labyrinth along).
            roll = new GameObject("RollSound").AddComponent<AudioSource>();
            roll.transform.SetParent(transform, false);
            roll.clip = SoundSynth.RollNoise();
            roll.loop = true;
            roll.volume = 0f;
            roll.spatialBlend = 0.7f;
            roll.playOnAwake = false;
            game.RoundChanged += OnRound;
            game.LevelLoaded += OnLevel;
            game.FitChanged += OnFit;
            SetGoal(false);
            ShowLevel();
            // TextMesh + dynamic font: put every glyph in the atlas up front, and rebuild the text if the atlas is
            // rebuilt anyway (otherwise later words can come out with wrong glyphs, seen as stray italics).
            if (message.font != null)
            {
                message.font.RequestCharactersInTexture(Glyphs, message.fontSize, FontStyle.Normal);
                message.font.RequestCharactersInTexture(Glyphs, plaque != null ? plaque.fontSize : message.fontSize, FontStyle.Normal);
                Font.textureRebuilt += OnFontRebuilt;
            }
        }

        const string Glyphs = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~…—";

        void OnFontRebuilt(Font f)
        {
            if (message == null || f != message.font) return;
            foreach (var tm in new[] { message, plaque })
            {
                if (tm == null) continue;
                var t = tm.text;
                tm.text = "";
                tm.text = t;
            }
        }

        void Start()
        {
            if (Application.isBatchMode) return;
            roll.Play();
        }

        void OnDestroy()
        {
            if (game != null)
            {
                game.RoundChanged -= OnRound;
                game.LevelLoaded -= OnLevel;
                game.FitChanged -= OnFit;
            }
            Font.textureRebuilt -= OnFontRebuilt;
        }

        public void Grabbed() => sfx.PlayOneShot(click, 0.5f);

        void OnLevel(int index)
        {
            ShowLevel();
            SetGoal(false);
        }

        void ShowLevel()
        {
            if (plaque != null) plaque.text = LevelLabel(game.Layout);
        }

        void OnRound(RoundEvent e)
        {
            switch (e)
            {
                case RoundEvent.Fell:
                    fellAt = Time.time;
                    sfx.PlayOneShot(plunk, 0.9f);
                    break;
                case RoundEvent.Won:
                    sfx.PlayOneShot(chime, 0.8f);
                    SetGoal(true);
                    break;
                case RoundEvent.ResetBall:
                    SetGoal(false);
                    break;
            }
        }

        // While the fit is being changed (and a moment after), the message shows the measures.
        void OnFit(string cause)
        {
            if (cause == "load") return;
            fitText = cause == "reset" ? $"Table and handles reset\n{Measures()}" : Measures();
            fitUntil = Time.unscaledTime + FitMessageSeconds;
        }

        string Measures() =>
            $"Table {game.Fit.TableHeight * 100f:0} cm  ·  handles {game.Fit.LeverSpacing * 200f:0} cm apart";

        void Update()
        {
            if (Time.unscaledTime < fitUntil) message.text = fitText;
            else message.text = game.Round.State switch
            {
                RoundState.Falling => "Oops! Back to the start…",
                RoundState.Won when game.CampaignComplete => "You mastered all five labyrinths!"
                                  + (DesktopInput.Active ? "\nPress R to play again from Level 1" : "\nPress A or X to play again from Level 1"),
                RoundState.Won => $"Level {game.Layout.Number} complete!  {game.Round.WonSeconds:0} s" + FallText()
                                  + $"\nNext: {Levels.Create(game.Campaign.Index + 1).Name}",
                _ => game.EverGrabbed ? "" : (DesktopInput.Active ? "W / S: tilt forward and back\nA / D: tilt left and right\nR: restart" : Hint),
            };
            if (game.Round.State == RoundState.Won)
            {
                var pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 5f);
                SetGoalColor(goalWon * pulse);
            }

            // Rolling sound: only while the ball is on the board and moving.
            var onBoard = game.Round.State == RoundState.Playing && game.BallOnBoard.y > -0.005f;
            var speed = game.Ball.isKinematic ? 0f : game.Ball.linearVelocity.magnitude;
            var target = onBoard ? Mathf.Clamp01(speed / 0.6f) * rollVolume : 0f;
            roll.volume = Mathf.MoveTowards(roll.volume, target, Time.deltaTime * 3f);
            roll.pitch = 0.7f + Mathf.Clamp01(speed / 1.2f) * 0.6f;
            roll.transform.position = game.Ball.position;
        }

        string FallText() => game.Round.Falls switch { 0 => "", 1 => ",  1 fall", var n => $",  {n} falls" };

        void SetGoal(bool won) => SetGoalColor(won ? goalWon : goalIdle);

        void SetGoalColor(Color c)
        {
            var goal = game.GoalRenderer;
            if (goal == null) return;
            block ??= new MaterialPropertyBlock();
            goal.GetPropertyBlock(block);
            block.SetColor("_Color", c);
            block.SetColor("_EmissionColor", c * 0.6f);
            goal.SetPropertyBlock(block);
        }
    }
}
