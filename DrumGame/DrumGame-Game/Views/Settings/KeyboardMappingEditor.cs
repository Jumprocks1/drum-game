using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Media;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Input;

namespace DrumGame.Game.Views.Settings;

public class KeyboardMappingEditor : CompositeDrawable
{
    public KeyboardMappingEditor()
    {
        RelativeSizeAxes = Axes.Both;
    }

    KB Keyboard;

    DrumsetAudioPlayer Drumset => Util.DrumGame.Drumset.Value;

    FillFlowContainer<DrumButtonTooltip> FlowContainer;

    public KeyboardMapping Mapping = Util.ConfigManager.KeyboardMapping.Value;

    [BackgroundDependencyLoader]
    private void load()
    {
        FlowContainer = new FillFlowContainer<DrumButtonTooltip>();
        FlowContainer.RelativeSizeAxes = Axes.X;
        FlowContainer.Spacing = new Vector2(5);
        FlowContainer.AutoSizeAxes = Axes.Y;
        FlowContainer.Padding = new MarginPadding(2.5f);
        foreach (var channel in Enum.GetValues<DrumChannel>())
        {
            if (channel == DrumChannel.None || channel >= DrumChannel.Metronome) continue;

            FlowContainer.Add(new DrumButtonTooltip
            {
                Text = channel.ToString(),
                Height = 22,
                Width = 130,
                Action = () => { },
                TooltipText = $"Press a key to (un)bind it to {channel}."
            });
        }

        var ds = new DrawSizePreservingFillContainer
        {
            TargetDrawSize = new Vector2(KB.Width, KB.Height),
            Strategy = DrawSizePreservationStrategy.Minimum,
            Child = Keyboard = new KB(this)
        };

        var grid = new GridContainer { RelativeSizeAxes = Axes.Both };
        grid.RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.Distributed) };
        grid.Content = new Drawable[][] {
            new Drawable[] { FlowContainer },
            new Drawable[] { ds },
        };

        AddInternal(grid);

        foreach (var map in Mapping)
            Keyboard.Get(map.Key).Channel = map.Value;
    }

    public void KeyPressed(InputKey key)
    {
        Drumset.Play(new DrumChannelEvent(0, Mapping.GetChannel(key)));
    }
    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key == Key.Escape || e.Repeat) return false;
        var inputKey = e.Key switch
        {
            Key.LShift => InputKey.LShift,
            Key.RShift => InputKey.RShift,
            Key.LAlt => InputKey.LAlt,
            Key.RAlt => InputKey.RAlt,
            Key.LControl => InputKey.LControl,
            Key.RControl => InputKey.RControl,
            _ => (InputKey)e.Key
        };
        var hover = FlowContainer.Children.FirstOrDefault(e => e.IsHovered);
        if (hover != null)
        {
            var channel = Enum.Parse<DrumChannel>(hover.Text.ToString());
            if (Mapping.GetChannel(inputKey) == channel)
                Mapping.Remove(inputKey);
            else
                Mapping.Set(inputKey, channel);
            Keyboard.Get(inputKey).Channel = Mapping.GetChannel(inputKey);
            return true;
        }
        Keyboard.UpdateChildren(e);
        KeyPressed(inputKey);
        return true;
    }
    protected override void OnKeyUp(KeyUpEvent e)
    {
        Keyboard.UpdateChildren(e);
        base.OnKeyUp(e);
    }

    static float MarginSize = 0.05f;

    class KB : Container<KB.KeyboardKey>
    {
        KeyboardMappingEditor KeyboardMapping;
        static FontUsage font = FrameworkFont.Regular.With(size: 20f / 50);
        [Resolved] public CommandController Command { get; private set; }
        public new const float Height = 6.25f;
        public new const float Width = 22.5f;

        public KB(KeyboardMappingEditor keyboardMapping)
        {
            KeyboardMapping = keyboardMapping;
            base.Width = Width;
            base.Height = Height;
        }

        public KeyboardKey Get(InputKey key) => Children.FirstOrDefault(e => e.Key == key);

        [BackgroundDependencyLoader]
        private void load()
        {
            foreach (var key in PhysicalKeyboard.SquareKeys)
                Add(new KeyboardKey(key.Item1.X, key.Item1.Y, 1, 1, key.Item2, this));
            foreach (var key in PhysicalKeyboard.SpecialKeys)
                Add(new KeyboardKey(key.Item1.X, key.Item1.Y, key.Item3.X, key.Item3.Y, key.Item2, this));
        }

        public void UpdateChildren(UIEvent e)
        {
            if (e is KeyboardEvent ke)
            {
                var key = ke.Key;
                var inputKey = key switch
                {
                    Key.LShift => InputKey.LShift,
                    Key.RShift => InputKey.RShift,
                    Key.LAlt => InputKey.LAlt,
                    Key.RAlt => InputKey.RAlt,
                    Key.LControl => InputKey.LControl,
                    Key.RControl => InputKey.RControl,
                    _ => (InputKey)key
                };
                var k = Get(inputKey);
                if (k != null) k.Pressed = ke is KeyDownEvent;
            }
        }

        public class KeyboardKey : CompositeDrawable, IHasTooltip
        {
            KB Keyboard;
            public InputKey Key;

            DrumIcon Icon;

            DrumChannel _channel;
            public DrumChannel Channel
            {
                get => _channel; set
                {
                    if (_channel == value) return;
                    _channel = value;

                    if (Icon != null) RemoveInternal(Icon, true);

                    Icon = DrumIcon.From(value);
                    if (Icon != null)
                    {
                        Icon.Origin = Anchor.BottomCentre;
                        Icon.Anchor = Anchor.BottomCentre;
                        Icon.Scale = new Vector2(1 / 24f * 0.6f);
                        Icon.Y = -0.05f;
                        Icon.Depth = -1;
                        AddInternal(Icon);
                    }

                    UpdateColour();
                }
            }

            public LocalisableString TooltipText => Channel == DrumChannel.None ? "" : Channel.ToString();

            bool pressed;
            public bool Pressed
            {
                get => pressed; set
                {
                    if (pressed == value) return;
                    pressed = value;
                    UpdateColour();
                }
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                Pressed = true;
                Keyboard.KeyboardMapping?.KeyPressed(Key);
                return true;
            }

            protected override void OnMouseUp(MouseUpEvent e)
            {
                Pressed = false;
            }

            public void UpdateMapping()
            {
                UpdateColour();
            }

            public void UpdateColour()
            {
                if (Pressed)
                {
                    Box.Colour = Colour4.SkyBlue;
                }
                else
                {
                    if (Channel != DrumChannel.None)
                        Box.Colour = Colour4.LightGreen;
                    else
                        Box.Colour = Colour4.White;
                }
            }


            Box Box;
            public KeyboardKey(float x, float y, float width, float height, InputKey key, KB keyboard)
            {
                Key = key;
                Keyboard = keyboard;
                X = x;
                Y = y;
                Width = width;
                Height = height;
                AddInternal(Box = new Box
                {
                    Width = width - MarginSize * 2,
                    Height = height - MarginSize * 2,
                    X = MarginSize,
                    Y = MarginSize,
                    Origin = Anchor.TopLeft
                });
                UpdateColour();
                AddInternal(new SpriteText
                {
                    X = width / 2,
                    Y = 0.27f,
                    Origin = Anchor.Centre,
                    Text = HotkeyDisplay.KeyString(key),
                    Colour = Colour4.Black,
                    Font = font
                });
            }
        }
    }
    // TODO merge with InputDisplay
    class DrumIcon : Circle
    {
        public static Colour4 CymbolColour = Colour4.LightGoldenrodYellow;
        public static Colour4 SnareColour = Colour4.White;
        public static Colour4 BassColour = Colour4.LightGray;
        public Colour4 BaseColour;
        DrumIcon(DrumChannel channel, float size, Colour4 colour, float depth = 0) :
            this(channel, new Vector2(size), colour, depth)
        { }
        DrumIcon(DrumChannel channel, Vector2 size, Colour4 colour, float depth = 0)
        {
            BaseColour = colour;
            var hasBorder = channel != DrumChannel.RideBell;
            var borderSize = hasBorder ? 2f : 0;
            Size = size + new Vector2(borderSize * 2);
            Colour = colour;
            Depth = depth;
            if (hasBorder)
            {
                BorderColour = Colour4.DarkGray;
                BorderThickness = borderSize;
            }
            if (colour == CymbolColour && channel != DrumChannel.RideBell)
            {
                Add(new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(6f),
                    Colour = Colour4.DarkGray,
                    Depth = -1
                });
            }
        }
        public static DrumIcon From(DrumChannel channel) => channel switch
        {
            // max size is 20.6 (since border adds +0.3 * 2)
            DrumChannel.Ride => new DrumIcon(channel, 20, CymbolColour, -5),
            DrumChannel.RideBell => new DrumIcon(channel, 4, CymbolColour, -6),
            DrumChannel.Crash => new DrumIcon(channel, 16, CymbolColour, -4),
            DrumChannel.ClosedHiHat => new DrumIcon(channel, 13, CymbolColour, -3),
            DrumChannel.SmallTom => new DrumIcon(channel, 10, Util.Skin.Notation.Channels[channel].Color, 1),
            DrumChannel.MediumTom => new DrumIcon(channel, 12, Util.Skin.Notation.Channels[channel].Color, 2),
            DrumChannel.LargeTom => new DrumIcon(channel, 14, Util.Skin.Notation.Channels[channel].Color, 3),
            DrumChannel.Snare => new DrumIcon(channel, 14, SnareColour, 4),
            DrumChannel.BassDrum => new DrumIcon(channel, new Vector2(20, 17), BassColour, 5),
            DrumChannel.OpenHiHat => From(DrumChannel.ClosedHiHat),
            DrumChannel.HalfOpenHiHat => From(DrumChannel.ClosedHiHat),
            _ => null
        };
    }
}

