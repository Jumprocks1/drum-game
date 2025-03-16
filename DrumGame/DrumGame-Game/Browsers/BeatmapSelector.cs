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
using DrumGame.Game.Beatmaps.Display.Mania;
using DrumGame.Game.Beatmaps.Formats;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Browsers.BeatmapSelection;
using DrumGame.Game.Commands;
using DrumGame.Game.Commands.Requests;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Interfaces;
using DrumGame.Game.IO;
using DrumGame.Game.Modals;
using DrumGame.Game.Modifiers;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
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
    public Bindable<string> StringModifiersBindable => Util.ConfigManager.GetBindable<string>(DrumGameSetting.Modifiers);

    public void LoadModsFromConfig() // could probably just put this in a constructor instead
    {
        var mods = StringModifiersBindable.Value;
        if (string.IsNullOrWhiteSpace(mods)) return;
        Modifiers = BeatmapModifier.ParseModifiers(mods, false);
        // we don't call TriggerModifiersChanged since we don't need to assign to the bindable
        OnModifiersChange?.Invoke();
    }

    void TriggerModifiersChanged()
    {
        StringModifiersBindable.Value = BeatmapModifier.SerializeAllModifiers(Modifiers);
        OnModifiersChange?.Invoke();
    }

    public void ModifierConfigured(BeatmapModifier modifier)
    {
        TriggerModifiersChanged();
    }
    public void AddModifier(BeatmapModifier modifier)
    {
        if (modifier == null) return;
        (Modifiers ??= new()).Add(modifier);
        TriggerModifiersChanged();
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
                    TriggerModifiersChanged();
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
            TriggerModifiersChanged();
        }
    }
}
[Cached]
public partial class BeatmapSelector : CompositeDrawable
{
    public const float HeaderHeight = 50;
    public BeatmapOpenMode OpenMode => State.OpenMode;
    readonly Func<string, bool> OnSelect;
    public readonly BeatmapSelectorState State;
    public BeatmapSelector(Func<string, bool> onSelect) : this(onSelect, new BeatmapSelectorState()) { }
    private BeatmapSelector(Func<string, bool> onSelect, BeatmapSelectorState state)
    {
        OnSelect = onSelect;
        State = state;
        Util.CommandController.RegisterHandlers(this);
    }
    // This ref is super sketchy but I kinda like it
    // All it lets us do is pass in null for the state and still have it work
    // If we pass null into the state above then it will be sad without the ref
    // Could probably just use a static default selector state for null but this is fun too
    public BeatmapSelector(Func<string, bool> onSelect, ref BeatmapSelectorState state) : this(onSelect, state ??= new BeatmapSelectorState()) { }
    BeatmapCarousel Carousel;
    public CollectionStorage CollectionStorage => Util.DrumGame.CollectionStorage;
    public MapStorage MapStorage => Util.MapStorage;
    List<BeatmapSelectorMap> Maps;
    void LoadAllMaps()
    {
        Maps = MapStorage.GetMaps().Select(e => new BeatmapSelectorMap(e)).ToList();
    }
    public BeatmapDetailContainer DetailContainer;
    // FilteredMaps contains all maps that match our collection + search filter
    public List<BeatmapSelectorMap> FilteredMaps;
    // CarouselMaps is a grouping of FilteredMaps. Maps with the same MapSet Id are combined into 1 BeatmapSelectorMap
    // We will have to store the selected map as more than just an index,
    //  since the carousel index would only show which map set is selected
    public List<BeatmapSelectorMap> CarouselMaps => FilteredMaps;
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
    private void load()
    {
        State.SearchBindable = Util.ConfigManager.GetBindable<string>(DrumGameSetting.BeatmapSearch);
        State.CollectionBindable = Util.ConfigManager.GetBindable<string>(DrumGameSetting.CurrentCollection);
        State.OnIndexChange += UpdateTargetMap;
        Carousel = new BeatmapCarousel(this);

        if (Util.Skin.SelectorBackground != null && Util.Skin.SelectorBackground.Alpha > 0)
            AddInternal(new BackgroundSkinTexture(() => Util.Skin.SelectorBackground, null) { RelativeSizeAxes = Axes.Both, Depth = 500, Relative = true });
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
                NameTooltip = provider?.AbsolutePath
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
    public void SelectMap(BeatmapSelectorMap map, bool autoplay) => SelectMap(map?.MapStoragePath, autoplay);
    public bool SelectMap(string mapStoragePath, bool autoplay)
    {
        if (mapStoragePath == null) return false;
        State.Autoplay = autoplay;
        return OnSelect(State.Filename = mapStoragePath);
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

        var beatmap = MapStorage.LoadForQuickEdit(map.MapStoragePath);
        mutate(beatmap);
        beatmap.SaveToDisk(MapStorage);
        DetailContainer.ReloadTarget(map, beatmap);
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
            if (MapStorage.Contains(file))
            {
                if (SelectMap(file, false))
                    return;
            }
            if (Util.DrumGame.OpenFile(context)) return;
            var extension = Path.GetExtension(file);
            // this checks for archives since the CopyAudio method supports loading from a zip
            // sometimes when buying from Amazon, single songs are zipped.
            if (Util.AudioExtension(extension) || Util.ArchiveExtension(extension))
            {
                var beatmap = Beatmap.Create();
                beatmap.Source = new BJsonSource(MapStorage.GetFullPath("temp"), BJsonFormat.Instance); // temporary source so we can copy audio
                beatmap.Audio = Util.CopyAudio(file, beatmap);

                var beatmapName = Path.GetFileNameWithoutExtension(beatmap.Audio);
                beatmapName = new Regex(@"^((\d\d \-?)|(\d\d\. ?\-?))").Replace(beatmapName, "");
                beatmapName = beatmapName.ToFilename(".bjson");

                beatmap.Source = new BJsonSource(MapStorage.GetFullPath(beatmapName), BJsonFormat.Instance);
                var tags = AudioTagUtil.GetAudioTags(beatmap.FullAudioPath());
                beatmap.Title = tags.Title;
                beatmap.Artist = tags.Artist;

                if (File.Exists(beatmap.Source.AbsolutePath))
                {
                    // technically this is sort of bad since it will leave a dangling audio file
                    context.ShowMessage("A map with that name already exists");
                    return;
                }
                beatmap.SaveToDisk(MapStorage);
                OnSelect(State.Filename = beatmapName);
            }
        }, "Open/Import File", "You can drag and drop a file to load it at any time.");
        if (FileRequest != null)
        {
            FileRequest.Add(new SpriteText
            {
                Text = "Supported file types include (hover for more info):",
                Font = FrameworkFont.Regular.With(size: 20),
                Y = 10
            });
            var y = 36;
            void addFileType(string name, string help)
            {
                FileRequest.Add(new MarkupTooltipSpriteText
                {
                    Y = y,
                    X = 10,
                    Text = name,
                    MarkupTooltip = help,
                    Font = FrameworkFont.Regular.With(size: 16),
                    Colour = DrumColors.BrightGreen
                });
                y += 18;
            }
            addFileType(".bjson", "These are the primary format for Drum Game maps.\nOther formats get converted to .bjson when imported.\nThis format can be viewed with a standard text editor such as VSCode or Notepad."
                + $"\nTo open the current map in your file explorer, try {IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.RevealInFileExplorer)}.");
            addFileType(".dtx, set.def", "These file types are from DTXMania.\nImporting for these types should work in almost all cases."
                + "\nIf the .dtx file contains multiple BGM tracks (sometimes called stems), you may want to install FFmpeg."
                + "\nWith FFmpeg installed, the game will attempt to merge the BGM files before importing."
                + "\nFFmpeg should be placed in the system path or in the `resources/lib` folder.");
            addFileType("song.ini", $"This format is from Clone Hero/Phaseshift."
                + "\nCurrently only imports the highest difficulty. These files can be edited before importing using Onyx."
                + "\nMost notes should import correctly, though keep in mind Clone Hero charts were designed for having only 4/5 lanes."
                + "\nBackground audio may fail to import."
                + "\nImporting these is still a work in progress, please report any issues on the Discord.");
            addFileType(".mp3, .m4a, .ogg, .webm, .flac, .wav", "These are audio files.\nWhen imported on the select screen, the game will make a new map.\nWhen imported in the editor, the game will swap out the BGM for the new file.");
            addFileType(".zip, .7z, .rar", "These are archive formats.\nWhen imported, the game will try to find any supported file types and import them."
                + "\n.7z and .rar requiring having 7z.exe in your system path."
                + "\nIf importing the archive fails or has issues, please try extracting the files first.");
        }
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
                o.Source = new BJsonSource(MapStorage.GetFullPath(name + ".bjson"), BJsonFormat.Instance);
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
    public bool CreateMapSet(CommandContext context)
    {
        var maps = FilteredMaps.ToArray();
        foreach (var map in maps)
        {
            var canEdit = MapStorage.CanEdit(map);
            if (!canEdit)
            {
                context.Palette.ShowMessage("The current search results contain maps that cannot be edited.");
                return true;
            }
        }
        if (maps.Length <= 1)
        {
            context.Palette.ShowMessage("Please filter the map list to 2+ maps in order to make a map set");
            return true;
        }
        // order by is needed for consistent hashing
        var defaultHash = Util.MD5(maps.OrderBy(e => e.MapStoragePath).SelectMany(e =>
        {
            var meta = e.LoadedMetadata;
            return new string[] { meta.Title, meta.Artist, meta.Mapper };
        }));
        var req = context.Palette.Request(new RequestConfig
        {
            Title = "Creating Map Set",
            Description = $"This will override the map set field for all visible maps ({maps.Length}).",
            Field = new StringFieldConfig("Map set ID", defaultHash)
            {
                MarkupTooltip = "The default value is a hash of the metadata for all maps in the map set.\nAny value should work."
            },
            CommitText = $"Create Set",
            OnCommit = req =>
            {
                var newMapSet = req.GetValue<string>();
                void applyChange()
                {
                    foreach (var map in maps)
                    {
                        var beatmap = MapStorage.LoadForQuickEdit(map.MapStoragePath);
                        beatmap.MapSetId = newMapSet;
                        beatmap.SaveToDisk(MapStorage);
                    }
                    Refresh(); // we are changing all visible maps, so this is good
                }
                if (maps.Length <= 10)
                {
                    applyChange();
                }
                else context.Palette.Request(new RequestConfig
                {
                    Title = $"Changing {maps.Length} maps",
                    Description = "This is more than 10 maps, are you sure you wish to proceed?",
                    CommitText = "Apply Change",
                    OnCommit = _ => applyChange()
                });
            }
        });
        void addWarning(string message)
        {
            var y = req.InnerContent.Children.Sum(e => e.Height);
            var s = new SpriteText
            {
                Text = message,
                Colour = DrumColors.WarningText,
                Font = FrameworkFont.Regular,
                Y = y
            };
            req.Add(s);
        }
        var check = maps[0].LoadedMetadata.Artist;
        if (maps.Any(e => e.LoadedMetadata.Artist != check))
            addWarning("Set contains multiple artists");
        check = maps[0].LoadedMetadata.Mapper;
        if (maps.Any(e => e.LoadedMetadata.Mapper != check))
            addWarning("Set contains multiple mappers");
        // take shortest title so things like (TV Size) work
        check = maps.Select(e => e.LoadedMetadata.Title).MinBy(e => e.Length);
        if (maps.Any(e => !e.LoadedMetadata.Title.Contains(check, StringComparison.OrdinalIgnoreCase)))
            addWarning("Set contains multiple titles");
        if (maps.Length > 10)
            addWarning("Set contains more than 10 maps");
        return req;
    }
    [CommandHandler]
    public bool EditBeatmapMetadata(CommandContext context)
    {
        var target = GetTargetMap(context);
        if (target == null) return false;
        // if we can't edit, we still want to display, just disable saving
        var canEdit = MapStorage.CanEdit(target);
        var beatmap = canEdit ? MapStorage.LoadForQuickEdit(target.MapStoragePath)
            : MapStorage.LoadForQuickMetadata(target.MapStoragePath);
        return context.Palette.Push(MetadataEditor.Build(beatmap, null, onCommit: req =>
        {
            beatmap.SaveToDisk(MapStorage);
            if (filterLoaded) target.LoadFilter(); // update filter string
                                                   // make sure active detail container gets reloaded if needed
                                                   // this method only reloads if the current target matches
            DetailContainer.ReloadTarget(target, beatmap);
            Carousel.ReloadCard(target); // make sure metadata on our card gets updated
            ReloadFiltering();
        }, !canEdit));
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
                    var beatmap = MapStorage.LoadForQuickEdit(map.MapStoragePath); // we need the full deserialize so we can re-save it
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
                        var beatmap = MapStorage.LoadForQuickEdit(map.MapStoragePath); // we need the full deserialize so we can re-save it
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
        return DtxExporter.Export(context, MapStorage.LoadMapForPlay(TargetMap.MapStoragePath));
    }

