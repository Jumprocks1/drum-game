using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics;

namespace DrumGame.Game.Beatmaps.Practice;

public partial class PracticeMode
{
    public void Configure()
    {
        RequestModal req = null;
        Drawable[] startBeatLabelButtons = [
            new IconButton(() => {
                var field = (StringFieldConfig.StringField)req.GetField(nameof(StartBeat));
                if (double.TryParse(field.Value, out var v)) {
                    var m = Beatmap.MeasureFromBeat(v);
                    v += Beatmap.BeatFromMeasure(m - 1) - Beatmap.BeatFromMeasure(m);
                    field.Value = v.ToString();
                }
            }, FontAwesome.Solid.MinusCircle, 20) { MarkupTooltip = "Subtract measure" },
            new IconButton(() => {
                var field = (StringFieldConfig.StringField)req.GetField(nameof(StartBeat));
                if (double.TryParse(field.Value, out var v)) {
                    var m = Beatmap.MeasureFromBeat(v);
                    v += Beatmap.BeatFromMeasure(m + 1) - Beatmap.BeatFromMeasure(m);
                    field.Value = v.ToString();
                    var endField = (StringFieldConfig.StringField)req.GetField(nameof(EndBeat));
                    if (double.TryParse(endField.Value, out var v2) && v >= v2) {
                        endField.Value = (v + 4).ToString();
                    }
                }
            }, FontAwesome.Solid.PlusCircle, 20) { MarkupTooltip = "Add measure" }
        ];
        Drawable[] endBeatLabelButtons = [
            new IconButton(() => {
                var field = (StringFieldConfig.StringField)req.GetField(nameof(EndBeat));
                if (double.TryParse(field.Value, out var v)) {
                    var m = Beatmap.MeasureFromBeat(v);
                    v += Beatmap.BeatFromMeasure(m - 1) - Beatmap.BeatFromMeasure(m);
                    field.Value = v.ToString();
                    var startField = (StringFieldConfig.StringField)req.GetField(nameof(StartBeat));
                    if (double.TryParse(startField.Value, out var v2) && v <= v2) {
                        startField.Value = (v - 4).ToString();
                    }
                }
            }, FontAwesome.Solid.MinusCircle, 20) { MarkupTooltip = "Subtract measure" },
            new IconButton(() => {
                var field = (StringFieldConfig.StringField)req.GetField(nameof(EndBeat));
                if (double.TryParse(field.Value, out var v)) {
                    var m = Beatmap.MeasureFromBeat(v);
                    v += Beatmap.BeatFromMeasure(m + 1) - Beatmap.BeatFromMeasure(m);
                    field.Value = v.ToString();
                }
            }, FontAwesome.Solid.PlusCircle, 20) { MarkupTooltip = "Add measure" }
        ];


        Util.Palette.Close<RequestModal>();
        req = Util.Palette.Request(new RequestConfig
        {
            Fields = [
                new NumberFieldConfig { Label = "Lead-in", RefN = () => ref Config.LeadInBeats,
                    MarkupTooltip = "Practice mode will start the song this many beats before the actual practice section.\nThis gives you time to prepare before the scoring starts." },
                new NumberFieldConfig{ Label = "Starting Speed", RefN = () => ref Config.StartRatePercent,
                    MarkupTooltip = $"Speed used when first entering practice mode\n\nTo change the current speed, use one of these commands:\n"
                    + $"{IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.SetPlaybackSpeed)}\n"
                    + $"{IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.SetPlaybackBPM)}\n"
                    + $"{IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.DecreasePlaybackSpeed)}\n"
                    + $"{IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.IncreasePlaybackSpeed)}" },
                new NumberFieldConfig{ Label = "Speed Increment", RefN = () => ref Config.RateIncrementPercent,
                    MarkupTooltip = "How much the speed changes after playing the section successfully" },
                new IntFieldConfig{ Label = "Minimum Streak", RefN = () => ref Config.MinimumStreak, MarkupTooltip =
                        "The speed will only increase when you have successfully completed\n" +
                        "the section at least this many times."},
                new EnumFieldConfig<PracticeMetronomeSetting> { Label = "Metronome", DefaultValue = Config.Metronome,
                    OnCommit = e => { if (Config.Metronome != e) { Config.Metronome = e; RefreshMetronome(); } } },
                new NumberFieldConfig { Label = "Metronome Volume", RefN = () => ref Config.MetronomeVolumePercent },
                new NumberFieldConfig {
                    Label = "Start Beat", DefaultValue = StartBeat, OnCommit = v => {
                        if (v is double d) StartBeat = d;
                    }, LabelButtons = startBeatLabelButtons, Key = nameof(StartBeat)
                },
                new NumberFieldConfig {
                    Label = "End Beat", DefaultValue = EndBeat, OnCommit = v => {
                        if (v is double d) EndBeat = d;
                    }, LabelButtons = endBeatLabelButtons, Key = nameof(EndBeat)
                },
                new NumberFieldConfig { Label = "Target Accuracy", RefN = () => ref Config.TargetAccuracyPercent, MarkupTooltip = "To pass, you need this accuracy or higher" },
                new IntFieldConfig { Label = "Target Misses", RefN = () => ref Config.TargetMisses, MarkupTooltip = "To pass, you need this many misses or fewer" },
            ],
            Title = "Configuring Practice Mode",
            OnCommit = e =>
            {
                PracticeInfoPanel.UpdateText();
                UpdateOverlay();
                Track.SeekToBeat(StartBeat - Config.LeadInBeats);
                Config.InvokeChanged();
            },
            Footer = new DrumButton
            {
                AutoSize = true,
                Text = "Exit Practice Mode",
                Action = () =>
                {
                    Player.Mode = BeatmapPlayerMode.Playing;
                    req.Close();
                }
            }
        });
    }
}