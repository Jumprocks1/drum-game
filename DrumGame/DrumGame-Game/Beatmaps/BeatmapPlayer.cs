using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DrumGame.Game.API;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Display.Mania;
using DrumGame.Game.Beatmaps.Display.ScoreDisplay;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Beatmaps.Practice;
using DrumGame.Game.Beatmaps.Replay;
using DrumGame.Game.Browsers;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Input;
using DrumGame.Game.Media;
using DrumGame.Game.Modifiers;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Logging;

namespace DrumGame.Game.Beatmaps;

[Flags]
public enum BeatmapPlayerMode
{
    Listening = 0,
    Edit = 1, // allows editing beatmap
    Fill = Edit + 2,
    Playing = 4, // loads input handler/display
    Record = Edit | Playing,
    Watching = 8,
    Replay = Playing | Watching, // mostly just set so it changes to mode text
    Practice = 16 | Playing
}
public class BeatmapPlayer : CompositeDrawable
{
    BeatmapPlayerMode _mode = BeatmapPlayerMode.Listening;
    public BeatmapPlayerInputHandler BeatmapPlayerInputHandler;
    public PracticeMode PracticeMode;
    protected FileSystemResources Resources => Util.Resources;
    protected MapStorage MapStorage => Util.MapStorage;
    public CommandController Command => Util.CommandController;
    public BeatmapSelectorLoader Loader => Dependencies.Get<BeatmapSelectorLoader>();
    protected virtual bool AutoStart => true;
    public BeatmapPlayerMode Mode
    {
        get => _mode; set
        {
            _mode = value;
            if (value.HasFlagFast(BeatmapPlayerMode.Playing))
            {
                // make sure we have some sort of input handler
                if (BeatmapPlayerInputHandler == null)
                {
                    BeatmapPlayerInputHandler = new BeatmapPlayerInputHandler(this);
                    BeatmapPlayerInputHandler.OnTrigger += Display.OnDrumTrigger;
                    Display.EnterPlayMode();
                }
                if (AutoStart && value != BeatmapPlayerMode.Practice && !Track.IsRunning) Track.Start();
            }
            else
            {
                if (BeatmapPlayerInputHandler != null)
                {
                    Display.LeavePlayMode();
                    BeatmapPlayerInputHandler.Dispose();
                    BeatmapPlayerInputHandler = null;
                }
            }
            if (Display is MusicNotationBeatmapDisplay d)
            {
                var edit = value.HasFlagFast(BeatmapPlayerMode.Edit);
                var record = value == BeatmapPlayerMode.Record;
                var measureLines = (edit && !record) || Util.Skin.Notation.MeasureLines;
                if (d.MeasureLines == null == measureLines) d.ToggleMeasureLines();
            }
            ProtectedModeChanged(value);
            ModeChanged?.Invoke(value);
        }
    }
    // added so that ModeChanged is always called after child mode change
    protected virtual void ProtectedModeChanged(BeatmapPlayerMode mode) { }
    public event Action<BeatmapPlayerMode> ModeChanged;
    public readonly Beatmap Beatmap;
    public BeatClock Track;
    public readonly BeatmapDisplay Display;
    public readonly BeatmapOpenMode OpenMode;
    public List<BeatmapModifier> Modifiers;
    public BeatmapPlayer(Beatmap beatmap, BeatmapDisplay display, BeatmapOpenMode openMode, List<BeatmapModifier> mods = null)
    {
        // careful not to do too much here, since beatmap could be modified before loading
        OpenMode = openMode;
        display.Player = this;
        Beatmap = beatmap;
        Display = display;
        display.RelativeSizeAxes = Axes.Both;
        display.Depth = -1;
        RelativeSizeAxes = Axes.Both;
        Modifiers = mods;
        ApplyMods();
        PracticeMode.AddHook(this);

        Beatmap.LengthChanged += LengthChanged;
        LengthChanged();
        Track = new BeatClock(Beatmap, LoadTrack(true));
        Track.BeforeSeek += _ =>
        {
            if (Util.DrumGame.Drumset.IsValueCreated)
                Util.DrumGame.Drumset.Value.ClearQueue();
        };
        Command.RegisterHandlers(this);
        Util.DrumGame.VolumeController.MetronomeVolume.Muted.BindValueChanged(MetronomeMuteChanged, true);
        AddInternal(Display);
    }
    void ApplyMods()
    {
        if (Modifiers != null)
        {
            foreach (var mod in Modifiers) mod.Modify(this);
        }
    }
    protected double endTime = 0;
    void LengthChanged() => endTime = Beatmap.EndTime();

