using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace DrumGame.Game.Modifiers;

public class HiddenModifier : BeatmapModifier
{
    public new const string Key = "HD";
    public override string Abbreviation => Key;

    public override string FullName => "Hidden";
    public override bool AllowSaving => true;

    public override string MarkupDescription => "Causes notes to fade out as they get closer to the song cursor.\n" +
        $"Notes within {HiddenBeats} beats are entirely hidden.\n" +
        $"Notes fade out over {FadeBeats} beats.";

    float HiddenBeats = 3;
    float FadeBeats = 2;

    public const float StartPosition = -4; // start a little early just in case. Had issue with ghost notes popping out to the left

    public override void Configure() => Util.Palette.Request(new RequestConfig
    {
        Title = $"Configuring {FullName} Modifier",
        Fields = [
            new FloatFieldConfig {
                Label = "Hidden beats",
                MarkupTooltip = "How many beats are completely hidden in front of the cursor.",
                RefN = () => ref HiddenBeats
            },
            new FloatFieldConfig {
                Label = "Fade beats",
                MarkupTooltip = "How many beats are partially hidden.",
                RefN = () => ref FadeBeats
            }
        ],
        // triggering this will update the mod display
        // main thing that changes is the color of the configure button
        OnCommit = _ => TriggerChanged()
    });
    protected override string SerializeData() => $"{HiddenBeats},{FadeBeats}";
    public override void ApplyData(string data)
    {
        var spl = data.Split(',', 2);
        HiddenBeats = float.Parse(spl[0]);
        FadeBeats = float.Parse(spl[1]);
    }


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
                Texture = Util.Resources.GetAssetTexture("fade.png", TextureFilteringMode.Linear),
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