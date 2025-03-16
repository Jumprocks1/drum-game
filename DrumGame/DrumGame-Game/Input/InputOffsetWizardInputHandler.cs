using System;
using System.Linq;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Media;
using DrumGame.Game.Midi;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Input;

public class InputOffsetWizardInputHandler
{
    DrumsetAudioPlayer Drumset;
    DrumChannelInputHandler InputHandler;
    TrackClock Track => InputHandler.Track;
    public InputOffsetWizardInputHandler(TrackClock track)
    {
        InputHandler = new DrumChannelInputHandler(TriggerEvent, track);
        Drumset = Util.DrumGame.Drumset.Value;

        DrumMidiHandler.AddNoteHandler(InputHandler.OnMidiNote, true);
    }
    public bool Handle(KeyDownEvent ev) => InputHandler.Handle(ev);
    public void Dispose()
    {
        DrumMidiHandler.RemoveNoteHandler(InputHandler.OnMidiNote);
        OnTrigger = null;
        InputHandler.Dispose();
        InputHandler = null;
    }
    public event Action<DrumChannelEvent> OnTrigger;
    public void TriggerEvent(DrumChannelEvent ev) => TriggerEvent(ev, true);
    public void TriggerEvent(DrumChannelEvent ev, bool playAudio)
    {
        if (!ev.MIDI && playAudio)
        {
            var currentBeatmap = Util.SelectorLoader?.CurrentBeatmap;
            if (currentBeatmap != null)
            {
                ev.CurrentBeatmap = currentBeatmap;
                ev.HitObject = currentBeatmap.HitObjects.FirstOrDefault(e => e.Channel == ev.Channel);
            }
            Drumset.Play(ev);
        }
        OnTrigger?.Invoke(ev);
    }
}