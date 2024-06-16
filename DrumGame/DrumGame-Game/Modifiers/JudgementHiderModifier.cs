using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Display.Mania;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Modifiers;

public class JudgementHiderModifier : BeatmapModifier
{
    public new const string Key = "JH";
    public override string Abbreviation => Key;

    public override string FullName => "Judgement Hider";
    public override bool AllowSaving => true;

    public override string MarkupDescription => "Hides all forms of judgement while playing. Your score will still be shown on the results screen.\nThis includes the song cursor, score indicators, and judgement indicators.";

    protected override void ModifyInternal(BeatmapPlayer player)
    {
        player.Display.HideJudgements = true;
        void updateAlphas(BeatmapPlayerMode mode)
        {
            if (player.Display is MusicNotationBeatmapDisplay display)
            {
                if (mode.HasFlag(BeatmapPlayerMode.Playing))
                {
                    display.SnapIndicator = false;
                    display.SongCursorVisible = false;
                }
                if (display.ScoreTopBar != null)
                    display.ScoreTopBar.Alpha = 0;
            }
            else if (player.Display is ManiaBeatmapDisplay maniaDisplay)
            {
                foreach (var child in maniaDisplay.LaneContainer.Children)
                    if (child.Name == "Cursor") child.Alpha = 0;
            }
            if (player.Display.HitErrorDisplay != null)
                player.Display.HitErrorDisplay.Alpha = 0;
        }
        player.ModeChanged += updateAlphas;
        player.OnLoadComplete += _ => updateAlphas(player.Mode);
        updateAlphas(player.Mode);
    }
}