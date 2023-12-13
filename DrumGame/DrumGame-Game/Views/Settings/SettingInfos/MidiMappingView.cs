using System;
using DrumGame.Game.Channels;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Input;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Midi;
using DrumGame.Game.Modals;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class MidiMappingView : ModalBase, IModal
{
    public Action CloseAction { get; set; }
    void Close() => CloseAction?.Invoke();

    DrumScrollContainer ScrollContainer;
    [Resolved] DrumGameConfigManager ConfigManager { get; set; }
    BindableMidiMapping Bindable => ConfigManager.MidiMapping;
    MidiMapping Mapping => Bindable.Value;

    public MidiMappingView()
    {
        AddInternal(new ModalBackground(Close));
        var inner = new ClickBlockingContainer
        {
            RelativeSizeAxes = Axes.Both,
            Width = 0.8f,
            Height = 0.9f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre
        };
        inner.Add(new Box
        {
            Colour = DrumColors.DarkBackground,
            RelativeSizeAxes = Axes.Both
        });
        inner.Add(new CommandIconButton(Commands.Command.ViewMidi, FontAwesome.Solid.Music, 30)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = 8,
            X = -8
        });
        inner.Add(new SpriteText
        {
            Text = "Editing MIDI Mapping",
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Font = FrameworkFont.Regular.With(size: 40),
            Y = 5
        });
        inner.Add(new SpriteText
        {
            Text = "To start, hit the trigger that you want to remap",
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Font = FrameworkFont.Regular.With(size: 20),
            Y = 45
        });
        inner.Add(new DrumButton
        {
            Y = 70,
            Text = "Add",
            Height = 30,
            Width = 100,
            Action = () =>
            {
                Bindable.Value.Add(0, DrumChannel.None);
                Bindable.TriggerChange();
            }
        });
        inner.Add(new DrumButton
        {
            Y = 70,
            Text = "Clear",
            Height = 30,
            Width = 150,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Action = () =>
            {
                Bindable.Value.Clear();
                Bindable.TriggerChange();
            }
        });
        inner.Add(new Container
        {
            RelativeSizeAxes = Axes.Both,
            Padding = new MarginPadding { Top = 100 },
            Child = ScrollContainer = new DrumScrollContainer
            {
                RelativeSizeAxes = Axes.Both
            }
        });
        AddInternal(inner);
    }

    bool OnMidiNote(MidiNoteOnEvent ev)
    {
        Schedule(() =>
        {
            var hasMapping = Mapping.HasMappingOverride(ev.Note);
            if (!hasMapping)
            {
                Mapping.Add(ev.Note, ChannelMapping.MidiMapping(ev.Note));
                UpdateView();
            }
            foreach (var e in ScrollContainer.Children)
            {
                var row = (Row)e;
                if (row.Input.Value == ev.Note)
                {
                    row.Flash();
                    break;
                }
            }
        });
        return true;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        DrumMidiHandler.AddNoteHandler(OnMidiNote, true);
        Bindable.BindValueChanged(OnChange, true);
    }

    protected override void Dispose(bool isDisposing)
    {
        DrumMidiHandler.RemoveNoteHandler(OnMidiNote, true);
        Bindable.ValueChanged -= OnChange;
        base.Dispose(isDisposing);
    }

    void OnChange(ValueChangedEvent<MidiMapping> e) => UpdateView();
    public void UpdateView()
    {
        ScrollContainer.Clear();
        var y = 0;
        foreach (var pair in Bindable.Value)
        {
            ScrollContainer.Add(new Row(this, pair) { Y = y, Depth = y });
            y += 30;
        }
    }

    class Row : CompositeDrawable
    {

        public Bindable<byte> Input;
        public Bindable<DrumChannel> Map;
        Box background;
        new MidiMappingView Parent;
        public void Flash()
        {
            Parent.ScrollContainer.ScrollIntoView(this);
            background.FlashColour(Colour4.PaleGreen, 500, Easing.OutQuint);
        }
        public Row(MidiMappingView parent, (byte, DrumChannel) mapping)
        {
            Parent = parent;
            Height = 30;
            AutoSizeAxes = Axes.X;
            Input = new Bindable<byte>(mapping.Item1);
            Map = new Bindable<DrumChannel>(mapping.Item2);
            Input.BindValueChanged(e =>
            {
                Parent.Bindable.Value.Replace(e.OldValue, e.NewValue, Map.Value);
                Parent.Bindable.TriggerChange();
            });
            Map.BindValueChanged(e =>
            {
                Parent.Bindable.Value.Replace(Input.Value, Input.Value, e.NewValue);
                Parent.Bindable.TriggerChange();
            });
            AddInternal(background = new Box
            {
                Colour = Colour4.Transparent,
                RelativeSizeAxes = Axes.Both
            });
            var textBox = new DrumTextBox
            {
                Width = 200,
                Height = 30,
                Text = mapping.Item1.ToString()
            };
            AddInternal(textBox);
            textBox.OnCommit += (e, _) =>
            {
                if (byte.TryParse(e.Current.Value, out var o)) Input.Value = o;
                textBox.Text = Input.Value.ToString();
            };
            AddInternal(new SpriteText
            {
                Origin = Anchor.Centre,
                X = 250,
                Y = 15,
                Text = "=>"
            });
            AddInternal(new EnumAutocomplete<DrumChannel>(Map)
            {
                Width = 200,
                Height = 30,
                X = 300
            });
            AddInternal(new IconButton(() =>
            {
                parent.Mapping.Remove(Input.Value);
                parent.UpdateView();
            }, FontAwesome.Solid.Times, 20)
            {
                X = 500,
                Margin = new MarginPadding(5)
            });
        }
    }
}