    public bool AtEndScreen => endScreen != null;

    public const double EndTimeDelay = 1000;

    double endScreenTriggerTime;

    protected virtual bool ShouldTriggerEndScreen =>
        Mode == BeatmapPlayerMode.Playing && BeatmapPlayerInputHandler != null &&
            BeatmapPlayerInputHandler.Scorer.ReplayInfo.StartNote == -1 &&
            (Track.CurrentTime > endTime + EndTimeDelay || Track.CurrentTime >= Track.EndTime) &&
            Beatmap.HitObjects.Count > 0;

    // if we seek backwards or switch modes, hide end screen
    bool ShouldHideEndscreen => Track.CurrentTime < endScreenTriggerTime || Mode != BeatmapPlayerMode.Playing;

    EndScreen endScreen;
    protected override void Update()
    {
        Track.Update(Clock.ElapsedFrameTime);
        BeatmapPlayerInputHandler?.Update();
        PracticeMode?.Update();
        // TODO we should always show end screen, but have a note saying not played from start, so not saving replay
        // Have option to save anyways (to file)
        if (ShouldTriggerEndScreen)
        {
            TriggerEndScreen();
        }
        else if (endScreen != null)
        {
            if (ShouldHideEndscreen)
            {
                RemoveInternal(endScreen, true);
                endScreen = null;
            }
        }
        base.Update();
    }

    [CommandHandler] public void ShowEndScreen() => TriggerEndScreen();
    public void TogglePracticeMode() => Mode = Mode == BeatmapPlayerMode.Practice ? BeatmapPlayerMode.Playing : BeatmapPlayerMode.Practice;
    [CommandHandler(Commands.Command.PracticeMode)]
    public void PracticeModeCommand()
    {
        if (PracticeMode == null) Mode = BeatmapPlayerMode.Practice;
        else PracticeMode.Configure();
    }

    public void EnterPracticeMode(double startBeat, double endBeat)
    {
        PracticeMode = new(Display, startBeat, endBeat);
        Mode = BeatmapPlayerMode.Practice;
        PracticeMode.Begin();
    }

    protected virtual void TriggerEndScreen()
    {
        if (endScreen != null) return;
        endScreenTriggerTime = Track.CurrentTime;
        ReplayInfo replayInfo;
        BeatmapReplay replay = null;
        if (this is BeatmapReplayPlayer replayPlayer)
        {
            replayInfo = replayPlayer.ReplayInfo;
        }
        else if (BeatmapPlayerInputHandler != null)
        {
            // eventually this should run on background thread
            replayInfo = BeatmapPlayerInputHandler.BuildReplay();
            replayInfo.SetMods(Modifiers);
            replayInfo.MapId = Beatmap.Id;
            replayInfo.SetCompleteTime();
            Logger.Log($"Saving replay info for {replayInfo.MapId}", target: LoggingTarget.Database, level: LogLevel.Important);
            using (var context = Util.GetDbContext())
            {
                context.Replays.Add(replayInfo);
                context.GetOrAddBeatmap(Beatmap.Id).PlayTime = replayInfo.CompleteTimeTicks;
                context.SaveChanges();
            }
            replay = new BeatmapReplay(BeatmapPlayerInputHandler.Events);
        }
        else replayInfo = new();
        endScreen = new EndScreen(replayInfo, replay, this)
        {
            Alpha = 0f,
            Depth = -2
        };
        AddInternal(endScreen);
        endScreen.FadeIn();
    }

