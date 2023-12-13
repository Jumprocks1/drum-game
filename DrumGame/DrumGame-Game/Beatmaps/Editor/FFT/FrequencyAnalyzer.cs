using System;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using ManagedBass;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;

namespace DrumGame.Game.Beatmaps.Editor.Timing;

// Rename to auto mapper plot
public class AutoMapperPlot : Drawable
{
    public BeatClock Track => Editor.Track;
    public Beatmap Beatmap => Editor.Beatmap;
    public readonly BeatmapEditor Editor;
    public TriggerPlot Plot;
    public AutoMapperPlot(BeatmapEditor editor)
    {
        Editor = editor;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Plot = new(Editor);
        Editor.Display.AuxDisplay.Add(Plot);
        // Watcher = FileWatcher.FromPath<AutoMapperPlotSettings>(Util.Resources.GetAbsolutePath("freqSettings.json"));
        // Watcher.JsonChanged += SettingsChanged;
        // Watcher.ForceTrigger();
    }

    protected override void Dispose(bool isDisposing)
    {
        if (ThreadSafety.IsUpdateThread)
            Editor.Display.AuxDisplay.Remove(Plot, true);
        base.Dispose(isDisposing);
    }
}