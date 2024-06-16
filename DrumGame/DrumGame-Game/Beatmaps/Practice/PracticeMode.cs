using System;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Media;
using osu.Framework.Allocation;
using DrumGame.Game.Modifiers;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using DrumGame.Game.Channels;
using DrumGame.Game.Components.Overlays;
using System.ComponentModel;
using Newtonsoft.Json;

namespace DrumGame.Game.Beatmaps.Practice;

public partial class PracticeMode
{
    public enum PracticeMetronomeSetting
    {
        // what value = 0 is the default
        LeadIn,
        Disabled,
        Always
    }
    public class PracticeConfig : IInit, IChangedEvent // should be saved to file
    {
        [DefaultValue(4d)]
        public double LeadInBeats = 4;

        [DefaultValue(75d)]
        public double StartRatePercent = 75;
        [JsonIgnore] public double StartRate => StartRatePercent / 100;

        [DefaultValue(5d)]
        public double RateIncrementPercent = 5;
        [JsonIgnore] public double RateIncrement => RateIncrementPercent / 100;

        [DefaultValue(3)]
        public int MinimumStreak = 3;
        [DefaultValue(0.8f)]
        public float OverlayStrength = 0.8f;
        [DefaultValue(95d)]
        public double TargetAccuracyPercent = 95;
        public int TargetMisses;

        public PracticeMetronomeSetting Metronome;
        [DefaultValue(20d)]
        public double MetronomeVolumePercent = 20;
        [JsonIgnore] public double MetronomeVolume => MetronomeVolumePercent / 100;

        public void InvokeChanged()
        {
            Changed?.Invoke();
        }
        public event Action Changed;
        public void Init()
        {
        }
    }

    #region Basic state

    double _startBeat;
    public double StartBeat
    {
        get => _startBeat; set
        {
            _startBeat = value;
            StartTime = Beatmap.MillisecondsFromBeat(value - Beatmap.BeatEpsilon);
        }
    }
    public double StartTime;
    double _endBeat;
    public double EndBeat
    {
        get => _endBeat; set
        {
            _endBeat = value;
            EndTime = Beatmap.MillisecondsFromBeat(value - Beatmap.BeatEpsilon);
        }
    }
    public double EndTime;
    public int Streak; // negative for failures, 0 for no streak, positive for successes
    #endregion

