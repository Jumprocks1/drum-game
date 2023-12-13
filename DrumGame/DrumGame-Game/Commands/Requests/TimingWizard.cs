using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Graphics3D.View;
using DrumGame.Game.Modals;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace DrumGame.Game.Commands.Requests;

public class TimingWizard : RequestModal
{
    const int BeatsPerMeasure = 4;
    public List<(double beat, double time)> userBeats = new();
    int nextIndex = 0;
    bool extrapolateIndex = true;

    double offset = 0; // based on regression
    double OffsetDisplay => subtractMeasuresFromOffset.Current.Value ?
        Util.Mod(offset, (BeatsPerMeasure * slope)) : offset; // can have measure offsets
    double slope = 500;
    double bpm => 60000 / slope;

    // can't use beat clock since that relies on timing already existing
    TrackClock Track => Editor.Track;
    Beatmap Beatmap => Editor.Beatmap;
    BeatmapEditor Editor;
    public TimingWizard(BeatmapEditor editor) : base(new RequestConfig
    {
        Title = "Timing Wizard",
        OnCommit = e => ((TimingWizard)e).OnCommit(),
        CommitText = "Apply"
    })
    {
        Editor = editor;
    }

    List<Drawable> BeatView = new();

    FillFlowContainer body;
    SpriteText bpmText;
    SpriteText offsetText;
    TooltipSpriteText measureDuration;
    DrumCheckbox subtractMeasuresFromOffset;

    ErrorGraph Graph;

    [BackgroundDependencyLoader]
    private void load()
    {
        Add(new Box
        {
            Height = 500,
            RelativeSizeAxes = Axes.X,
            Alpha = 0
        });
        Add(body = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            Direction = FillDirection.Vertical,
            AutoSizeAxes = Axes.Y
        });
        body.Add(new SpriteText
        {
            Text = "To start timing, start the song and tap Q on each beat."
        });
        body.Add(bpmText = new TooltipSpriteText("~95% confidence"));
        body.Add(offsetText = new TooltipSpriteText("~95% confidence"));
        body.Add(measureDuration = new TooltipSpriteText());
        body.Add(subtractMeasuresFromOffset = new DrumCheckbox { LabelText = "Subtract Measures From Offset" });
        subtractMeasuresFromOffset.Current.Value = true;
        subtractMeasuresFromOffset.Current.ValueChanged += _ => UpdateValue();
        var smallSize = 15;
        var largeSize = 25;
        var spacing = 5;
        var beatContainer = new Container
        {
            Width = largeSize * BeatsPerMeasure + spacing * (BeatsPerMeasure - 1),
            Height = largeSize,
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Margin = new MarginPadding(5)
        };
        for (var i = 0; i < BeatsPerMeasure; i++)
        {
            var circle = new Circle
            {
                X = i * (largeSize + spacing) + (i == 0 ? 0 : (largeSize - smallSize) / 2),
                Y = i == 0 ? 0 : (largeSize - smallSize) / 2,
                Width = i == 0 ? largeSize : smallSize,
                Height = i == 0 ? largeSize : smallSize,
                Alpha = 0
            };
            BeatView.Add(circle);
            beatContainer.Add(circle);
        }
        body.Add(beatContainer);
        body.Add(Graph = new ErrorGraph
        {
            RelativeSizeAxes = Axes.X,
            Height = 300,
        });
        AddFooterButton(new CommandButton(Command.OffsetWizard)
        {
            Text = "Offset Wizard",
            Width = 120,
            Height = 30
        });
    }

    void OnCommit()
    {
        var oldOffset = Beatmap.StartOffset;
        var newOffset = OffsetDisplay;
        var newTempo = new Tempo { MicrosecondsPerQuarterNote = (int)Math.Round(slope * 1000) };
        var oldTiming = Beatmap.TempoChanges;
        Editor.PushChange(() =>
        {
            Beatmap.StartOffset = newOffset;
            Beatmap.TempoChanges = new List<TempoChange> { new TempoChange(0, newTempo) };
            Beatmap.FireTempoUpdated();
        }, () =>
          {
              Beatmap.StartOffset = oldOffset;
              Beatmap.TempoChanges = oldTiming;
              Beatmap.FireTempoUpdated();
          }, $"set offset to {newOffset} and BPM to {newTempo.HumanBPM}");
    }

    public void UpdateBeatVisiblity(int beat)
    {
        beat %= BeatsPerMeasure;
        for (var i = 0; i < BeatsPerMeasure; i++) BeatView[i].Alpha = i <= beat ? 1 : 0;
    }

    public void UpdateValue()
    {
        if (userBeats.Count == 0) return;
        if (userBeats.Count == 1)
        {
            offset = userBeats[0].time;
            slope = 500;
        }
        else
        {
            var reg = Statistics.LinearRegression(userBeats);
            offset = reg.Intercept;
            slope = reg.Slope;

            var bpmLow = 60000 / (reg.Slope + reg.SlopeError * 2);
            var bpmHigh = 60000 / (reg.Slope - reg.SlopeError * 2);
            bpmText.Text = $"BPM: {bpm:0.00} (\u00b1{(bpmHigh - bpmLow) / 2:0.00})";
            offsetText.Text = $"Offset: {OffsetDisplay:0.00} (\u00b1{reg.InterceptError * 2:0.00})";
            measureDuration.Text = $"Measure duration: {slope * BeatsPerMeasure:0.00}";
            measureDuration.Tooltip = $"{slope:0.00} per beat";
        }
    }

    public void UserBeat()
    {
        var time = Track.AbsoluteTime;
        if (extrapolateIndex && userBeats.Count >= 2)
        {
            nextIndex = (int)Math.Round((Track.AbsoluteTime - offset) / slope);
        }
        userBeats.Add((nextIndex, time));
        UpdateBeatVisiblity(nextIndex);
        nextIndex += 1;
        UpdateValue();
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key == Key.Q)
        {
            UserBeat();
            return true;
        }
        return base.OnKeyDown(e);
    }

    protected override void Update()
    {
        Graph.UpdateData(userBeats, Track.CurrentTime, slope, offset);
        base.Update();
    }
}