    FileRequest FileRequest;
    public Track LoadTrack(bool allowVirtual = false)
    {
        Track track = null;
        void AddVt()
        {
            var vt = Resources.Tracks.GetVirtual(0);
            void UpdateVt() => vt.Length = Beatmap.MillisecondsFromBeat(Beatmap.QuarterNotes) + 3000;
            // should probably unbind these, but also doesn't matter much
            Beatmap.TempoUpdated += UpdateVt;
            Beatmap.OffsetUpdated += UpdateVt;
            Beatmap.LengthChanged += UpdateVt;
            UpdateVt();
            track = vt;
        }
        if (Beatmap.Audio == "metronome")
        {
            AddVt();
            Util.DrumGame.VolumeController.MetronomeVolume.Unmute();
        }
        else
        {
            track = Resources.GetTrack(Beatmap.FullAudioPath());

            if (Beatmap.YouTubeID != null && track == null)
            {
                var target = Beatmap.YouTubeAudioPath;
                if (File.Exists(target))
                {
                    track = Resources.GetTrack(target);
                    Beatmap.UseYouTubeOffset = true;
                    Beatmap.FireOffsetUpdated();
                }
            }
            // track = null;
            if (track == null)
            {
                if (!allowVirtual) return null;
                if (Beatmap.YouTubeID != null && RemoteVideoWebSocket.Current?.Connected == true)
                {
                    RemoteVideoWebSocket.Current.VideoCallback(Beatmap.YouTubeID, () =>
                    {
                        Schedule(() =>
                        {
                            var newTrack = new YouTubeTrack(Beatmap.YouTubeID);
                            ((AudioCollectionManager<AdjustableAudioComponent>)Util.Resources.Tracks).AddItem(newTrack);
                            newTrack.Volume.Value = Beatmap.CurrentRelativeVolume;
                            SwapTrack(newTrack);
                        });
                    });
                    RemoteVideoWebSocket.Current.TargetVideoId = Beatmap.YouTubeID;
                }
                else FixAudio(null);
                AddVt();
            }
        }
        track.Volume.Value = Beatmap.CurrentRelativeVolume;
        return track;
    }

    protected override void Dispose(bool isDisposing)
    {
        // make sure to dispose children first, since this will unhook MIDI events for us
        // if we dispose the track before MIDI hooks, we could get invalid events
        base.Dispose(isDisposing);
        Beatmap.LengthChanged -= LengthChanged;
        Util.DrumGame.VolumeController.MetronomeVolume.Muted.ValueChanged -= MetronomeMuteChanged;
        BeatmapPlayerInputHandler?.Dispose();
        PracticeMode?.Exit();
        PracticeMode = null;
        Command.RemoveHandlers(this);
        Track.Dispose();
    }
    [CommandHandler] public void NextNote() => Track.SeekToBeat(Track.NextHitOrBeat(true));
    [CommandHandler] public void PreviousNote() => Track.SeekToBeat(Track.NextHitOrBeat(false));
    [CommandHandler]
    public void SwitchMode()
    {
        if (Mode.HasFlagFast(BeatmapPlayerMode.Playing))
            Mode = this is BeatmapEditor ? BeatmapPlayerMode.Edit : BeatmapPlayerMode.Listening;
        else
            Mode = BeatmapPlayerMode.Playing;
    }
    [CommandHandler] public void SeekToNextBookmark() => SeekToNext(Beatmap.Bookmarks.Select(e => e.Time));
    [CommandHandler] public void SeekToPreviousBookmark() => SeekToPrevious(Beatmap.Bookmarks.Select(e => e.Time));
    [CommandHandler] public void SeekToNextTimelineMark() => SeekToNext(Beatmap.GetTimelineMarks());
    [CommandHandler] public void SeekToPreviousTimelineMark() => SeekToPrevious(Beatmap.GetTimelineMarks());
    public void SeekToNext(IEnumerable<double> beatTimes)
    {
        var currentBeat = Track.CurrentBeat + Beatmap.BeatEpsilon;
        foreach (var t in beatTimes)
        {
            if (t > currentBeat)
            {
                Track.Seek(Beatmap.MillisecondsFromBeat(t));
                return;
            }
        }
        if (Beatmap.QuarterNotes > currentBeat) Track.Seek(Beatmap.MillisecondsFromBeat(Beatmap.QuarterNotes));
    }
    public void SeekToPrevious(IEnumerable<double> beatTimes)
    {
        double currentBeat;
        if (Track.IsRunning)
        {
            // This gives us 200ms to press back again to skip past the most recent point
            // we don't need to use EffectiveRate since we already check Track.IsRunning
            currentBeat = Beatmap.BeatFromMilliseconds(Track.CurrentTime - 200 * Track.Rate);
        }
        else
        {
            currentBeat = Track.CurrentBeat - Beatmap.BeatEpsilon;
        }
        double? target = null;
        foreach (var t in beatTimes)
        {
            if (t >= currentBeat) break;
            target = t;
        }
        Track.Seek(target.HasValue ? Beatmap.MillisecondsFromBeat(target.Value) : -Track.LeadIn);
    }