    public PracticeConfig Config = Util.ConfigManager.PracticeConfig.Value;
    public BeatmapDisplay Display;
    public Beatmap Beatmap => Display.Beatmap;
    public BeatClock Track => Player.Track;
    public BeatmapPlayer Player => Display.Player;
    public BeatmapScorer Scorer => Player.BeatmapPlayerInputHandler?.Scorer;
    public PracticeMode(BeatmapDisplay display, double start, double end)
    {
        Init(display, start, end);
    }
    void Init(BeatmapDisplay display, double start, double end)
    {
        Display = display;
        StartBeat = start;
        EndBeat = end;
        if (display is MusicNotationBeatmapDisplay notation)
            notation.ClearSelection();
    }
    public PracticeMode(BeatmapDisplay display)
    {
        if (display is MusicNotationBeatmapDisplay notation &&
            notation.Selection != null && notation.Selection.IsComplete)
            Init(display, notation.Selection.Start, notation.Selection.End.Value);
        else
        {
            var currentMeasure = display.Track.CurrentMeasure;
            var beatmap = display.Beatmap;
            Init(display, beatmap.BeatFromMeasure(currentMeasure), beatmap.BeatFromMeasure(currentMeasure + 4));
        }
    }
    [CommandHandler] public void Close() => Player.TogglePracticeMode();
    [CommandHandler] public void DecreasePlaybackSpeed() => Track.Rate -= Config.RateIncrement;
    [CommandHandler] public void IncreasePlaybackSpeed() => Track.Rate += Config.RateIncrement;
    public void Update()
    {
        if (Track.CurrentTime > EndTime)
            CompleteRound();
    }
    void CompleteRound() // warning, this can dispose self
    {
        var exit = false;
        // don't update streak if we didn't hit anything
        if (Scorer != null && Scorer.ReplayInfo.AccuracyHit > 0)
        {
            var accuracyGood = Scorer.ReplayInfo.AccuracyPercent >= Config.TargetAccuracyPercent;
            var missesGood = Scorer.ReplayInfo.Miss <= Config.TargetMisses;
            var success = accuracyGood && missesGood;
            if (success)
            {
                if (Streak <= 0) Streak = 1;
                else Streak += 1;
            }
            else
            {
                if (Streak >= 0) Streak = -1;
                else Streak += -1;
            }
            if (Streak >= Config.MinimumStreak)
            {
                if (Track.Rate >= 1)
                {
                    Util.Palette.ShowMessage($"Good job. Exiting practice mode.", position: MessagePosition.Center);
                    exit = true;
                }
                else
                {
                    // we could reset streak here
                    var newRate = Math.Min(Track.Rate + Config.RateIncrement, 1);
                    Util.Palette.ShowMessage($"Good job. Increasing speed to {newRate * 100:0}%", position: MessagePosition.Center);
                    Track.Rate = newRate;
                }
            }
        }
        Track.SeekToBeat(StartBeat - Config.LeadInBeats); // could make this async
        if (exit)
        {
            // this immediately disposes ourself, be very careful
            Player.Mode = BeatmapPlayerMode.Playing;
            Scorer.SeekPracticeMode = true;
        }
        else Display.PracticeInfoPanel.UpdateText();
    }
    PracticeMetronome Metronome;
    void RefreshMetronome()
    {
        Track.UnregisterEvents(Metronome);
        Track.RegisterEvents(Metronome ??= new(this, Player.Dependencies.Get<Lazy<DrumsetAudioPlayer>>().Value));
    }
    public event Action<PracticeMode> PracticeChanged;
    public void Begin()
    {
        Logger.Log($"Starting practice for beats {StartBeat}-{EndBeat}");
        RefreshMetronome();

        Util.CommandController.RegisterHandlers(this);
        Track.Stop();
        Track.SeekToBeat(StartBeat - Config.LeadInBeats);
        Display.StartPractice(this);
        Track.Rate = Config.StartRate;

        var scorer = Scorer;
        if (scorer != null)
        {
            scorer.ResetTo(Track.CurrentTime);
            scorer.SeekPracticeMode = false;
            scorer.PracticeMode = this;
        }
    }
    PracticeInfoPanel PracticeInfoPanel => Display.PracticeInfoPanel;
    public static void AddHook(BeatmapPlayer player)
    {
        player.ModeChanged += e =>
        {
            if (e.HasFlag(BeatmapPlayerMode.Practice))
            {
                if (player.PracticeMode == null)
                {
                    player.PracticeMode = new(player.Display);
                    player.PracticeMode.Begin();
                }
            }
            else if (player.PracticeMode != null)
            {
                player.PracticeMode.Exit();
                player.PracticeMode = null;
            }
        };
    }

    public void Exit()
    {
        Track.UnregisterEvents(Metronome);
        Track.Rate = 1;
        Metronome = null;
        Util.CommandController.RemoveHandlers(this);
        if (Player.BeatmapPlayerInputHandler != null)
        {
            var scorer = Player.BeatmapPlayerInputHandler.Scorer;
            if (scorer != null)
            {
                scorer.ResetTo(Track.CurrentTime);
                scorer.PracticeMode = null;
                scorer.SeekPracticeMode = true; // prevent immediate misses
            }
        }
        Display.ExitPractice(this);
    }
}

public class PracticeMetronome(PracticeMode PracticeMode, DrumsetAudioPlayer drumset) : Metronome(PracticeMode.Player, drumset)
{
    public override DrumChannel Channel => DrumChannel.PracticeMetronome;
    public override DrumChannelEvent MakeEvent(bool measureBeat)
        => new PracticeMetronomeEvent(0, Channel, measureBeat ? (byte)1 : (byte)2)
        { Volume = PracticeMode.Config.MetronomeVolume };

    protected override double GetNextBeat(int currentMeasure, double currentBeat)
    {
        var next = base.GetNextBeat(currentMeasure, currentBeat);
        var config = PracticeMode.Config;
        if (config.Metronome == PracticeMode.PracticeMetronomeSetting.Disabled || config.MetronomeVolume == 0)
            return double.PositiveInfinity;
        if (config.Metronome == PracticeMode.PracticeMetronomeSetting.LeadIn)
        {
            // we will still trigger metronome on start beat, especially if it's a measure start
            if (next > PracticeMode.StartBeat)
                return double.PositiveInfinity;
        }
        return next;
    }
}


public class PracticeMetronomeEvent : DrumChannelEvent
{
    public double Volume;

    public PracticeMetronomeEvent(double time, DrumChannel channel, byte velocity = 92) : base(time, channel, velocity)
    {
    }
}