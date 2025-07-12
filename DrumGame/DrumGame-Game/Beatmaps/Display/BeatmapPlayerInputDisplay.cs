using System;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;

namespace DrumGame.Game.Beatmaps.Display;

public class BeatmapPlayerInputDisplay : CompositeDrawable
{
    bool _visible = true;
    public bool Visible
    {
        get => _visible; set
        {
            if (value == _visible) return;
            _visible = value;
            Container.Alpha = value ? 1 : 0;
            Background.Alpha = value ? 1 : 0;
        }
    }
    Box Background;
    Container Container;
    BeatmapPlayerInputDisplayInner Inner;
    public void Hit(DrumChannelEvent ev)
    {
        Inner.ChannelDisplays[(int)ev.Channel]?.Hit();
    }
    public BeatmapPlayerInputDisplay()
    {
        RelativeSizeAxes = Axes.Both;
        // background
        AddInternal(Background = new Box
        {
            Colour = Util.Skin.Notation.InputDisplayBackground,
            RelativeSizeAxes = Axes.Both,
        });
        AddInternal(Container = new DrawSizePreservingFillContainer
        {
            Child = Inner = new BeatmapPlayerInputDisplayInner
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre
            },
            TargetDrawSize = new osuTK.Vector2(BeatmapPlayerInputDisplayInner.Width, BeatmapPlayerInputDisplayInner.Height)
        });
    }




    public class BeatmapPlayerInputDisplayInner : CompositeDrawable
    {
        public new const float Width = 57.2f;
        public new const float Height = 40.2f;
        public ChannelDisplay[] ChannelDisplays = new ChannelDisplay[(int)DrumChannel.Metronome];
        public void Add(Drawable d) => AddInternal(d);

        [BackgroundDependencyLoader]
        private void load()
        {
            base.Width = Width;
            base.Height = Height;
            for (int i = 0; i < ChannelDisplays.Length; i++)
            {
                var ci = ChannelDisplay.From((DrumChannel)i);
                ChannelDisplays[i] = ci;
                if (ci != null) ci.Parent(this);
            }
            ChannelDisplays[(int)DrumChannel.SideStick] = ChannelDisplays[(int)DrumChannel.Snare];
            ChannelDisplays[(int)DrumChannel.OpenHiHat] = ChannelDisplays[(int)DrumChannel.ClosedHiHat];
            ChannelDisplays[(int)DrumChannel.HalfOpenHiHat] = ChannelDisplays[(int)DrumChannel.ClosedHiHat];
            ChannelDisplays[(int)DrumChannel.HiHatPedal] = ChannelDisplays[(int)DrumChannel.ClosedHiHat];
            ChannelDisplays[(int)DrumChannel.Splash] = ChannelDisplays[(int)DrumChannel.Crash];
            ChannelDisplays[(int)DrumChannel.China] = ChannelDisplays[(int)DrumChannel.Crash];
            ChannelDisplays[(int)DrumChannel.RideCrash] = ChannelDisplays[(int)DrumChannel.Ride];
        }
    }
    public class ChannelDisplay
    {
        public void Parent(BeatmapPlayerInputDisplayInner display)
        {
            display.Add(Drawable);
        }
        public static Colour4 CymbolColour = Colour4.LightGoldenrodYellow;
        public static Colour4 SnareColour = Colour4.White;
        public static Colour4 BassColour = Colour4.LightGray;
        public Colour4 BaseColour;
        public ChannelDisplay(DrumChannel channel, float x, float y, float size, Colour4 colour, float depth = 0) :
            this(channel, new Vector2(x, y), new Vector2(size), colour, depth)
        { }
        public ChannelDisplay(DrumChannel channel, Vector2 position, Vector2 size, Colour4 colour, float depth = 0)
        {
            BaseColour = colour;
            var hasBorder = channel != DrumChannel.RideBell;
            var borderSize = hasBorder ? 0.3f : 0;
            var c = new Circle
            {
                Position = position + new Vector2(0, 0.2f),
                Size = size + new Vector2(borderSize * 2),
                Origin = Anchor.Centre,
                Colour = colour,
                Depth = depth,
            };
            if (hasBorder)
            {
                c.BorderColour = Colour4.DarkGray;
                c.BorderThickness = borderSize;
            }
            Drawable = c;
            if (colour == CymbolColour && channel != DrumChannel.Ride)
            {
                c.Add(new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(1.5f),
                    Colour = Colour4.DarkGray,
                    Depth = -1
                });
            }
        }
        public static ChannelDisplay From(DrumChannel channel) => channel switch
        {
            DrumChannel.Ride => new ChannelDisplay(channel, 46.9f, 18.5f, 20, CymbolColour, -5),
            DrumChannel.RideBell => new ChannelDisplay(channel, 46.9f, 18.5f, 4, CymbolColour, -6),
            DrumChannel.Crash => new ChannelDisplay(channel, 11.8f, 15.0f, 14, CymbolColour, -4),
            DrumChannel.ClosedHiHat => new ChannelDisplay(channel, 6.8f, 28.6f, 13, CymbolColour, -3),
            DrumChannel.SmallTom => new ChannelDisplay(channel, 21.4f, 20.0f, 10, Util.Skin.Notation.Channels[channel].Color, 1),
            DrumChannel.MediumTom => new ChannelDisplay(channel, 32.5f, 20.0f, 12, Util.Skin.Notation.Channels[channel].Color, 2),
            DrumChannel.LargeTom => new ChannelDisplay(channel, 39.1f, 31.1f, 14, Util.Skin.Notation.Channels[channel].Color, 3),
            DrumChannel.Snare => new ChannelDisplay(channel, 17.6f, 31.1f, 14, SnareColour, 4),
            DrumChannel.BassDrum => new ChannelDisplay(channel, new Vector2(27.1f, 9.4f), new Vector2(20, 17), BassColour, 5),
            _ => null
        };
        public Drawable Drawable;
        public void Hit()
        {
            Drawable.ClearTransforms();
            Drawable.Colour = Colour4.PaleGreen;
            Drawable.FadeColour(BaseColour, 300);
            Drawable
                .ScaleTo(1.1f, 50, Easing.OutQuint)
                .Then(e => e.ScaleTo(1, 150));
        }
    }
}