    public bool AddAudio(string file)
    {
        var newAudioPath = Util.CopyAudio(file, Beatmap);
        if (newAudioPath == null) return false;
        if (newAudioPath == Beatmap.Audio)
        {
            // audio path already good, may need to swap track though
            if (Track.Virtual) SwapTrack(LoadTrack());
            return true;
        }


        var ed = this as BeatmapEditor;
        // this handles swapping the audio track
        var change = new AudioBeatmapChange(Beatmap, newAudioPath, this);
        if (ed != null) ed.PushChange(change);
        else change.Do();
        // we don't want to save in edit mode, since they need to save manually
        // in edit mode they will also be asked to save on exit
        if (ed == null)
        {
            Beatmap.Export();
            Beatmap.TrySaveToDisk();
        }
        else
        {
            if (Beatmap.Title == null || Beatmap.Artist == null)
            {
                var tags = AudioTagUtil.GetAudioTags(Beatmap.FullAudioPath());
                if (Beatmap.Title == null)
                    ed.PushChange(() => Beatmap.Title = tags.Title,
                        () => Beatmap.Title = null, $"set beatmap title to {tags.Title}");
                if (Beatmap.Artist == null)
                    ed.PushChange(() => Beatmap.Artist = tags.Artist,
                        () => Beatmap.Title = null, $"set beatmap artist to {tags.Artist}");
            }
        }
        CloseFileRequest();
        return true;
    }

    [CommandHandler]
    public bool OpenFile(CommandContext context)
    {
        var oldRequest = FileRequest;
        if (oldRequest != null) Schedule(oldRequest.Close);
        FileRequest = context.GetFile(file =>
        {
            var extension = Path.GetExtension(file);
            if (Util.AudioExtension(extension) || Util.ArchiveExtension(extension)) AddAudio(file);
            else if (extension == ".mid")
            {
                if (this is BeatmapEditor ed)
                {
                    var oldTiming = new List<TempoChange>(Beatmap.TempoChanges);
                    var oldNotes = new List<HitObject>(Beatmap.HitObjects);
                    var oldMeasures = new List<MeasureChange>(Beatmap.MeasureChanges);
                    var oldTick = Beatmap.TickRate;
                    ed.PushChange(() =>
                    {
                        try { MidiLoader.LoadMidi(Beatmap, file); }
                        catch (Exception e)
                        {
                            Logger.Error(e, "MIDI import failed");
                            Util.Palette.ShowMessage("MIDI import failed. See log for details.");
                        }
                        Display.ReloadNoteRange(true);
                        Beatmap.FireTempoUpdated();
                    }, () =>
                      {
                          Beatmap.TickRate = oldTick;
                          Beatmap.TempoChanges = oldTiming;
                          Beatmap.HitObjects = oldNotes;
                          Beatmap.MeasureChanges = oldMeasures;
                          Beatmap.UpdateLength();
                          Display.ReloadNoteRange(true);
                          Beatmap.FireTempoUpdated();
                      }, $"Import MIDI from {file}");
                }
            }
        }, "Open/Import File", "You can also simply drag and drop a file to load it at any time.");
        return true;
    }


