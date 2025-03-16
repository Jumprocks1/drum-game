using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Midi;

public class MidiMonitor : CompositeDrawable, IHasOverlayModalConfig
{
    public Colour4 ModalBackgroundColor => new(0, 0, 0, 200);
    public MidiMonitor()
    {
        DrumMidiHandler.AddNoteHandler(OnMidiNote, true);
        RelativeSizeAxes = Axes.Both;
    }

    const float bigFont = 30;
    const float smallFont = 18;
    const float Spacing = 3;

    List<SpriteText> Texts = new();

    SpriteText DeviceText;

    [BackgroundDependencyLoader]
    private void load()
    {
        Util.CommandController.RegisterHandlers(this);
        AddInternal(new SpriteText
        {
            Text = "Press MIDI notes to see their current mapping info",
            X = Spacing,
            Y = Spacing,
            Font = FrameworkFont.Regular.With(size: 25),
        });
        AddInternal(DeviceText = new SpriteText
        {
            X = Spacing,
            Y = Spacing * 2 + 25,
            Font = FrameworkFont.Regular.With(size: 20),
        });
        AddInternal(new DrumButton
        {
            Text = "Set MIDI Input/Output Devices",
            AutoSize = true,
            Action = () =>
            {
                DrumMidiHandler.UpdateInputConnectionAsync(false, true).Wait(100);
                DrumMidiHandler.UpdateOutputConnectionAsync().Wait(100);
                UpdateText();
                var midiInput = Util.ConfigManager.GetBindable<string>(Stores.DrumGameSetting.PreferredMidiInput);
                var preferredInput = string.IsNullOrWhiteSpace(midiInput.Value) ? null : midiInput.Value;
                preferredInput ??= DrumMidiHandler.Input?.Details?.Name;
                var inputs = DrumMidiHandler.Inputs;
                var inputField = AutocompleteFieldConfig.FromOptions(DrumMidiHandler.Inputs.Select(e => e.Name), preferredInput);
                inputField.Label = "Input";
                inputField.MarkupTooltip = "Used for passing MIDI inputs into the game for scoring.\nRequired for standard gameplay.";

                var midiOutput = Util.ConfigManager.GetBindable<string>(Stores.DrumGameSetting.PreferredMidiOutput);
                var preferredOutput = string.IsNullOrWhiteSpace(midiOutput.Value) ? null : midiOutput.Value;
                preferredOutput ??= DrumMidiHandler.Output?.Details?.Name ?? preferredInput;
                var outputs = DrumMidiHandler.Outputs;
                var outputField = AutocompleteFieldConfig.FromOptions(DrumMidiHandler.Outputs.Select(e => e.Name)
                    .Append(DrumMidiHandler.Disabled), preferredOutput);
                outputField.Label = "Output";
                outputField.MarkupTooltip = "Allows hearing replays and autoplay through a MIDI device (instead of the in-game hitsounds).\nNot used during standard gameplay.";

                var req = Util.Palette.Request(new RequestConfig
                {
                    Title = "Setting MIDI Devices",
                    CommitText = "Save",
                    AutoFocus = false,
                    OnCommit = e =>
                    {
                        midiInput.Value = e.GetValue<BasicAutocompleteOption>(0).Name;
                        midiOutput.Value = e.GetValue<BasicAutocompleteOption>(1).Name;
                        DrumMidiHandler.UpdateInputConnectionAsync(false, true).Wait(100);
                        DrumMidiHandler.UpdateOutputConnectionAsync().Wait(100);
                        UpdateText();
                    },
                    Fields = [inputField, outputField]
                });
                if (DrumMidiHandler.Outputs.Count == 0 && DrumMidiHandler.Inputs.Count == 0)
                    req.AddWarning("Warning: No MIDI devices found");
            },
            Y = DeviceText.Y + 20
        });

        var textBox = new DrumTextBox
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Width = 100,
            Height = 30
        };
        AddInternal(new SpriteText
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -100,
            Font = FrameworkFont.Regular.With(size: 30),
            Text = "Program:"
        });
        textBox.OnCommit += (_, __) =>
        {
            DrumMidiHandler.SendEvent(new DrumMidiHandler.ProgramChangeEvent((byte)(byte.Parse(textBox.Current.Value) - 1)));
        };
        AddInternal(textBox);
        UpdateText();
    }

    void UpdateText()
    {
        DeviceText.Text = "Current MIDI connections - " + DrumMidiHandler.DeviceString;
    }

    double nextRefresh = 0; // will cause immediate refresh
    const double RefreshMs = 500; // ms for each refresh. 500ms => 2/s
    protected override void Update()
    {
        if (Clock.CurrentTime > nextRefresh)
        {
            DrumMidiHandler.UpdateInputConnectionAsync(false, true).Wait(100);
            UpdateText();
            nextRefresh = Clock.CurrentTime + RefreshMs;
        }
        base.Update();
    }

    bool OnMidiNote(MidiNoteOnEvent e)
    {
        Schedule(() =>
        {
            // this layout code is awful
            Texts.Add(null);
            for (var i = Texts.Count - 2; i >= 0; i--)
            {
                if (i >= 10)
                {
                    RemoveInternal(Texts[i], true);
                    Texts.RemoveAt(i);
                }
                else
                {
                    if (i == 0) Texts[i].Font = FrameworkFont.Condensed.With(size: smallFont);
                    Texts[i].Y = Spacing * 3 + 25 + 20 + 30 + bigFont + Spacing + (smallFont + Spacing) * i;
                    Texts[i + 1] = Texts[i];
                }
            }
            var channel = e.DrumChannel;
            var hiHat = channel == Channels.DrumChannel.ClosedHiHat || channel == Channels.DrumChannel.HalfOpenHiHat || channel == Channels.DrumChannel.OpenHiHat;
            var text = $"MIDI {e.Note} ({e.MidiKey}) vel: {e.VelocityString} mapped channel: {channel}";
            if (hiHat) text += $" pedal: {e.HiHatControl}";
            Texts[0] = new SpriteText
            {
                Text = text,
                Y = Spacing * 3 + 25 + 20 + 30,
                X = Spacing,
                Font = FrameworkFont.Condensed.With(size: bigFont)
            };
            AddInternal(Texts[0]);
        });
        return true;
    }

    protected override void Dispose(bool isDisposing)
    {
        DrumMidiHandler.RemoveNoteHandler(OnMidiNote);
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }
}