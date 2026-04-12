using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using DrumGame.Game.Channels;
using Newtonsoft.Json;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using Container = osu.Framework.Graphics.Containers.Container;

namespace DrumGame.Game.Skinning;

public class SkinNotationInputDisplayInfo
{
    // could have used RectangleF but many of it's props are getter only
    public readonly struct Skin_BoundingBox
    {
        public Skin_BoundingBox(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
        public readonly float Left;
        public readonly float Top;
        public readonly float Right;
        public readonly float Bottom;
        [JsonIgnore] public readonly float Width => Right - Left;
        [JsonIgnore] public readonly float Height => Bottom - Top;
        public static Skin_BoundingBox Expand(IEnumerable<Skin_BoundingBox> boxes)
        {
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;
            foreach (var box in boxes)
            {
                if (box.Left < minX) minX = box.Left;
                if (box.Right > maxX) maxX = box.Right;
                if (box.Top < minY) minY = box.Top;
                if (box.Bottom > maxY) maxY = box.Bottom;
            }
            return new(minX, minY, maxX, maxY);
        }
        public static bool operator ==(Skin_BoundingBox a, Skin_BoundingBox b)
            => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
        public static bool operator !=(Skin_BoundingBox a, Skin_BoundingBox b)
            => a.Left != b.Left || a.Top != b.Top || a.Right != b.Right || a.Bottom != b.Bottom;
        public override bool Equals(object obj) => obj is Skin_BoundingBox b && b == this;
        public override int GetHashCode() => Left.GetHashCode() ^ Right.GetHashCode() ^ Top.GetHashCode() ^ Bottom.GetHashCode();
    }

    public float Padding;
    public Skin_BoundingBox BoundingBox;
    // public Skin_Defaults Defaults;
    public class Skin_InputDisplayChannel
    {
        public enum InputDisplayType
        {
            Circle, // Default. aka capsule if width != height
            [EnumMember(Value = "kick-drum")]
            KickDrum,
            [EnumMember(Value = "double-kick-drum")]
            DoubleKickDrum,
            Pedal,
            [EnumMember(Value = "hihat-pedal-indicator")]
            HiHatPedalIndicator
        }
        public DrumChannel[] Channels;
        public DrumChannel Channel { set => Channels = [value]; }
        public byte[] MidiNotes; // optional
        public byte MidiNote { set => MidiNotes = [value]; }

        public DrumChannel[] BellChannels;
        public DrumChannel BellChannel { set => BellChannels = [value]; }
        public byte[] BellMidiNotes;
        public byte BellMidiNote { set => BellMidiNotes = [value]; }

        public DrumChannel[] EdgeChannels;
        public DrumChannel EdgeChannel { set => EdgeChannels = [value]; }
        public byte[] EdgeMidiNotes;
        public byte EdgeMidiNote { set => EdgeMidiNotes = [value]; }

        public Colour4 BorderColor;
        public float BorderWidth;

        public float X;
        public float Y;
        [JsonIgnore]
        public Vector2 Position
        {
            get => new(X, Y); set
            {
                X = value.X;
                Y = value.Y;
            }
        }
        public float Width;
        public float Height;
        public float Size
        {
            set
            {
                Width = value;
                Height = value;
            }
        }
        public Colour4 Color;
        public float Depth;
        public InputDisplayType Type;

        [DefaultValue(Anchor.Centre)] public Anchor Origin = Anchor.Centre;

        [JsonIgnore]
        public Vector2 OriginRelative
        {
            get
            {
                var result = Vector2.Zero;
                if (Origin.HasFlagFast(Anchor.x1))
                    result.X = 0.5f;
                else if (Origin.HasFlagFast(Anchor.x2))
                    result.X = 1;

                if (Origin.HasFlagFast(Anchor.y1))
                    result.Y = 0.5f;
                else if (Origin.HasFlagFast(Anchor.y2))
                    result.Y = 1;

                return result;
            }
        }

        [JsonIgnore]
        public Skin_BoundingBox BoundingBox
        {
            get
            {
                var origin = OriginRelative;
                var left = X - origin.X * Width;
                var top = Y - origin.Y * Height;
                return new(left, top, left + Width, top + Height);
            }
        }

        public Drawable ApplyTo(Drawable drawable, SkinNotationInputDisplayInfo skin)
        {
            drawable.X = X - skin.BoundingBox.Left;
            drawable.Y = Y - skin.BoundingBox.Top;
            drawable.Width = Width;
            drawable.Height = Height;
            drawable.Anchor = Anchor.TopLeft;
            drawable.Origin = Origin;
            if (drawable.Parent == null)
                drawable.Depth = Depth;
            return drawable;
        }

        public float BellSize;
        public Colour4 BellColor;
        public float? OverlayStrength;
    }

    public List<Skin_InputDisplayChannel> Displays;

    [DefaultValue((byte)127)] public byte HiHatPedalMax = 127;

    public Colour4 BackgroundColor = Colour4.Gainsboro;

    static readonly Colour4 DefaultCymbalColor = Colour4.LightGoldenrodYellow;

    static Skin_InputDisplayChannel make(DrumChannel[] channels, float x, float y, float size, Colour4 color, float depth = 0)
        => make(channels, x, y, size, size, color, depth);
    static Skin_InputDisplayChannel make(DrumChannel[] channels, float x, float y, float width, float height, Colour4 color, float depth = 0) => new()
    {
        Channels = channels,
        X = x,
        Y = y,
        Width = width,
        Height = height,
        Color = color,
        Depth = depth,
        BorderColor = Colour4.DarkGray * color,
        BorderWidth = 0.3f,
        BellSize = color == DefaultCymbalColor ? 1.5f : 0,
        BellColor = Colour4.DarkGray * color
    };

    public void LoadDefaults(NotationSkinInfo skin)
    {
        if (Displays == null)
        {
            var snareColour = Colour4.White;
            var bassColour = Colour4.LightGray;
            var ride = make([DrumChannel.Ride, DrumChannel.RideCrash], 46.9f, 18.7f, 20.6f, DefaultCymbalColor, -5);
            ride.BellSize = 0;
            var rideBell = make([DrumChannel.RideBell], 46.9f, 18.7f, 4, DefaultCymbalColor, -6);
            rideBell.BorderWidth = 0;
            Displays = [
                ride,
                rideBell,
                make([DrumChannel.Crash, DrumChannel.Splash, DrumChannel.China], 11.8f, 15.2f, 14.6f, DefaultCymbalColor, -4),
                make([DrumChannel.ClosedHiHat, DrumChannel.OpenHiHat, DrumChannel.HalfOpenHiHat, DrumChannel.HiHatPedal],
                    6.8f, 28.8f, 13.6f, DefaultCymbalColor, -3),
                make([DrumChannel.SmallTom], 21.4f, 20.2f, 10.6f, skin.Channels[DrumChannel.SmallTom].Color, 1),
                make([DrumChannel.MediumTom], 32.5f, 20.2f, 12.6f, skin.Channels[DrumChannel.MediumTom].Color, 2),
                make([DrumChannel.LargeTom], 39.1f, 31.3f, 14.6f, skin.Channels[DrumChannel.LargeTom].Color, 3),
                make([DrumChannel.Snare, DrumChannel.SideStick], 17.6f, 31.3f, 14.6f, snareColour, 4),
                make([DrumChannel.BassDrum],27.1f, 9.6f, 20.6f, 17.6f, bassColour, 5),
            ];
            if (BoundingBox == default) BoundingBox = new(0, 0, 57.2f, 40.2f);
        }
        else
        {
            foreach (var display in Displays)
            {
                if (display.Color == default)
                    display.Color = Colour4.White;
                if (display.BorderWidth > 0 && display.BorderColor == default)
                    display.BorderColor = new Colour4(169, 169, 169, 255);
            }
        }
        if (BoundingBox == default)
        {
            BoundingBox = Skin_BoundingBox.Expand(Displays.Select(e => e.BoundingBox));
            if (float.IsInfinity(BoundingBox.Left) || BoundingBox.Width == 0 || BoundingBox.Height == 0)
                BoundingBox = new(0, 0, 1, 1);
            else if (Padding > 0)
                BoundingBox = new Skin_BoundingBox(
                    BoundingBox.Left - Padding,
                    BoundingBox.Top - Padding,
                    BoundingBox.Right + Padding,
                    BoundingBox.Bottom + Padding);
        }
    }
}