    [CommandHandler]
    public bool ExportMap(CommandContext context) => BeatmapExporter.Export(context, MapStorage.LoadMapForPlay(TargetMap.MapStoragePath));


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

    bool handleMapExternally(CommandContext context, Action<string> action)
    {
        var targetMap = GetTargetMap(context);
        if (targetMap == null) return false;
        action(MapStorage.GetFullPath(targetMap.MapStoragePath));
        return true;
    }
    [CommandHandler] public bool RevealInFileExplorer(CommandContext context) => handleMapExternally(context, Util.RevealInFileExplorer);
    [CommandHandler] public bool OpenExternally(CommandContext context) => handleMapExternally(context, Util.OpenExternally);
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
    public enum ExportSearchType
    {
        Basic,
        RequestListJson,
        FullJson
    }
    [CommandHandler]
    public bool ExportSearchToFile(CommandContext context)
    {
        var requestConfig = new RequestConfig
        {
            Title = "Exporting Search",
            Field = new EnumFieldConfig<ExportSearchType>(),
            CommitText = "Export",
            OnCommitBasic = request =>
            {
                var time = DateTime.UtcNow.GetHashCode();
                var format = request.GetValue<ExportSearchType>();
                string path = null;
                if (format == ExportSearchType.Basic)
                {
                    var name = $"search-export-{time:X8}.txt";
                    path = Path.Join(Util.Resources.GetDirectory("exports").FullName, name);
                    var text = new StringBuilder();

                    foreach (var map in FilteredMaps)
                    {
                        var meta = map.LoadedMetadata;
                        text.AppendLine($"{meta.Artist} - {meta.Title}");
                    }

                    File.WriteAllText(path, text.ToString());
                }
                else if (format == ExportSearchType.RequestListJson)
                {
                    var name = $"search-export-{time:X8}.json";
                    path = Path.Join(Util.Resources.GetDirectory("exports").FullName, name);

                    var collection = CollectionStorage.GetCollection("request-list.json");
                    var targetMaps = FilteredMaps;

                    if (collection != null)
                        targetMaps = collection.Apply(Maps, CollectionStorage).ToList();


                    File.WriteAllText(path, JsonConvert.SerializeObject(targetMaps.Select(e => e.LoadedMetadata).Select(e =>
                    {
                        var dtxLevel = e.DtxLevel;
                        var diffString = e.DifficultyString;
                        if (string.IsNullOrWhiteSpace(diffString))
                            diffString = dtxLevel;
                        else if (!string.IsNullOrWhiteSpace(dtxLevel))
                            diffString = $"{diffString} - {dtxLevel}";
                        return new
                        {
                            e.Artist,
                            e.Title,
                            MedianBPM = e.BPM,
                            e.DtxLevel,
                            e.Difficulty,
                            DifficultyString = diffString,
                            e.Duration,
                            e.ImageUrl,
                            e.Mapper,
                            e.MapSetId,
                            e.RomanArtist,
                            e.RomanTitle,
                            e.Tags,
                            PlayableDuration = e.Duration
                        };
                    })));
                }
                else if (format == ExportSearchType.FullJson)
                {
                    var name = $"search-export-{time:X8}.json";
                    path = Path.Join(Util.Resources.GetDirectory("exports").FullName, name);
                    File.WriteAllText(path, JsonConvert.SerializeObject(FilteredMaps.Select(e => e.LoadedMetadata)));
                }
                var dontOpen = context.TryGetParameter<bool>(1, out var o) && o;
                if (path != null && !dontOpen)
                    Util.Host.PresentFileExternally(path);
            }
        };
        return context.HandleRequest(requestConfig);
    }
    [CommandHandler]
    public bool LoadYouTubeAudio(CommandContext context)
    {
        var targetMap = GetTargetMap(context);
        if (targetMap == null) return false;
        var beatmap = MapStorage.LoadForQuickMetadata(targetMap.MapStoragePath);
        YouTubeDL.TryFixAudio(beatmap, _ => Util.ActivateCommandUpdateThread(Command.Refresh));
        return true;
    }
}

