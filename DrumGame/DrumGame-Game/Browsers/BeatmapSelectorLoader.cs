using System;
using DrumGame.Game.API;
using DrumGame.Game.Media;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Beatmaps.Replay;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.DB;
using NAudio.Wave;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using DrumGame.Game.Utils;
using DrumGame.Game.Beatmaps.Display.Mania;
using DrumGame.Game.Components.Overlays;
using DrumGame.Game.Modals;
using DrumGame.Game.Modifiers;
using DrumGame.Game.IO;
using DrumGame.Game.Containers;

namespace DrumGame.Game.Browsers;

[Cached]
public class BeatmapSelectorLoader : CompositeDrawable
{
    public MemoryAudioRecorder AudioRecorder;
    public BeatmapSelectorState SelectorState = new();
    [Resolved] MapStorage MapStorage { get; set; }
    [Resolved] CommandController Command { get; set; }
    private class BeatmapScene : CompositeDrawable
    {
        public BeatmapPlayer Player;
        readonly BeatmapSelectorState SelectorState; // this is mutable, be careful
        void AddPlayer(BeatmapPlayer player)
        {
            var shader = player.Beatmap.Shader;
            if (!string.IsNullOrWhiteSpace(shader) && Util.Resources.Exists(shader))
            {
                AddInternal(new BeatmapShaderContainer(player, shader)
                {
                    RelativeSizeAxes = Axes.Both
                });
            }
            else
                AddInternal(player);
        }
        public BeatmapScene(Beatmap beatmap, BeatmapSelectorState state)
        {
            RelativeSizeAxes = Axes.Both;
            SelectorState = state;
            BeatmapDisplay display = Util.ConfigManager.DisplayMode.Value == DisplayPreference.Mania ?
                new ManiaBeatmapDisplay() : new MusicNotationBeatmapDisplay();
            if ((state.OpenMode == BeatmapOpenMode.Edit || state.OpenMode == BeatmapOpenMode.Record) && display is MusicNotationBeatmapDisplay mnd)
            {
                AddPlayer(Player = new BeatmapEditor(beatmap, mnd, state.OpenMode, state.Modifiers));
            }
            else
            {
                AddPlayer(Player = new BeatmapPlayer(beatmap, display, state.OpenMode, state.Modifiers));
            }
        }
        public BeatmapScene(Beatmap beatmap, BeatmapReplay replay, BeatmapSelectorState state, ReplayInfo replayInfo)
        {
            SelectorState = state;
            RelativeSizeAxes = Axes.Both;
            var display = new MusicNotationBeatmapReplayDisplay(replay, replayInfo) { RelativeSizeAxes = Axes.Both, Depth = -1 };
            AddPlayer(Player = new BeatmapReplayPlayer(beatmap, display));
        }
        [BackgroundDependencyLoader]
        private void load()
        {
            if (SelectorState.OpenMode == BeatmapOpenMode.Listen)
            {
                Player.Mode = BeatmapPlayerMode.Listening;
                Player.Track.Start();
            }
            else if (SelectorState.Autoplay || SelectorState.OpenMode == BeatmapOpenMode.Play)
                Player.Mode = BeatmapPlayerMode.Playing; // this will start the track for us
            UserActivity.Activity = new PlayingBeatmap(Player);
        }
    }
    BeatmapSelector selector;
    BeatmapScene scene;
    public BeatmapSelectorLoader()
    {
        Util.SelectorLoader = this;
        SelectorState.LoadModsFromConfig();
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        ShowSelector();
        Command.RegisterHandlers(this);
    }

    protected override void Dispose(bool isDisposing)
    {
        Command.RemoveHandlers(this);
        DisableRecorder();
        base.Dispose(isDisposing);
    }

    public void SaveAudioRecording()
    {
        AudioRecorder.WriteToFile(DateTime.MinValue, Dependencies.Get<FileSystemResources>().GetDirectory("recordings").FullName);
    }

    [CommandHandler]
    public bool CloseWithoutSaving(CommandContext _)
    {
        if (scene != null)
        {
            ShowSelector();
            return true;
        }
        return false;
    }
    [CommandHandler] public bool Close(CommandContext _) => CloseWithoutSaving(_);

    public Beatmap CurrentBeatmap => scene?.Player?.Beatmap;

