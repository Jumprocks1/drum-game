using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Modals;
using DrumGame.Game.Timing;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;

namespace DrumGame.Game.Beatmaps.Editor.Timing;

public class OffsetWizard : RequestModal
{
    // can't use beat clock since that relies on timing already existing
    public BeatClock Track => Editor.Track;
    public Beatmap Beatmap => Editor.Beatmap;
    public BeatmapEditor Editor;
    public OffsetWizard(BeatmapEditor editor) : base("Offset Wizard")
    {
        Editor = editor;
    }

    public double TargetBeat => Editor.SnapTarget;

    FillFlowContainer body;
    SpriteText targetBeatText;
    VolumePlot Plot;
    double WindowWidth = 0.008;

    [BackgroundDependencyLoader]
    private void load()
    {
        Add(new Box
        {
            Height = 500,
            RelativeSizeAxes = Axes.X,
            Alpha = 0
        });
        Add(body = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            Direction = FillDirection.Vertical,
            AutoSizeAxes = Axes.Y
        });
        body.Add(targetBeatText = new TooltipSpriteText("Current snap position in editor"));
        var windowWidthText = new DrumTextBox
        {
            Text = (WindowWidth * 1000).ToString("0.0"),
            CommitOnFocusLost = true,
            Height = 30,
            Width = 100,
            Margin = new MarginPadding { Top = 3 }
        };
        windowWidthText.OnCommit += (_, __) =>
        {
            if (double.TryParse(windowWidthText.Current.Value, out double value) && value > 0.1 && value < 100 && loadingComplete)
            {
                var newValue = value / 1000;
                if (newValue == WindowWidth) return;
                WindowWidth = newValue;
                LoadNewPlot();
            }
            windowWidthText.Text = (WindowWidth * 1000).ToString("0.0");
        };
        body.Add(windowWidthText);
        LoadNewPlot();
    }

    bool loadingComplete = true;

    void LoadNewPlot()
    {
        if (!loadingComplete) return;
        loadingComplete = false;
        if (Plot != null) body.Remove(Plot, true);
        var progress = new CircularProgress
        {
            Width = 50,
            Height = 50,
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre
        };
        body.Add(progress);
        Plot = new VolumePlot(Editor, WindowWidth, 300)
        {
            RelativeSizeAxes = Axes.X,
            Progress = progress
        };
        LoadComponentAsync(Plot, e =>
        {
            body.Remove(progress, true);
            progress = null;
            body.Add(e);
            loadingComplete = true;
        });
    }

    protected override void Update()
    {
        targetBeatText.Text = $"Target Beat: {TargetBeat}";
        if (Plot != null) Plot.Offset = Track.AbsoluteTime;
        base.Update();
    }
}