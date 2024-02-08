using System.Linq;
using System.Threading;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Browsers.BeatmapSelection;

// could add LongRunningLoadAttribute but I doubt it would do anything
public class BeatmapDetailLoader : CompositeDrawable
{
    const float Spacing = 4;
    BeatmapSelectorMap Map;
    public Beatmap Beatmap;
    PreviewLoader PreviewLoader;
    public BeatmapDetailLoader(BeatmapSelectorMap targetMap, PreviewLoader previewLoader)
    {
        Map = targetMap;
        PreviewLoader = previewLoader;
    }

    [Resolved] CancellationToken cancel { get; set; }

    CommandButton SortButton;
    DrumScrollContainer Scroll;

    public void LoadFromBeatmap(Beatmap beatmap)
    {
        if (cancel.IsCancellationRequested) return;
        if (Beatmap != null) ClearInternal(true);
        Beatmap = beatmap;


        if (cancel.IsCancellationRequested) return;

        var flow = new FillFlowContainer
        {
            Direction = FillDirection.Vertical,
            Spacing = new osuTK.Vector2(Spacing),
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y
        };

        var x = 0f;

        if (Beatmap.Description != null)
        {
            var textContainer = new TextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
            textContainer.AddParagraph(Beatmap.Description);
            flow.Add(textContainer);
        }
        var dtxLevel = Beatmap.GetDtxLevel();
        if (dtxLevel != null)
        {
            flow.Add(new SpriteText { Text = $"DTX Level: {Beatmap.FormatDtxLevel(dtxLevel)}" });
        }

        Container iconContainer = null;
        void AddIcon<T>() where T : IBeatmapIcon
        {
            if (T.TryConstruct(Beatmap, 30) is Drawable icon)
            {
                iconContainer ??= new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 30
                };
                icon.X = x;
                iconContainer.Add(icon);
                x += 30 + Spacing;
            }
        }
        AddIcon<BandcampIcon>();
        AddIcon<YouTubeIcon>();
        AddIcon<SpotifyIcon>();
        AddIcon<AmazonIcon>();
        AddIcon<OtotoyIcon>();
        if (iconContainer != null) flow.Add(iconContainer);

        var replaySort = Util.ConfigManager.ReplaySort;
        flow.Add(SortButton = new CommandButton(Commands.Command.SetReplaySort)
        {
            Width = 320,
            Height = 25
        });
        replaySort.BindValueChanged(SortChanged, true);


        x = 0;
        Scroll = new DrumScrollContainer
        {
            Width = 320,
            RelativeSizeAxes = Axes.Y
        };

        var replayY = 0f;
        using (var context = Util.GetDbContext())
        {
            // Will need to virtualize these eventually
            // It's fine to pull all the replays from the DB, but we shouldn't render them all
            var replays = context.Replays.Where(e => e.MapId == Beatmap.Id);

            foreach (var replay in replays.Sort(replaySort.Value))
            {
                Scroll.Add(new ReplayDisplay(replay) { Y = replayY });
                replayY += ReplayDisplay.Height + Spacing;
            }
        }

        var grid = new GridContainer { RelativeSizeAxes = Axes.Both };
        var maxSize = replayY == 0f ? float.MaxValue : replayY - Spacing;
        grid.RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.Distributed, maxSize: maxSize) };
        grid.Content = new Drawable[][] {
            new Drawable[] { flow },
            new Drawable[] { Scroll },
        };

        AddInternal(grid);
        Scroll.ScrollbarOverlapsContent = false; // have to set this after adding to the draw tree not, sure why
    }


    [BackgroundDependencyLoader]
    private void load(MapStorage mapStorage)
    {
        if (Map == null) return;
        Padding = new MarginPadding(Spacing);
        RelativeSizeAxes = Axes.Both;
        LoadFromBeatmap(mapStorage.DeserializeMap(Map.MapStoragePath, skipNotes: true));
    }

    void SortChanged(ValueChangedEvent<SortMethod> e)
    {
        if (SortButton != null) SortButton.Text = $"Sort by: {e.NewValue.DisplayName()}";
        // when we virtualize, we will change this to always run on change. The replays will just be loaded once to a List during loading
        if (Scroll != null)
        {
            var replayY = 0f;
            foreach (var child in Scroll.Children.Cast<ReplayDisplay>().Sort(e.NewValue))
            {
                child.Y = replayY;
                replayY += ReplayDisplay.Height + Spacing;
            }
        }
    }

    protected override void LoadAsyncComplete()
    {
        if (cancel.IsCancellationRequested) Dispose();
        else base.LoadAsyncComplete();
    }

    protected override void LoadComplete()
    {
        PreviewLoader.SetTarget(Beatmap);
        base.LoadComplete();
    }

    protected override void Dispose(bool isDisposing)
    {
        Util.ConfigManager.ReplaySort.ValueChanged -= SortChanged;
        Map = null;
        Beatmap = null;
        base.Dispose(isDisposing);
    }
}

