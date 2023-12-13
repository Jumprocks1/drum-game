using System.Collections.Generic;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Midi;

public class MidiView : CompositeDrawable, IHasOverlayModalConfig
{
    public Colour4 ModalBackgroundColor => new(0, 0, 0, 200);
    public MidiView()
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
            DrumMidiHandler.UpdateInputConnection(false, true);
            UpdateText();
            nextRefresh = Clock.CurrentTime + RefreshMs;
        }
        base.Update();
    }

    bool OnMidiNote(MidiNoteOnEvent e)
    {
        Schedule(() =>
        {
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
                    Texts[i].Y = Spacing * 3 + 25 + 20 + bigFont + Spacing + (smallFont + Spacing) * i;
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
                Y = Spacing * 3 + 25 + 20,
                X = Spacing,
                Font = FrameworkFont.Condensed.With(size: bigFont)
            };
            AddInternal(Texts[0]);
        });
        return true;
    }

    protected override void Dispose(bool isDisposing)
    {
        DrumMidiHandler.RemoveNoteHandler(OnMidiNote, true);
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }
}