    public bool UseYouTubeOffset => Beatmap.UseYouTubeOffset;
    public string CurrentAudioPath => UseYouTubeOffset ? Beatmap.YouTubeAudioPath : Beatmap.FullAudioPath();
    void CloseFileRequest() // update thread
    {
        FileRequest?.Close();
        FileRequest = null;
    }
    [CommandHandler]
    public bool FixAudio(CommandContext _) // update thread
    {
        if (Track != null && !Track.Virtual) return false;
        CloseFileRequest();
        FileRequest = Util.Palette.RequestFile("Beatmap Missing Audio File",
            "Drop an mp3/ogg file into the window to add audio to this beatmap.",
            e =>
            {
                if (e != null && !string.IsNullOrWhiteSpace(e))
                {
                    Util.EnsureUpdateThread(() =>
                    {
                        CloseFileRequest();
                        AddAudio(e);
                    });
                }
            });
        if (Beatmap.YouTubeID != null)
        {
            FileRequest.Add(new SpriteText
            {
                Text = "This map has a YouTube URL that can be used for audio.",
                Font = FrameworkFont.Regular,
                Y = 10,
            });
            var waiting = false;
            var button1 = new CommandButton(Commands.Command.LoadYouTubeAudio)
            {
                AutoSize = true,
                Text = "Download YouTube Audio",
                Y = 35
            };
            FileRequest.Add(button1);
            FileRequest.Add(new DrumButtonTooltip
            {
                Text = "Connect YouTube Audio",
                TooltipText = $"Clicking this will open {DrumWebSocket.DrumGameWebUrl} and connect to YouTube with a WebSocket. Performance may be degraded compared to native audio.",
                Height = 30,
                AutoSize = true,
                X = button1.Width + 5,
                Y = 35,
                Action = () =>
                {
                    var socket = RemoteVideoWebSocket.EnsureStarted();
                    socket.TargetVideoId = Beatmap.YouTubeID;
                    Util.Host.OpenUrlExternally(DrumWebSocket.DrumGameWebUrl + "/ws");
                    if (!waiting)
                    {
                        waiting = true;
                        FileRequest.Add(new SpriteText
                        {
                            Text = "Waiting for connection...",
                            Font = FrameworkFont.Regular,
                            Y = 65
                        });
                    }
                    socket.VideoCallback(Beatmap.YouTubeID, () =>
                    {
                        Schedule(() =>
                        {
                            FileRequest.Close();
                            var newTrack = new YouTubeTrack(Beatmap.YouTubeID);
                            ((AudioCollectionManager<AdjustableAudioComponent>)Util.Resources.Tracks).AddItem(newTrack);
                            newTrack.Volume.Value = Beatmap.CurrentRelativeVolume;
                            SwapTrack(newTrack);
                        });
                    });
                }
            });
        }
        return true;
    }

    public void SwapTrack(Track track)
    {
        Track.SwapTrack(track);
        if (this is BeatmapEditor be) be.ResetOffsetWizard();
    }

    [CommandHandler]
    public bool LoadYouTubeAudio(CommandContext _)
    {
        if (Beatmap.YouTubeID != null)
        {
            var target = Beatmap.YouTubeAudioPath;
            if (File.Exists(target))
            {
                var track = Resources.GetTrack(target);
                track.Volume.Value = Beatmap.CurrentRelativeVolume;
                SwapTrack(track);
                Beatmap.UseYouTubeOffset = true;
                Beatmap.FireOffsetUpdated();
                CloseFileRequest();
                return true;
            }
            else
            {
                YouTubeDL.ForceLoadYouTubeAudio(Beatmap, _ => Schedule(() =>
                {
                    if (File.Exists(target))
                        LoadYouTubeAudio(null);
                }), false);
                return true;
            }
        }
        return false;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (BeatmapPlayerInputHandler != null && BeatmapPlayerInputHandler.Handle(e))
            return true;
        return base.OnKeyDown(e);
    }


    [CommandHandler] public void RevealAudioInFileExplorer() => Util.RevealInFileExplorer(CurrentAudioPath);
    Metronome _metronome;
    void MetronomeMuteChanged(ValueChangedEvent<bool> e)
    {
        if (e.NewValue) // e.NewValue == Muted
        {
            Track.UnregisterEvents(_metronome);
            _metronome = null;
        }
        else
        {
            _metronome = new Metronome(this, Util.DrumGame.Drumset.Value);
            Track.RegisterEvents(_metronome);
        }
    }

    public class PlayerState
    {
        public double Time;
        public bool Running;
    }
    public virtual PlayerState ExportState() => new()
    {
        Time = Track.CurrentTime,
        Running = Track.IsRunning
    };
    public virtual new void LoadState(PlayerState state)
    {
        Track.Seek(state.Time);
        if (state.Running) Track.Play();
        else Track.Stop();
    }
}
