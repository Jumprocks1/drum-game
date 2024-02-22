
using DrumGame.Game.Components;
using DrumGame.Game.Timing;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
namespace DrumGame.Game.Beatmaps.Editor.Timing;

// helper class that is meant to render a plot of a waveform (aligned based on the current position in a Track)


public record WaveformData
{
    public float[] Data;
    public double ViewSampleRate; // Times per second that we try to update the view
    public double DataSampleRate;
    public float ScalingFactor = 1;
    public double Offset; // used to correct for things like WindowWidth in VolumePlot
}

[LongRunningLoad]
public abstract class WaveformPlot : CompositeDrawable
{
    protected Plot Plot;
    // make sure to subtract WindowWidth when using VolumePlot, make sure to test this works (try it without subtracting first, take some screenshots)
    public double Offset; // center of plot
    // how many plot samples we can see at a given time
    // it may also be good to think about this relative to the sample rate of the plot
    // in many cases, the plot maybe only have samples at 1khz, meaning a wider range of time is displayed across fewer samples
    readonly int DisplayCount;
    WaveformData Data;
    float[] Vertices;
    protected BeatClock Track => Editor.Track;
    protected Beatmap Beatmap => Editor.Beatmap;
    protected readonly BeatmapEditor Editor;
    protected double TargetBeat => Editor.SnapTarget;
    public CircularProgress Progress;
    public WaveformPlot(BeatmapEditor editor, int sampleCount)
    {
        Editor = editor;
        Height = 300;
        DisplayCount = sampleCount;
    }
    Box TargetCursor;

    // make sure to set SampleRate inside here
    protected abstract WaveformData LoadData();

    [BackgroundDependencyLoader]
    private void load()
    {
        Plot = new Plot { Height = Height, RelativeSizeAxes = Axes.X };

        Plot.Vertices = Vertices = new float[DisplayCount];

        Data = LoadData();

        Plot.PathRadius = 1;

        AddInternal(Plot);
        AddInternal(new Box
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Width = 3,
            Colour = Colour4.AliceBlue.MultiplyAlpha(0.5f),
            RelativeSizeAxes = Axes.Y
        });
        AddInternal(TargetCursor = new Box
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.TopCentre,
            Width = 3,
            Colour = Colour4.PaleVioletRed.MultiplyAlpha(0.5f),
            RelativeSizeAxes = Axes.Y,
            RelativePositionAxes = Axes.X
        });
    }


    // current loaded position relative to Data.ViewSampleRate
    protected int loadedViewSample = int.MinValue;
    void UpdatePlot()
    {
        var firstSample = (int)((Offset + Data.Offset) * Data.ViewSampleRate / 1000 - DisplayCount / 2);
        if (firstSample == loadedViewSample) return;
        var h = Height;
        loadedViewSample = firstSample;
        for (var i = 0; i < DisplayCount; i++)
        {
            // only reads first channel
            var t = i + firstSample;
            if (t < 0) Vertices[i] = h;
            else
            {
                var index = (int)(Data.DataSampleRate / Data.ViewSampleRate * t);
                Vertices[i] = index >= Data.Data.Length ? h : (1 - Data.Data[index] * Data.ScalingFactor) * h;
            }
        }
        Plot.Invalidate();
    }

    protected override void Update()
    {
        UpdatePlot();
        var targetTime = Beatmap.MillisecondsFromBeat(TargetBeat);
        var targetSample = (targetTime + Data.Offset) * Data.ViewSampleRate / 1000;
        TargetCursor.X = (float)((targetSample - loadedViewSample) / DisplayCount);
        base.Update();
    }

    protected override void Dispose(bool isDisposing)
    {
        Vertices = null;
        Data = null;
        base.Dispose(isDisposing);
    }
}