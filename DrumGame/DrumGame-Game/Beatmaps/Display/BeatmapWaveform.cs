using System;
using System.IO;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Commands;
using DrumGame.Game.Notation;
using DrumGame.Game.Stores;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Beatmaps.Display;

public class BeatmapWaveform : CompositeDrawable
{
    long? targetTempo;
    Stream stream;
    WaveformGraph graph;
    Waveform waveform;
    MusicFont Font => Display.Font;
    Beatmap Beatmap => Display.Beatmap;
    readonly MusicNotationBeatmapDisplay Display;
    BeatClock Track => Display.Track;
    Box cursor;
    float _staffHeight = 0;
    public float StaffHeight
    {
        get => _staffHeight; set
        {
            _staffHeight = value;
            if (value > 0)
            {
                UpdateScaling();
            }
        }
    }
    bool SnapCursor = false;
    public BeatmapWaveform(MusicNotationBeatmapDisplay display)
    {
        Display = display;
        RelativeSizeAxes = Axes.X;
        Height = 200;
        AddInternal(cursor = new Box
        {
            Width = 6,
            Colour = Colour4.CornflowerBlue.MultiplyAlpha(0.5f),
            Origin = Anchor.TopCentre,
            RelativeSizeAxes = Axes.Y,
            Depth = -1
        });
        stream = Util.Resources.GetStream(Display.Player.CurrentAudioPath);
        Beatmap.TempoUpdated += UpdateScaling;
        // Console.WriteLine(ManagedBass.Bass.CurrentDevice); // seems like if this is 0, waveform won't load
        waveform = new Waveform(stream);
        Util.CommandController.RegisterHandlers(this);
    }

    public double TimeToPixel() => (double)_staffHeight * Font.Spacing / 4 * 1000 / (targetTempo ?? Beatmap.ChangeAt<TempoChange>(-1).MicrosecondsPerQuarterNote);

    [CommandHandler]
    public void SetWaveformTempo()
    {
        // not sure if we have a better way to get current tempo
        targetTempo = Beatmap.ChangeAt<TempoChange>(Track.CurrentBeat).MicrosecondsPerQuarterNote;
        UpdateScaling();
    }

    protected override void Update()
    {
        var targetSnapCursor = Display.SnapIndicator;
        if (targetSnapCursor != SnapCursor)
        {
            SnapCursor = targetSnapCursor;
            cursor.Colour = (SnapCursor ? Colour4.PaleVioletRed : Colour4.CornflowerBlue).MultiplyAlpha(0.5f);
            if (!SnapCursor) cursor.X = 0;
        }
        if (graph != null)
            graph.X = -(float)(Track.CurrentTime * TimeToPixel());
        if (SnapCursor && Display.Player is BeatmapEditor ed)
        {
            var snapTarget = Beatmap.MillisecondsFromBeat(ed.SnapTarget);
            cursor.X = (float)((snapTarget - Track.CurrentTime) * TimeToPixel());
        }
        base.Update();
    }

    public void UpdateScaling()
    {
        if (graph == null)
        {
            AddInternal(graph = new WaveformGraph
            {
                Waveform = waveform,
                BaseColour = new Colour4(0, 0, 0, 255),
                LowColour = new Colour4(0, 0, 255, 255),
                MidColour = new Colour4(255, 0, 0, 255),
                HighColour = new Colour4(0, 255, 0, 255),
            });
        }
        graph.Width = (float)(Track.EndTime * TimeToPixel());
        graph.Height = 200;
        graph.Resolution = 1;
    }

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        stream?.Dispose();
        waveform.Dispose();
        Beatmap.TempoUpdated -= UpdateScaling;
        base.Dispose(isDisposing);
    }
}
