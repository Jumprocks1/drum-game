using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Components;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using DrumGame.Game.Utils;

namespace DrumGame.Game.Beatmaps.Display.Components;

public class ScoreTopBar : CompositeDrawable
{
    public new const float Height = 50;
    readonly HitErrorDisplay HitErrorDisplay;
    readonly BeatmapScorer Scorer;
    readonly ComboMeter Meter;
    public ScoreTopBar(BeatmapScorer scorer)
    {
        Scorer = scorer;
        Scorer.OnScoreEvent += HandleScoreEvent;
        RelativeSizeAxes = Axes.X;
        base.Height = Height;
        AddInternal(HitErrorDisplay = new HitErrorDisplay(BeatmapScorer.HitWindows)
        {
            Width = 200,
            Origin = Anchor.TopCentre,
            Anchor = Anchor.TopCentre,
            Height = 40
        });
        AddInternal(statsText = new StatsText
        {
            X = 3
        });
        AddInternal(scoreText = new ScoreText
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.CentreRight,
            X = -46,
            Y = 20,
            Colour = Util.Skin.Notation.NotationColor,
            Font = FrameworkFont.Regular.With(size: 32)
        });
        AddInternal(Meter = new ComboMeter(Scorer, 40)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
        });
        Scorer.Track.OnSeekCommit += ResetScore;
    }
    void ResetScore(double _)
    {
        HitErrorDisplay.Clear();
    }

    protected override void Dispose(bool isDisposing)
    {
        Scorer.OnScoreEvent -= HandleScoreEvent;
        Scorer.Track.OnSeekCommit -= ResetScore;
        base.Dispose(isDisposing);
    }

    public void HandleScoreChange()
    {
        Meter.UpdateValues(Scorer.MultiplierHandler.MeterLevel, Scorer.MultiplierHandler.Multiplier);
        statsText.Text = $"{Scorer.Accuracy}  {Scorer.ReplayInfo.Combo}x";
        scoreText.Target = Scorer.ReplayInfo.Score;
    }

    public void HandleScoreEvent(ScoreEvent e)
    {
        if (!e.Ignored)
        {
            if (e.HitError.HasValue) // this filters out rolls
            {
                HitErrorDisplay.AddTick((float)e.HitError.Value);
            }
        }
    }

    SpriteText statsText;
    ScoreText scoreText;
}
public class StatsText : SpriteText, IHasTooltip
{
    public LocalisableString TooltipText => "Accuracy and combo";
    public StatsText()
    {
        Colour = Util.Skin.Notation.NotationColor;
        Font = FrameworkFont.Regular.With(size: 24);
    }
}
class ScoreText : SpriteText
{
    public long Target;
    double Loaded;
    protected override void Update()
    {
        var newDisplay = Util.ExpLerp(Loaded, Target, 0.995, Clock.TimeInfo.Elapsed, 0.01);
        if (Loaded != newDisplay)
        {
            var i = (int)newDisplay;
            if ((int)Loaded != i) Text = i.ToString();
            Loaded = newDisplay;
        }
        base.Update();
    }
    public ScoreText()
    {
        Text = "0";
    }
}
