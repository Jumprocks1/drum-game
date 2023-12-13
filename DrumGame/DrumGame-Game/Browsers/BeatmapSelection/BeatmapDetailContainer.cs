using System.Threading;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Components;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Browsers.BeatmapSelection;

public class BeatmapDetailContainer : CompositeDrawable
{
    BeatmapSelectorMap _targetMap;
    public BeatmapSelectorMap TargetMap
    {
        get => _targetMap; set
        {
            if (_targetMap == value) return;
            _targetMap = value;
            TargetChanged();
        }
    }
    public PreviewLoader PreviewLoader;

    public BeatmapDetailContainer()
    {
        AddInternal(PreviewLoader = new());
        Padding = new MarginPadding
        {
            Right = BeatmapCarousel.Width,
            Bottom = ModeSelector.Height,
            Top = BeatmapSelector.HeaderHeight,
        };
        RelativeSizeAxes = Axes.Both;
        AddInternal(new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = DrumColors.DarkBackground.MultiplyAlpha(0.5f)
        });
    }

    CancellationTokenSource cancellation;
    BeatmapDetailLoader Loader;
    public void ReloadTarget(Beatmap beatmap)
    {
        if (Loader.IsLoaded && Loader.Beatmap.Source.Filename == beatmap.Source.Filename)
            Loader.LoadFromBeatmap(beatmap);
    }
    void TargetChanged()
    {
        PreviewLoader.SetPendingTarget(TargetMap);
        if (IsDisposed) return;
        if (cancellation != null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
        else if (Loader != null)
        {
            RemoveInternal(Loader, true);
            Loader = null;
        }
        cancellation = new CancellationTokenSource();
        Loader = new BeatmapDetailLoader(TargetMap, PreviewLoader);
        var token = cancellation.Token;
        // Callback runs on update thread only if not cancelled, and cancels only happen on update thread, so this is safe
        LoadComponentAsync(Loader, e =>
        {
            cancellation.Dispose();
            cancellation = null;
            AddInternal(e);
        }, token);
    }

    protected override void Dispose(bool isDisposing)
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        base.Dispose(isDisposing);
    }
}

