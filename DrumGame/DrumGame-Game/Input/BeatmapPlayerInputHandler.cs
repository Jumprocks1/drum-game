using System;
using System.Collections.Generic;
using DrumGame.Game.Media;
using DrumGame.Game.Beatmaps.Replay;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Midi;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using DrumGame.Game.Beatmaps;
using osu.Framework.Input.Events;
using osu.Framework.Threading;
using osu.Framework.Development;
using System.Diagnostics;

namespace DrumGame.Game.Input;

// Allows a user to play a beatmap
public class BeatmapPlayerInputHandler : IDisposable
{
    public List<IReplayEvent> Events = new(); // doesn't need to be sorted, will be sorted later
    DrumsetAudioPlayer Drumset;
    readonly BeatmapPlayer Player;
    public BeatmapScorer Scorer;
    GameHost Host => Drumset.Host;
    DrumChannelInputHandler InputHandler;
    public ReplayInfo BuildReplay() => Scorer.ReplayInfo.Clone();

    public BeatmapPlayerInputHandler(BeatmapPlayer player)
    {
        Player = player;
        if (Player != null)
        {
            Scorer = new BeatmapScorer(Player.Beatmap, Player.Track);
            Player.Track.AfterSeek += AfterSeek;
            Scorer.OnScoreEvent += e => // don't need to unsubscribe since Scorer will die before us
            {
                Player.Display?.DisplayScoreEvent(e);
            };
        }
        InputHandler = new DrumChannelInputHandler(TriggerEvent, Player.Track);

        Drumset = Util.DrumGame.Drumset.Value;

        DrumMidiHandler.AddNoteHandler(InputHandler.OnMidiNote, true, true);
        DrumMidiHandler.AddAuxHandler(OnMidiAux);
    }

    public void AfterSeek(double position)
    {
        Debug.Assert(ThreadSafety.IsUpdateThread);
        if (Scorer.AfterSeek(position)) Events.Clear();
        // don't expect this to remove all events that occur after the seek point if the seek is triggered by a timestamp
        // track time is based on the audio thread, but the delayed events are queued on the update thread
        // fortunately, even if it triggers, the effects of that trigger would occur on the track prior to the seek
        // for example, if a loop or practice section has a hit at the very end, it may still trigger.
        // if that happens, it will trigger on the state before the seek
        // in practice mode, this should result in an ignore event occuring right before the seek
        // this behavior can be tested by subtracting a few ms from the delay when queuing on the update thread
        foreach (var ev in QueuedEvents) ev.Cancel();
        QueuedEvents.Clear();
    }

    bool OnMidiAux(MidiAuxEvent ev)
    {
        var trackTime = Player.Track.AbsoluteTime - Util.ConfigManager.MidiInputOffset.Value * Player.Track.EffectiveRate;
        Events.Add(new BeatmapReplay.ReplayAuxEvent(trackTime, ev));
        return true;
    }
    public bool Handle(KeyDownEvent ev) => InputHandler.Handle(ev);

    public void Dispose()
    {
        OnTrigger = null;
        DrumMidiHandler.RemoveNoteHandler(InputHandler.OnMidiNote);
        InputHandler.Dispose();
        InputHandler = null;
        DrumMidiHandler.RemoveAuxHandler(OnMidiAux);
        Scorer?.Dispose();
        if (Player != null) Player.Track.AfterSeek -= AfterSeek;
    }

    // Use for "live" events (not replays or auto-playback)
    public void TriggerEvent(DrumChannelEvent ev) => TriggerEvent(ev, true);

    public event Action<DrumChannelEvent> OnTrigger;

    // Triggered when a DrumChannel is fired (either by keyboard or MIDI or mouse)
    // Can pass in a time to override track time (for auto player/replays)
    public void TriggerEvent(DrumChannelEvent ev, bool playAudio)
    {
        // ignore events that are queued during a seek
        if (ev.TimeVersion != 0 && ev.TimeVersion != Player.Track.TimeVersion) return;
        var channel = ev.Channel;
        if (Player != null)
        {
            // should probably allow this to be rebound, but not certain how to do that
            // I think we can just expose a method on the command controller to check if the DrumChannelEvent is bound to `CloseEndScreen`
            if ((ev.Channel == DrumChannel.Crash || ev.Channel == DrumChannel.China || ev.Channel == DrumChannel.Splash) && Player.AtEndScreen)
                Util.CommandController.ActivateCommand(Commands.Command.Close);
            else
            {
                Events.Add(ev);
                Scorer?.Hit(ev);
            }
        }
        // don't want to play hit sample if the user already had samples via their MIDI device
        // we do want to play samples if it was hit with a keyboard or inside a replay
        if ((!ev.MIDI || Util.ConfigManager.PlaySamplesFromMidi.Value) && playAudio) Drumset.Play(ev);
        OnTrigger?.Invoke(ev);
    }
    List<ScheduledDelegate> QueuedEvents = new();
    public void TriggerEventDelayed(DrumChannelEvent ev)
    {
        var trackTime = Player.Track.AbsoluteTime;
        if (ev.Time > trackTime)
        {
            var delay = (ev.Time - trackTime) / Player.Track.PlaybackSpeed.Value;
            ScheduledDelegate s = null;
            s = Host.UpdateThread.Scheduler.AddDelayed(() =>
            {
                QueuedEvents.Remove(s);
                TriggerEvent(ev, false);
            }, delay);
            QueuedEvents.Add(s);
            // triggering runs on update thread, but we can get better timing for replays + automatic playback by queueing on the audio thread
            if (!ev.MIDI) Drumset.PlayAt(ev, Player.Track, ev.Time);
        }
        else TriggerEvent(ev);
    }
    public void SendMidiBytesDelayed(byte[] bytes, double time)
    {
        var ev = new DrumChannelEvent(time, DrumChannel.None, 0)
        {
            MidiControl = bytes,
            MIDI = true
        };
        Drumset.PlayAt(ev, Player.Track, time);
    }

    public void Update()
    {
        Scorer?.Update();
    }
}
