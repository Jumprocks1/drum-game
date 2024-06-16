using System;
using System.Collections.Generic;
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
    IReadOnlyList<MapSetEntry> MapSet;
    public Beatmap Beatmap;
    PreviewLoader PreviewLoader;
    public BeatmapDetailLoader(BeatmapSelectorMap targetMap, PreviewLoader previewLoader)
    {
        PreviewLoader = previewLoader;
        Padding = new MarginPadding(Spacing);
        RelativeSizeAxes = Axes.Both;
        LoadOnUpdateThread(targetMap);
    }

    public void Reload(BeatmapSelectorMap map, Beatmap beatmap)
    {
        LoadOnUpdateThread(map);
        LoadFromBeatmap(beatmap);
    }

    void LoadOnUpdateThread(BeatmapSelectorMap map)
    {
        Util.MapStorage.LoadMetadataCache(); // make sure cache is loaded
        Map = map;
        MapSet = Util.MapStorage.MapSets[map]; // this can still be null maybe
    }

    [Resolved] CancellationToken cancel { get; set; }

    CommandButton SortButton;
    DrumScrollContainer Scroll;
    GridContainer Grid;

    public void RefreshReplays()
    {
        var replaySort = Util.ConfigManager.ReplaySort;
        var replayY = 0f;
        using var context = Util.GetDbContext();
        // Will need to virtualize these eventually
        // It's fine to pull all the replays from the DB, but we shouldn't render them all
        var replays = context.Replays.Where(e => e.MapId == Beatmap.Id);

        Scroll.Clear();
        foreach (var replay in replays.Sort(replaySort.Value))
        {
            Scroll.Add(new ReplayDisplay(replay) { Y = replayY });
            replayY += ReplayDisplay.Height + Spacing;
        }
        // we compute this max size so if we scroll below the replay area, it scrolls the beatmap list instead of the replays
        // if the scroll container has 0 children, it won't consume the scroll wheel, so float.MaxValue is fine
        var maxSize = replayY == 0f ? float.MaxValue : replayY - Spacing;
        Grid.RowDimensions = [
            new(GridSizeMode.AutoSize),
            new(GridSizeMode.Distributed, maxSize: maxSize)
        ];
    }
    // WARNING: this can run on background thread. Update thread access must be completed in the constructor/LoadOnUpdateThread
    public void LoadFromBeatmap(Beatmap beatmap)
    {
        lock (this)
        {
            if (cancel.IsCancellationRequested) return;
            ClearInternal(true);
            Beatmap = beatmap;


            if (cancel.IsCancellationRequested) return;

            // TODO flow height is only calculated once UpdateAfterChildren is run, which causes this entire grid layout to be different for 1 frame
            // It is more noticable in single threaded mode
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


            // Total grid width should be at minimum 1280 - CardWidth - CardMargin * 2 - Spacing * 2 = 
            //                               = 1280 - 520 - 5 * 2 - 4 * 2
            //                               = 742
            // At resolutions wider than 16:9, it may be larger

            var firstColumnWidth = 320;

            flow.Add(SortButton = new CommandButton(Commands.Command.SetReplaySort)
            {
                Width = firstColumnWidth,
                Height = 25
            });
            replaySort.BindValueChanged(SortChanged, true);


            x = 0;
            Scroll = new DrumScrollContainer
            {
                Width = firstColumnWidth,
                RelativeSizeAxes = Axes.Y
            };

            if (MapSet != null && MapSet.Count > 1)
            {
                var selectedIndex = -1;
                for (var i = 0; i < MapSet.Count; i++)
                {
                    if (MapSet[i].MapStoragePath == beatmap.MapStoragePath)
                        selectedIndex = i;
                }
                if (selectedIndex >= 0)
                {
                    var mapSetDisplay = new MapSetDisplay(MapSet, selectedIndex)
                    {
                        X = firstColumnWidth
                    };
                    AddInternal(mapSetDisplay);
                }
            }


            Grid = new()
            {
                RelativeSizeAxes = Axes.Y,
                Content = new Drawable[][] {
                    [ flow ],
                    [Scroll],
                },
                Width = firstColumnWidth
            };
            RefreshReplays();

            AddInternal(Grid);
            Scroll.ScrollbarOverlapsContent = false; // have to set this after adding to the draw tree, not sure why
        }
    }


    // WARNING: this runs on background thread. Update thread access must be completed in the constructor
    [BackgroundDependencyLoader]
    private void load()
    {
        if (Map == null) return;
        LoadFromBeatmap(Util.MapStorage.LoadForQuickMetadata(Map.MapStoragePath));
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

