using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Interfaces;
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
    static string TimingWorflow => "Recommended timing workflow:\n" +
        $"1. Use {IHasCommand.GetMarkupTooltipNoModify(Command.TimingWizard)} to get BPM and offset close\n" +
        $"2. Manually set the BPM to an integer value using {IHasCommand.GetMarkupTooltipNoModify(Command.ModifyCurrentBPM)}\n" +
        $"3. Open {IHasCommand.GetMarkupTooltipNoModify(Command.OffsetWizard)} and press <command>Compute Offset</c>\n" +
        $"4. Scroll through offset wizard (using arrow keys) to verify that the offset is correct\n";
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

    DrumButton ComputeButton;

    [BackgroundDependencyLoader]
    private void load()
    {
        Add(new Box
        {
            Height = 500,
            RelativeSizeAxes = Axes.X,
            Alpha = 0
        });
        AddFooterButton(ComputeButton = new DrumButton
        {
            AutoSize = true,
            Text = "Compute Offset",
            Action = () =>
            {
                var newOffset = Math.Round(Plot.ComputeBestOffset(), 2);
                var change = newOffset - Beatmap.CurrentTrackStartOffset;
                Editor.PushChange(new OffsetBeatmapChange(Editor, newOffset, Editor.UseYouTubeOffset));
                Track.Seek(Track.AbsoluteTime + change, true);
            },
            MarkupTooltip = $"Calculates the offset with the maximum average on-beat volume.\nThe tempo must be set perfectly before using this.\nCurrently only works for maps with a single tempo event.\n\n{TimingWorflow}"
        });
        ComputeButton.Enabled.Value = false; // gets enabled after we are done loading
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
            if (Beatmap.TempoChanges.Count == 0 || (Beatmap.TempoChanges.Count == 1 && Beatmap.TempoChanges[0].Time == 0))
                ComputeButton.Enabled.Value = true;
        });
    }

    protected override void Update()
    {
        targetBeatText.Text = $"Target Beat: {TargetBeat}";
        if (Plot != null) Plot.Offset = Track.AbsoluteTime;
        base.Update();
    }
}