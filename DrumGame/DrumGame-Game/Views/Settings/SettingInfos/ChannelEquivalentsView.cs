using System;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Interfaces;
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

public class ChannelEquivalentsView : ModalBase, IModal
{
    public Action CloseAction { get; set; }
    void Close() => CloseAction?.Invoke();
    DrumScrollContainer ScrollContainer;
    [Resolved] DrumGameConfigManager ConfigManager { get; set; }
    Bindable<ChannelEquivalents> Bindable => ConfigManager.ChannelEquivalents;

    public ChannelEquivalentsView()
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
        inner.Add(new SpriteText
        {
            Text = "Editing Channel Equivalents",
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Font = FrameworkFont.Regular.With(size: 40),
            Y = 5
        });
        inner.Add(new DrumButton
        {
            Y = 50,
            Text = "Add",
            Height = 30,
            Width = 100,
            Action = () =>
            {
                Bindable.Value.Add(DrumChannel.None, DrumChannel.None);
                Bindable.TriggerChange();
            }
        });
        inner.Add(new DrumButton
        {
            Y = 50,
            Text = "Reset to Default",
            Height = 30,
            Width = 150,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Action = () =>
            {
                Bindable.Value.ResetToDefault();
                Bindable.TriggerChange();
            }
        });
        inner.Add(new Container
        {
            RelativeSizeAxes = Axes.Both,
            Padding = new MarginPadding { Top = 80 },
            Child = ScrollContainer = new DrumScrollContainer
            {
                RelativeSizeAxes = Axes.Both
            }
        });
        AddInternal(inner);
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Bindable.BindValueChanged(OnChange, true);
    }

    protected override void Dispose(bool isDisposing)
    {
        Bindable.ValueChanged -= OnChange;
        base.Dispose(isDisposing);
    }

    void OnChange(ValueChangedEvent<ChannelEquivalents> _) => UpdateView();
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
        Bindable<DrumChannel> Input;
        Bindable<DrumChannel> Map;
        public Row(ChannelEquivalentsView parent, (DrumChannel, DrumChannel) equiv)
        {
            Height = 30;
            Input = new Bindable<DrumChannel>(equiv.Item1);
            Map = new Bindable<DrumChannel>(equiv.Item2);
            Input.BindValueChanged(e =>
            {
                parent.Bindable.Value.Replace(e.OldValue, Map.Value, e.NewValue, Map.Value);
                parent.Bindable.TriggerChange();
            });
            Map.BindValueChanged(e =>
            {
                parent.Bindable.Value.Replace(Input.Value, e.OldValue, Input.Value, e.NewValue);
                parent.Bindable.TriggerChange();
            });
            AddInternal(new EnumAutocomplete<DrumChannel>(Input)
            {
                Width = 200,
                Height = 30,
            });
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
        }
    }
}