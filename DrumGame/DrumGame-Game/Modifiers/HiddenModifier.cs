using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Modifiers;

public class HiddenModifier : BeatmapModifier
{
    public new const string Key = "HD";
    public override string Abbreviation => Key;

    public override string FullName => "Hidden";
    public override bool AllowSaving => true;

    public override string MarkupDescription => "Causes notes to fade out as they get closer to the song cursor.\n" +
        $"Notes within {HiddenBeats} beats are entirely hidden.";

    const float HiddenBeats = 3; // how much to hide 100% past the cursor
    const float FadeBeats = 2; // width of fade

    public const float StartPosition = -4; // start a little early just in case. Had issue with ghost notes popping out to the left

    protected override void ModifyInternal(BeatmapPlayer player)
    {
        if (player.Display is MusicNotationBeatmapDisplay display)
        {
            var hiddenOverlay = new Box
            {
                Colour = Util.Skin.Notation.PlayfieldBackground,
                Depth = -2,
                Y = -6,
                Height = 16,
                X = StartPosition
            };
            var fade = new Sprite
            {
                Colour = Util.Skin.Notation.PlayfieldBackground,
                Texture = Util.Resources.GetAssetTexture("fade.png"),
                Depth = -2,
                Y = -6,
                Height = 16
            };
            void updateAlphas(BeatmapPlayerMode mode)
            {
                var alpha = mode.HasFlag(BeatmapPlayerMode.Playing) ? 1 : 0;
                fade.Alpha = alpha;
                hiddenOverlay.Alpha = alpha;
            }
            player.ModeChanged += updateAlphas;
            updateAlphas(player.Mode);

            display.OnLoadComplete += _ =>
            {
                fade.Width = FadeBeats * display.Font.Spacing;
                var noteContainer = display.NoteContainer;
                noteContainer.Add(hiddenOverlay);
                noteContainer.Add(fade);
            };
            display.OnUpdate += _ =>
            {
                var x = (float)((display.Track.CurrentBeat + HiddenBeats) * display.Font.Spacing);
                hiddenOverlay.Width = x - StartPosition;
                fade.X = x;
            };
        }
    }
}