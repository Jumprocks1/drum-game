using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DrumGame.Game.API;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Browsers.BeatmapSelection;
using DrumGame.Game.Commands;
using DrumGame.Game.Commands.Requests;
using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using DrumGame.Game.IO;
using DrumGame.Game.Modals;
using DrumGame.Game.Modifiers;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Logging;
using osu.Framework.Utils;

namespace DrumGame.Game.Browsers;

public enum DisplayPreference
{
    Notation,
    Mania
}
public class BeatmapSelectorState
{
    public int _selectedIndex = -1;
    public int SelectedIndex
    {
        get => _selectedIndex; set
        {
            if (_selectedIndex == value) return;
            _selectedIndex = value;
            OnIndexChange?.Invoke(value);
        }
    }
    public event Action<int> OnIndexChange;
    public Bindable<string> SearchBindable;
    public Bindable<string> CollectionBindable;
    public string Collection { get => CollectionBindable.Value; set => CollectionBindable.Value = value; }
    public string Search { get => SearchBindable.Value; set => SearchBindable.Value = value; }
    public string Filename = null;
    public BeatmapOpenMode OpenMode = BeatmapOpenMode.Edit;
    public bool Autoplay = false; // set based on if we start a song with enter key vs midi press


    public List<BeatmapModifier> Modifiers;
    public event Action OnModifiersChange;
    public bool HasModifiers => Modifiers != null && Modifiers.Count > 0;
    public void AddModifier(BeatmapModifier modifier)
    {
        if (modifier == null) return;
        (Modifiers ??= new()).Add(modifier);
        OnModifiersChange();
    }
    public bool HasModifier(BeatmapModifier mod)
    {
        if (Modifiers == null) return false;
        return Modifiers.Any(e => e == mod);
    }
    public bool ToggleModifier(BeatmapModifier modifier) // return true if it was added, false if removed
    {
        if (Modifiers != null)
        {
            for (var i = 0; i < Modifiers.Count; i++)
            {
                if (Modifiers[i] == modifier)
                {
                    Modifiers.RemoveAt(i);
                    OnModifiersChange();
                    return false;
                }
            }
        }
        AddModifier(modifier);
        return true;
    }
    public void ClearModifiers()
    {
        if (HasModifiers)
        {
            Modifiers?.Clear();
            OnModifiersChange();
        }
    }
}
[Cached]
public partial class BeatmapSelector : CompositeDrawable
{
    public const float HeaderHeight = 50;
    public BeatmapOpenMode OpenMode => State.OpenMode;
    readonly Action<string> OnSelect;
    public readonly BeatmapSelectorState State;
    public BeatmapSelector(Action<string> onSelect) : this(onSelect, new BeatmapSelectorState()) { }
    private BeatmapSelector(Action<string> onSelect, BeatmapSelectorState state)
    {
        OnSelect = onSelect;
        State = state;
    }
    // This ref is super sketchy but I kinda like it
    // All it lets us do is pass in null for the state and still have it work
    // If we pass null into the state above then it will be sad without the ref
    // Could probably just use a static default selector state for null but this is fun too
    public BeatmapSelector(Action<string> onSelect, ref BeatmapSelectorState state) : this(onSelect, state ??= new BeatmapSelectorState()) { }
    BeatmapCarousel Carousel;
    [Resolved] public CollectionStorage CollectionStorage { get; private set; }
    [Resolved] public MapStorage MapStorage { get; private set; }
    [Resolved] public FileSystemResources Resources { get; private set; }
    List<BeatmapSelectorMap> Maps;
    void LoadAllMaps()
    {
        Maps = MapStorage.GetMaps().Select(e => new BeatmapSelectorMap(e)).ToList();
    }
    public BeatmapDetailContainer DetailContainer;
    public List<BeatmapSelectorMap> FilteredMaps;
    public List<BeatmapSelectorMap> CollectionMaps;
    string loadedCollection;
    Collection collection;
    BeatmapSelectorMap _targetMap;
    public BeatmapSelectorMap UpdateTargetMap()
    {
        BeatmapSelectorMap newTarget = null;
        if (FilteredMaps != null && State.SelectedIndex >= 0 && State.SelectedIndex < FilteredMaps.Count)
            newTarget = FilteredMaps[State.SelectedIndex];
        if (newTarget != _targetMap)
        {
            _targetMap = newTarget;
            if (DetailContainer != null) DetailContainer.TargetMap = TargetMap;
        }
        return newTarget;
    }
    public void UpdateTargetMap(int _) => UpdateTargetMap();
    public BeatmapSelectorMap TargetMap => UpdateTargetMap();
    BeatmapSelectorMap AimTarget;
    SpriteText filterText;
    SearchTextBox SearchInput;
    public static Colour4 HeaderBackground => DrumColors.DarkActiveBackground.MultiplyAlpha(0.8f);
    [BackgroundDependencyLoader]
    private void load(DrumGameConfigManager config)
    {
        State.SearchBindable = config.GetBindable<string>(DrumGameSetting.BeatmapSearch);
        State.CollectionBindable = config.GetBindable<string>(DrumGameSetting.CurrentCollection);
        State.OnIndexChange += UpdateTargetMap;
        Util.CommandController.RegisterHandlers(this);
        Carousel = new BeatmapCarousel(this);
        LoadAllMaps();
        // we set this so that we can still set a TargetMap even though the filter isn't actually loaded
        // we should probably rework this so that we can just directly set the target map
        FilteredMaps = Maps;
        AddInternal(Carousel);

        AddInternal(new ModeSelector(new ModeOption[]
        {
            new(BeatmapOpenMode.Edit,"Edit", DrumColors.DarkRed,
            "<brightRed>Edit Mode</>\nDefault mode. Works for regular playing, but also allows you to pause and make changes if you find any issues with the map.\n"+
            $"Starting the song with {IHasCommand.GetMarkupTooltip(Command.SelectAutostart)} will start the song immediately (instead of pausing at the start)."),
            new(BeatmapOpenMode.Play,"Play", DrumColors.DarkGreen,
            "<brightGreen>Play Mode</>\nSimilar to edit mode, but does not allow you to make/save changes to the map."),
            new(BeatmapOpenMode.Listen,"Listen", DrumColors.DarkOrange,
            "<brightOrange>Listen Mode</>\nPlays the song and scrolls the sheet music. Disables scoring."),
            new(BeatmapOpenMode.Record,"Record", DrumColors.DarkBlue,
            "<brightBlue>Record Mode</>\nSimilar to edit mode, but also records audio from an active recording device.\nUseful for making videos without external software.\n"+
            $"You can record a replay to a video file with {IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.RecordVideo)}. Requires FFmpeg.") {
                FontScale = 0.9f
            },
        }, State)
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
        });