    public BeatmapPlayer LoadMap(Beatmap beatmap)
    {
        if (scene != null) return null; // can't open 2 maps
        if (beatmap == null) return null;
        Util.Palette?.CloseAll();
        if (SelectorState.OpenMode == BeatmapOpenMode.Record)
        {
            if (AudioRecorder == null)
            {
                var devices = AlsaAudioRecorder.GetDeviceNames().AsList();
                if (devices.Count > 0)
                {
                    Command.NewContext().GetString(devices, selected =>
                    {
                        AudioRecorder = new MemoryAudioRecorder(selected, new AlsaAudioRecorder());
                        AudioRecorder.Start();
                        Command.RegisterHandler(Commands.Command.SaveAudioRecording, SaveAudioRecording);
                        LoadMap(beatmap);
                    }, "Select Recording Devices");
                    return null;
                }
            }
        }
        else DisableRecorder();
        HideSelector();
        AddInternal(scene = new BeatmapScene(beatmap, SelectorState));
        return scene.Player;
    }

    void DisableRecorder()
    {
        if (AudioRecorder != null)
        {
            Command.RemoveHandler(Commands.Command.SaveAudioRecording, SaveAudioRecording);
            AudioRecorder.Dispose();
            AudioRecorder = null;
        }
    }

    public void LoadReplay(ReplayInfo replayInfo, BeatmapReplay replay = null)
    {
        HideSelector();
        EnsureBeatmapClosed();
        var beatmap = MapStorage.LoadMapFromId(replayInfo.MapId);
        replay ??= BeatmapReplay.From(Dependencies.Get<FileSystemResources>(), replayInfo);
        AddInternal(scene = new BeatmapScene(beatmap, replay, SelectorState, replayInfo));
    }

    public void ShowSelector()
    {
        if (selector != null) return;
        Track track = null;
        string trackPath = null;
        if (scene != null)
        {
            RemoveInternal(scene, false);
            // we acquire the track so that we can continue playing it on the selector screen
            if (scene.Player.Track.PrimaryTrack == null && scene.Player.Track.Track.IsRunning)
            {
                track = scene.Player.Track.AcquireTrack();
                trackPath = scene.Player.Beatmap.FullAudioPath();
            }
            var source = scene.Player?.Beatmap?.Source?.MapStoragePath;
            if (!string.IsNullOrEmpty(source) && SelectorState.Filename != source)
            {
                // if we have an unsaved rename, it won't be in MapStorage
                if (Util.MapStorage.Exists(source))
                    SelectorState.Filename = source;
            }
            scene.Dispose();
            scene = null;
        }
        AddInternal(selector = new BeatmapSelector(map => LoadMap(MapStorage.LoadMapForPlay(map)) != null, ref SelectorState)
        {
            RelativeSizeAxes = Axes.Both
        });
        if (track != null) selector.DetailContainer.PreviewLoader.LeaseTrack(track, trackPath);
        UserActivity.Set(StaticActivityType.SelectingMap);
    }
    public void HideSelector()
    {
        if (selector != null)
        {
            RemoveInternal(selector, true);
            selector = null;
        }
    }

    void EnsureBeatmapClosed()
    {
        if (scene != null)
        {
            RemoveInternal(scene, true);
            scene = null;
        }
    }

    [CommandHandler]
    public bool UpdateMap(CommandContext context)
    {
        if (selector != null)
        {
            var target = selector.TargetMap?.MapStoragePath;
            if (target == null) return false;
            var sync = SSHSync.From(context);
            if (sync == null) return false;
            BackgroundTask.Enqueue(new BackgroundTask(_ =>
            {
                sync.CopyToLocal("maps", target);
                if (selector != null) Schedule(selector.Refresh);
            })
            { Name = $"Updating {target}" });
            return true;
        }
        else if (scene != null)
        {
            var player = scene.Player;
            var target = player.Beatmap.Source.MapStoragePath;
            if (target == null) return false;
            var sync = SSHSync.From(context);
            if (sync == null) return false;
            BackgroundTask.Enqueue(new BackgroundTask(_ =>
            {
                sync.CopyToLocal("maps", target);
                Schedule(() =>
                {
                    if (player is BeatmapEditor be && be.Dirty)
                    {
                        be.Close(Util.CommandController.NewContext());
                        return;
                    }
                    var state = player.ExportState();
                    var map = MapStorage.LoadMapForPlay(target);
                    if (map != null)
                    {
                        EnsureBeatmapClosed();
                        LoadMap(map);
                        scene?.Player?.LoadState(state);
                    }
                });
            })
            { Name = $"Updating {target}" });
            return true;
        }
        return false;
    }
}