        var x = 250;
        void AddButton(Command command, IconUsage icon)
        {
            AddInternal(new CommandIconButton(command, icon, 35)
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = x,
                Y = -5
            });
            x += 40;
        }

        AddButton(Command.ShowAvailableCommands, FontAwesome.Solid.Bars);
        AddButton(Command.OpenSettings, FontAwesome.Solid.Cog);
        AddButton(Command.SubmitFeedback, FontAwesome.Brands.Github);
        AddButton(Command.ViewRepositories, FontAwesome.Solid.Server);
        AddButton(Command.ConfigureMapLibraries, FontAwesome.Solid.Folder);
        AddButton(Command.OpenDrumGameDiscord, FontAwesome.Brands.Discord);
        AddInternal(new BeatmapModsButton(this));


        AddInternal(DetailContainer = new());
        // this updates TargetMap, so we have to take care to call it after DetailContainer is constructed
        if (State.Filename != null) State.SelectedIndex = FilteredMaps.FindIndex(e => e.MapStoragePath == State.Filename);

        AddInternal(new Box
        {
            RelativeSizeAxes = Axes.X,
            Height = HeaderHeight,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Colour = HeaderBackground,
        });

        AddInternal(SearchInput = new SearchTextBox(value: State.Search)
        {
            Width = BeatmapCarousel.Width,
            Height = 30,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            BackgroundColour = Colour4.Transparent
        });
        SearchInput.AddHelpButton<BeatmapSelectorMap>("Beatmap Search");
        AddInternal(filterText = new SpriteText
        {
            X = 5 - BeatmapCarousel.Width,
            Y = 32,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopLeft,
            Colour = Colour4.White,
            Font = FrameworkFont.Regular.With(size: 16)
        });
        AddInternal(new CollectionManager(this)
        {
            Width = 200,
            Height = 50,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -BeatmapCarousel.Width,
        });
        AddInternal(new DisplayPreferenceManager
        {
            Width = 100,
            Height = 50,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -BeatmapCarousel.Width - 200,
        });
        AddInternal(new ClockDisplay(HeaderHeight - 4) { Y = 2, X = 3 });
        SearchInput.OnCommit += OnCommit;
        SearchInput.Current.BindValueChanged(OnSearch);
        Util.MapStorage.MapLibraries.ChangedMapLibrary += RefreshSlow;
        UpdateFilter();
    }
    void RefreshSlow(MapLibrary provider)
    {
        var list = (provider == null ? Util.MapStorage.GetMaps() : provider.GetMaps()).ToList();
        var reader = new BulkActionScheduler(i => Util.MapStorage.GetMetadata(list[i]), list.Count);

        if (reader.TotalCount > 0)
        {
            var task = new BackgroundTask(_ => { })
            {
                Name = provider == null ? "Loading metadata" : $"Loading metadata for {provider.FriendlyName}",
                NameTooltip = provider?.Path
            };
            reader.OnComplete = () =>
            {
                task.RunningTask = Task.CompletedTask;
                task.Name = $"Loaded {reader.TotalCount} maps in {reader.Elapsed.TotalMilliseconds:0}ms";
                task.Complete();
                Refresh();
            };
            reader.AfterTick = () => task.UpdateProgress(reader.ProgressRatio);
            reader.Start();
            Util.Palette.NotificationOverlay.Register(task);
        }
        else Refresh();
    }
    protected override void Dispose(bool isDisposing)
    {
        State.OnIndexChange -= UpdateTargetMap;
        Util.MapStorage.MapLibraries.ChangedMapLibrary -= RefreshSlow;
        Util.CommandController.RemoveHandlers(this);
        RemoveTernaryHandlers(); // have to remove handlers
        base.Dispose(isDisposing);
    }
    void OnSearch(ValueChangedEvent<string> e)
    {
        State.Search = e.NewValue;
        UpdateFilter();
    }
    void OnCommit(TextBox _, bool __) => Carousel.Select();
    public void SelectMap(bool autoplay) => SelectMap(TargetMap, autoplay);
    public void SelectMap(BeatmapSelectorMap map, bool autoplay)
    {
        if (map == null) return;
        State.Autoplay = autoplay;
        OnSelect(State.Filename = map.MapStoragePath);
    }
    public void EditMap(BeatmapSelectorMap map)
    {
        State.OpenMode = BeatmapOpenMode.Edit;
        SelectMap(map, false);
    }

    public void CollectionChanged() => ReloadFiltering();

    List<BeatmapSelectorMap> LoadCollectionMaps()
    {
        var target = State.Collection;
        if (target == null) return Maps;
        if (target == loadedCollection) return CollectionMaps;
        collection = CollectionStorage.GetCollection(target);
        if (collection == null)
        {
            State.Collection = null;
            return Maps;
        }
        if (!filterLoaded)
        {
            foreach (var map in Maps) map.LoadFilter();
            filterLoaded = true;
        }
        loadedCollection = State.Collection;
        return CollectionMaps = collection.Apply(Maps, CollectionStorage).ToList();
    }
    bool filterLoaded = false;
    public void UpdateFilter(BeatmapSelectorMap target = null)
    {
        // this is the map we will try pulling our focus to after we finish filtering
        AimTarget = target ??= TargetMap ?? AimTarget;
        IEnumerable<BeatmapSelectorMap> filterRes = LoadCollectionMaps();
        if (!string.IsNullOrWhiteSpace(State.Search))
        {
            if (!filterLoaded)
            {
                foreach (var map in Maps) map.LoadFilter();
                filterLoaded = true;
            }
            filterRes = GenericFilterer<BeatmapSelectorMap>.Filter(filterRes, State.Search);
        }

        int? found = null;
        if (target != null)
        {
            for (var i = 0; i < Maps.Count; i++) Maps[i].Position = -1;
            if (FilteredMaps != null)
                for (var i = 0; i < FilteredMaps.Count; i++) FilteredMaps[i].Position = i;
            FilteredMaps = filterRes.AsList();
            // search criteria:
            //    if the target map is in the new filtered maps, return that
            //    otherwise, look for maps that are in the new filtered maps AND the old filtered maps
            //    take the map that was the closest in the old filtered maps
            //    if none of the maps in the previous old filtered maps are in the new filtered maps, then found should be null
            var targetPosition = target.Position; // old index of old target
            var foundDistance = int.MaxValue;
            for (int i = 0; i < FilteredMaps.Count; i++)
            {
                var map = FilteredMaps[i];
                var pos = map.Position;
                if (target == map) { found = i; break; }
                if (targetPosition >= 0 && pos >= 0 && Math.Abs(pos - targetPosition) < foundDistance)
                {
                    found = i;
                    foundDistance = Math.Abs(pos - targetPosition);
                }
            }
        }
        else FilteredMaps = filterRes.AsList();
        // if we found nothing, just pick a random map
        // we can test this with `d=2` then highlight `2` and type `3`
        found ??= FilteredMaps.Count > 0 ? RNG.Next(FilteredMaps.Count) : null;
        if (found.HasValue)
        {
            State.SelectedIndex = found.Value;
            Carousel.HardPullTarget(State.SelectedIndex);
        }
        Carousel.FilterChanged();
        UpdateTargetMap();
        if (State.Collection != null)
            filterText.Text = $"Showing {FilteredMaps.Count} of {CollectionMaps.Count} maps in {collection.Name ?? loadedCollection} collection ({Maps.Count} total)";
        else
            filterText.Text = $"Showing {FilteredMaps.Count} of {Maps.Count} maps";
    }

    [CommandHandler]
    public void Refresh() => Refresh(null);
    public void Refresh(string targetName, bool skipWriteTimes = false)
    {
        if (IsDisposed) return;
        targetName ??= TargetMap?.MapStoragePath;
        if (!skipWriteTimes) MapStorage.CheckWriteTimes();
        LoadAllMaps();
        FilteredMaps = null;
        filterLoaded = false;
        loadedCollection = null;
        // we have to choose a new target since the old BeatmapSelectorMap references are discarded
        UpdateFilter(targetName != null ? Maps.FirstOrDefault(e => e.MapStoragePath == targetName) : null);
        DetailContainer.PreviewLoader.RetryAudio();
    }


    public enum SyncOptions
    {
        [Display(Name = "Copy to local")]
        Local,
        [Display(Name = "Copy to remote")]
        Remote,
        [Display(Name = "Both")]
        Both,
        [Display(Name = "Score Database")]
        Database,
        [Display(Name = "Ratings (Overwrite)")]
        OverwriteRatings
    }

    [CommandHandler]
    public bool SyncMaps(CommandContext context)
    {
        var target = SSHSync.GetSyncTarget(context);
        if (target == null) return true;
        context.GetItem<SyncOptions>(e =>
        {
            BackgroundTask.Enqueue(new BackgroundTask(_ => sync(context, e))
            {
                Name = $"Syncing {e}"
            });
        }, "Syncing Maps", description: target);
        return true;
    }

    void sync(CommandContext context, SyncOptions syncType)
    {
        var sshSync = SSHSync.From(context);
        if (sshSync == null) return;
        var needsReload = false;
        string jumpTo = null;
        if (syncType == SyncOptions.Database || syncType == SyncOptions.OverwriteRatings)
        {
            var local = Path.GetTempFileName();
            try
            {
                sshSync.CopyRemoteFile("database.db", local);
                using var RemoteDb = new DrumDbContext($"Data Source={local};Mode=ReadOnly;");
                using var localContext = Util.GetDbContext();
                localContext.SyncWith(RemoteDb, syncType);
                RemoteDb.Database.EnsureDeleted();
            }
            finally
            {
                try { File.Delete(local); }
                catch (Exception e) { Logger.Error(e, "Failed to delete temp file"); }
            }
        }
        else
        {
            void SyncFolder(string path, bool gentle = true)
            {
                Logger.Log($"syncing {path}");
                var diff = sshSync.Diff(path, gentle);

                Logger.Log($"found {diff.Local.Count} local files, {diff.Remote.Count} remote files");

                var remote = syncType == SyncOptions.Remote || syncType == SyncOptions.Both;
                var local = syncType == SyncOptions.Local || syncType == SyncOptions.Both;

                if (remote && diff.RemoteMissing.Count > 0)
                {
                    var names = diff.RemoteMissing.Select(e => e.Name);
                    Logger.Log($"copying files({diff.RemoteMissing.Count}): {string.Join(", ", names)} to remote", level: LogLevel.Important);
                    sshSync.CopyToRemote(path, names);
                }

                if (local && diff.LocalMissing.Count > 0)
                {
                    var names = diff.LocalMissing.Select(e => e.Name);
                    Logger.Log($"copying files({diff.LocalMissing.Count}): {string.Join(", ", names)} to local", level: LogLevel.Important);
                    sshSync.CopyToLocal(path, names);
                    if (path == "maps") jumpTo = diff.LocalMissing[RNG.Next(diff.LocalMissing.Count)].Name;
                    needsReload = true;
                }
            }

            SyncFolder(Path.Join("maps"));
            SyncFolder(Path.Join("maps", "audio"));

            MapStorage.CheckWriteTimes();
            var tryCopyImages = new List<string>();
            foreach (var (file, meta) in MapStorage.GetAllMetadata())
                if (meta.Image != null && meta.ImageUrl == null && !MapStorage.Exists(meta.Image))
                    tryCopyImages.Add(Path.GetFileName(meta.Image));
            sshSync.CopyToLocal("maps/images", tryCopyImages);
            foreach (var image in tryCopyImages)
                if (!MapStorage.Exists("images/" + image))
                    Logger.Log($"Missing {image}", level: LogLevel.Error);
        }

        if (needsReload) Schedule(() =>
        {
            Refresh(null, skipWriteTimes: true); // don't need to check write times since we already checked it
            if (jumpTo != null)
            {
                if (string.IsNullOrWhiteSpace(SearchInput.Current.Value))
                    // this will trigger another filter update/partial refresh, but that's okay for now
                    SearchInput.Current.Value = "write>1h";
                Carousel.JumpToMap(jumpTo);
            }
        });
        context.ShowMessage("Sync completed");
    }

    [CommandHandler]
    public bool DeleteMap(CommandContext context)
    {
        var targetMap = GetTargetMap(context);
        var target = targetMap?.MapStoragePath;
        if (target == null) return false;
        Util.Palette.Push(new DeleteRequest(target, e =>
        {
            string newTarget = null;

            var i = State.SelectedIndex + 1;
            if (FilteredMaps.Count > i) newTarget = FilteredMaps[i].MapStoragePath;
            else if (FilteredMaps.Count > 1) newTarget = FilteredMaps[^2].MapStoragePath; // we just deleted the last file, so jump to 2nd to last

            if (e == DeleteOption.Delete)
                MapStorage.Delete(target);
            else if (e == DeleteOption.DeleteMapAndAudio)
            {
                var audio = MapStorage.GetMetadata(target).Audio;
                MapStorage.Delete(target);
                try
                {
                    if (audio != null) MapStorage.Delete(audio);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to delete audio");
                }
            }
            Refresh(newTarget);
        }));
        return true;
    }

    [CommandHandler]
    public bool SetReplaySort(CommandContext context)
    {
        context.GetItem(Utils.Util.ConfigManager.ReplaySort, "Setting Replay Sort Method");
        return true;
    }

    public static Regex YouTubeRegex => new Regex(@"(https:\/\/)?(www\.)?(youtube.com\/watch\?v=|youtu\.be/)(?<v>[0-9a-zA-Z_-]{11})|^(?<v>[0-9a-zA-Z_-]{11})$",
                RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

    bool TryLoadLink(BeatmapSelectorMap target, string link)
    {
        if (link == null) return false;
        var matchers = new (Regex, Action<Beatmap, Match>)[]
        {
            // image:
            // https://img.youtube.com/vi/VIDEO_ID/maxresdefault.jpg
            // https://img.youtube.com/vi/VIDEO_ID/hqdefault.jpg
            // some are in 16:9, others are 4:3, sometimes it's padded with black bars
            (YouTubeRegex, (b,m) => {
                b.YouTubeID = m.Groups[1].Value;
                if (b.ImageUrl == null && b.Image == null) {
                    b.ImageUrl = $"https://img.youtube.com/vi/{b.YouTubeID}/hq720.jpg"; // TODO 720p is really too much for tiny image
                    b.HashImageUrl();
                }
            }),
            (Spotify.SpotifyReference.Regex, (b,m) => {
                var spotifyReference = Spotify.SpotifyReference.From(m);
                b.Spotify = spotifyReference.ShortString;
                if (b.ImageUrl == null && b.Image == null) {
                    if (spotifyReference.Resource == Spotify.SpotifyResource.Track) {
                        Task.Run(async () => {
                            var track = await Spotify.GetTrack(b.Spotify);
                            Schedule(() => {
                                MutateBeatmap(target, b => {
                                    b.ImageUrl = track.Album.GoodImage.Url;
                                    b.HashImageUrl();
                                });
                            });
                        });
                    }
                }
            }),
            (new Regex(@"(https:\/\/)?(www\.)?amazon.com(\/.*)?\/(d|g)p(\/product)?\/(?<a>[0-9a-zA-Z]{10})",
                RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture),
                (b,m) => b.AmazonASIN = m.Groups[1].Value),
            (new Regex(@"^https:\/\/m.media-amazon.com\/images\/I\/(?<a>([0-9a-zA-Z+\-]|%2B){11}).*\.(?<e>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture),
                (b,m) => {
                    // Amazon parameter info:
                    //   Source image seems to always be: id.jpg
                    //   To add modifiers: id._MOD1_MOD2_MOD3_.jpg
                    //   Leading/trailing `_` are optional
                    //   Valid modifiers:
                    //     SS for square size
                    //     BL for blur
                    //     UX for square size?
                    //     FM for format (webp/png)
                    //     Q for quality
                    //     AC?
                    //     500 is a good max size, although 81zPgzpOOfL goes to 1400 for some reason
                    //     Seems like the higher resolution IDs show up when you go to play the song
                    // Example: https://m.media-amazon.com/images/I/81zPgzpOOfL._SS500_.jpg
                    // they can also look like this: 41Q6XHkQ%2BAL
                    var imageId = m.Groups[1].Value;
                    var ext = m.Groups[2].Value;
                    b.ImageUrl = $"https://m.media-amazon.com/images/I/{imageId}._SS400_.{ext}";
                    b.HashImageUrl();
                }),
            (new Regex(@"^(?<a>[0-9a-zA-Z]{10})$",
                RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture),
                (b,m) => b.AmazonASIN = m.Groups[1].Value),
            (new Regex(@"^(https:\/\/)?(?<a>\w+).bandcamp.com\/track\/(?<b>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture),
                (b,m) => { b.BandcampArtist = m.Groups[1].Value; b.BandcampTrack = m.Groups[2].Value; }),
        };

        foreach (var (regex, assign) in matchers)
        {
            var match = regex.Match(link);
            if (match.Success)
            {
                MutateBeatmap(target, beatmap => assign(beatmap, match));
                return true;
            }
        }
        return false;
    }

    public void MutateBeatmap(BeatmapSelectorMap map, Action<Beatmap> mutate)
    {
        if (!MapStorage.CanEdit(map))
        {
            Util.Palette.ShowMessage($"Cannot edit {map}");
            return;
        }

        var beatmap = MapStorage.DeserializeMap(map.MapStoragePath);
        mutate(beatmap);
        beatmap.SaveToDisk(MapStorage);
        DetailContainer.ReloadTarget(beatmap);
        Carousel.ReloadCard(map);
    }

    [CommandHandler]
    public bool AddLink(CommandContext context)
    {
        var target = TargetMap;
        if (target != null)
        {
            if (TryLoadLink(target, Util.ShortClipboard))
            {
                context.ShowMessage("Link loaded from clipboard");
                return true;
            }
            context.GetString(e =>
            {
                TryLoadLink(target, e);
            }, $"Add Link to {target.Metadata?.Title ?? target.MapStoragePath}", "URL", description: "Paste an Amazon or YouTube URL");
            return true;
        }
        return false;
    }
    [CommandHandler]
    public bool SearchSpotifyForMap(CommandContext context)
    {
        // when the url is opened, sometimes the key gets stuck held
        if (context.Repeat) return false;
        var target = TargetMap;
        if (target != null)
        {
            Task.Run(async () =>
            {
                var res = await Spotify.Search(TargetMap.LoadedMetadata);
                if (res.Any(e => !e.Album.Compilation))
                    res = res.Where(e => !e.Album.Compilation).ToList();
                Schedule(() => Util.Palette.Push(new SpotifyTrackDisplay(res, target, this)));
            });
            return true;
        }
        return false;
    }


    FileRequest FileRequest;
    [CommandHandler]
    public bool OpenFile(CommandContext context)
    {
        var oldRequest = FileRequest;
        if (oldRequest != null) Schedule(oldRequest.Close);
        FileRequest = context.GetFile(file =>
        {
            if (Util.DrumGame.OpenFile(context)) return;
            var extension = Path.GetExtension(file);
            if (Util.AudioExtension(extension) || Util.ArchiveExtension(extension))
            {
                var beatmap = Beatmap.Create();
                beatmap.Source = new BJsonSource(MapStorage.GetFullPath("temp")); // temporary source so we can copy audio
                beatmap.Audio = Util.CopyAudio(file, beatmap);

                var beatmapName = Path.GetFileNameWithoutExtension(beatmap.Audio);
                beatmapName = new Regex(@"^((\d\d \-?)|(\d\d\. ?\-?))").Replace(beatmapName, "");
                beatmapName = beatmapName.ToFilename(".bjson");

                beatmap.Source = new BJsonSource(MapStorage.GetFullPath(beatmapName));
                var tags = AudioTagUtil.GetAudioTags(beatmap.FullAudioPath());
                beatmap.Title = tags.Title;
                beatmap.Artist = tags.Artist;

                if (File.Exists(beatmap.Source.AbsolutePath))
                {
                    // technically this is sort of bad since it will leave a danging audio file
                    context.ShowMessage("A map with that name already exists");
                    return;
                }
                beatmap.SaveToDisk(MapStorage);
                OnSelect(State.Filename = beatmapName);
            }
        }, "Open/Import File", "You can also simply drag and drop a file to load it at any time.");
        return true;
    }

    [CommandHandler]
    public bool NewMapFromYouTube(CommandContext context) => context.GetString(url =>
        {
            var name = BeatmapSelector.YouTubeRegex.Match(url).Groups[1].Value;
            var relativeAudio = $"audio/{name}.ogg";
            // TODO would be nice to get some metadata up in here
            var task = YouTubeDL.DownloadBackground(url, $"{Util.MapStorage.AbsolutePath}/{relativeAudio}");
            task.OnSuccess += task =>
            {
                // we can't use our own scheduler, since removing yourself during update is bad
                Util.UpdateThread.Scheduler.Add(() =>
                {
                    var o = Beatmap.Create();
                    o.Source = new BJsonSource(MapStorage.GetFullPath(name + ".bjson"));
                    o.Audio = relativeAudio;
                    o.YouTubeID = name;
                    if (File.Exists(o.Source.AbsolutePath))
                    {
                        context.ShowMessage("A map with that name already exists");
                        return;
                    }
                    o.SaveToDisk(MapStorage);
                    OnSelect(State.Filename = o.Source.Filename);
                });
            };
            task.Enqueue();
        }, "Creating New Beatmap From YouTube", "Url", Util.ShortClipboard);

    public BeatmapSelectorMap GetTargetMap(CommandContext context)
        => context != null && context.TryGetParameter<BeatmapSelectorMap>(out var o) ? o : TargetMap;

    [CommandHandler]
    public bool EditBeatmapMetadata(CommandContext context)
    {
        var target = GetTargetMap(context);
        if (target == null) return false;
        // if we can't edit, we still want to display, just disable saving
        var canEdit = MapStorage.CanEdit(target);
        var beatmap = MapStorage.DeserializeMap(target.MapStoragePath, skipNotes: !canEdit); // we need the full deserialize so we can re-save it
        return context.Palette.Push(MetadataEditor.Build(beatmap, null, onCommit: req =>
        {
            beatmap.SaveToDisk(MapStorage);
            if (filterLoaded) target.LoadFilter(); // update filter string
            Carousel.ReloadCard(target); // make sure metadata on our card gets updated
            ReloadFiltering();
        }), !canEdit);
    }

    [CommandHandler] public bool SelectMods(CommandContext context) { context.Palette.Toggle(new ModSelector(State)); return true; }

    [CommandHandler]
    public bool ToggleMod(CommandContext context)
    {
        if (!context.TryGetParameter(out BeatmapModifier mod))
            if (context.TryGetParameter<string>(out var key))
                mod = BeatmapModifier.Get(key);
        if (mod != null)
        {
            State.ToggleModifier(mod);
            return true;
        }
        return false;
    }

    void ReloadAllCards()
    {
        Carousel.InvalidateAllCards();
        ReloadFiltering();
    }

    void ReloadFiltering()
    {
        loadedCollection = null; // have to make sure collection gets refiltered
        UpdateFilter(); // note, this updates all of the metadata on the visible cards. In the future this may not always be the case
    }

    [CommandHandler]
    public bool SetAllEmptyMappers(CommandContext context)
    {
        if (FilteredMaps != null)
        {
            var targets = FilteredMaps.Where(e => string.IsNullOrWhiteSpace(e.LoadedMetadata.Mapper)
                && MapStorage.CanEdit(e)).ToList();
            var currentMappers = MapStorage.GetCachedMetadata().Values.Select(e => e.Mapper)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct().OrderBy(e => e);
            context.GetStringFreeSolo(currentMappers, newMapper =>
            {
                foreach (var map in targets)
                {
                    var beatmap = MapStorage.DeserializeMap(map.MapStoragePath); // we need the full deserialize so we can re-save it
                    beatmap.Mapper = newMapper;
                    beatmap.SaveToDisk(MapStorage);
                    if (filterLoaded) map.LoadFilter(); // update filter string
                }
                ReloadAllCards();
            }, $"Setting Mapper For {targets.Count} Maps", "Mapper");
        }
        return true;
    }
    [CommandHandler]
    public bool AddTagsToAll(CommandContext context)
    {
        if (FilteredMaps != null)
        {
            var targets = FilteredMaps.ToList();
            context.GetString(tag =>
            {
                foreach (var map in targets)
                {
                    if (MapStorage.CanEdit(map))
                    {
                        var beatmap = MapStorage.DeserializeMap(map.MapStoragePath); // we need the full deserialize so we can re-save it
                        beatmap.AddTags(tag);
                        beatmap.SaveToDisk(MapStorage);
                        if (filterLoaded) map.LoadFilter(); // update filter string
                    }
                }
                ReloadAllCards();
            }, $"Adding Tags to {targets.Count} Maps", "Tags");
        }
        return true;
    }

    [CommandHandler]
    public bool ExportToDtx(CommandContext context)
    {
        if (TargetMap == null) return false;
        return DtxExporter.Export(context, MapStorage.LoadMap(TargetMap.MapStoragePath));
    }

    [CommandHandler]
    public bool ExportMap(CommandContext context) => BeatmapExporter.Export(context, MapStorage.LoadMap(TargetMap.MapStoragePath));


    bool ModifyRating(CommandContext context, int inc)
    {
        var targetMap = GetTargetMap(context);
        if (targetMap == null) return false;

        var target = targetMap?.LoadedMetadata?.Id;
        if (target == null) return false;

        using (var db = Util.GetDbContext())
        {
            var dbMap = db.GetOrAddBeatmap(target);
            dbMap.Rating += inc;
            db.SaveChanges();
            targetMap.LoadedMetadata.Rating = dbMap.Rating;
        }
        Carousel.ReloadCard(targetMap);
        return true;
    }
    [CommandHandler] public bool UpvoteMap(CommandContext context) => ModifyRating(context, 1);
    [CommandHandler] public bool DownvoteMap(CommandContext context) => ModifyRating(context, -1);
    [CommandHandler]
    public bool RevealInFileExplorer(CommandContext context)
    {
        var targetMap = GetTargetMap(context);
        if (targetMap == null) return false;
        Util.RevealInFileExplorer(MapStorage.GetFullPath(targetMap.MapStoragePath));
        return true;
    }
    [CommandHandler]
    public bool RevealAudioInFileExplorer(CommandContext context)
    {
        var targetMap = GetTargetMap(context);
        if (targetMap == null) return false;
        var main = MapStorage.GetFullPath(targetMap.LoadedMetadata.Audio);
        if (File.Exists(main))
            Util.RevealInFileExplorer(main);
        else
        {
            var youTubeId = Util.MapStorage.LoadMap(targetMap.LoadedMetadata).YouTubeID;
            var yt = Util.Resources.YouTubeAudioPath(youTubeId);
            if (File.Exists(yt)) Util.RevealInFileExplorer(yt);
        }
        return true;
    }
    [CommandHandler]
    public void ExportSearchToFile()
    {
        var time = DateTime.UtcNow.GetHashCode();
        var name = $"search-export-{time:X8}.txt";
        var path = Path.Join(Resources.GetDirectory("exports").FullName, name);
        var text = new StringBuilder();

        foreach (var map in FilteredMaps)
        {
            var meta = map.LoadedMetadata;
            text.AppendLine($"{meta.Artist} - {meta.Title}");
        }

        File.WriteAllText(path, text.ToString());

        Util.Host.PresentFileExternally(path);
    }
    [CommandHandler]
    public bool LoadYouTubeAudio(CommandContext context)
    {
        var targetMap = GetTargetMap(context);
        if (targetMap == null) return false;
        var beatmap = MapStorage.DeserializeMap(targetMap.MapStoragePath, skipNotes: true);
        YouTubeDL.TryFixAudio(beatmap, _ => Util.ActivateCommandUpdateThread(Command.Refresh));
        return true;
    }
}

