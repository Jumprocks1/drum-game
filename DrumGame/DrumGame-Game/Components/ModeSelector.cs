using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osu.Framework.Graphics.Shapes;
using System.Collections.Generic;
using osu.Framework.Input.Events;
using DrumGame.Game.Utils;
using DrumGame.Game.Browsers;
using osu.Framework.Allocation;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;

namespace DrumGame.Game.Components;

public enum BeatmapOpenMode
{
    Edit,
    Play,
    Record,
    Listen,
    Replay
}
public class ModeOption
{
    public readonly string Text;
    public readonly string Tooltip;
    public readonly Colour4 Colour;
    public float FontScale = 1;
    public readonly BeatmapOpenMode Mode;
    public ModeOption(BeatmapOpenMode mode, string text, Colour4 colour, string tooltip = null)
    {
        Mode = mode;
        Text = text;
        Colour = colour;
        Tooltip = tooltip;
    }
}
public class ModeSelector : CompositeDrawable
{
    public const float WedgeSides = 25;
    public const float FontSize = 30;
    public new const float Height = 80;
    public const float Slope = Height / WedgeSides;
    public static double MaxRotation = Math.Atan(Height / WedgeSides);
    public readonly ModeOption[] Options;
    public BeatmapOpenMode Mode => Options[Target].Mode;
    public BeatmapSelectorState State;
    new const float Padding = 5;
    // List<WedgeSpriteTextContainer> Wedges = new();
    public int Target = 0;
    public void SetTarget(int i)
    {
        Target = i;
        State.OpenMode = Mode;
    }
    List<WedgeOption> Wedges = new();
    public ModeSelector(ModeOption[] options, BeatmapSelectorState state)
    {
        State = state;
        Scale = new Vector2(1);
        base.Height = Height;
        Options = options;
        AutoSizeAxes = Axes.X;
        AddInternal(new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = DrumColors.DarkBackground
        });


        var maxW = (Height - Padding * 2) / (float)Math.Sin(MaxRotation) - FontSize / Slope;

        var x = 0f;
        for (int i = 0; i < options.Length; i++)
        {
            var option = options[i];
            if (option.Mode == state.OpenMode) Target = i;
            var targetRot = Target == i ? 0 : -(float)(MaxRotation / Math.PI * 180);
            var box = new CenteredSpriteText(option.Text, option.FontScale)
            {
                Width = maxW,
                Height = FontSize,
                Rotation = targetRot
            };
            var wedge = new WedgeOption(this, i, box, option.Colour) { X = x };
            Wedges.Add(wedge);
            AddInternal(wedge);
            x += wedge.Width - WedgeSides;
        }
        UpdateWedges(true);
    }

    void UpdateWedges(bool hard = false)
    {
        var dt = hard ? 0 : Clock.TimeInfo.Elapsed;
        var x = 0f;
        for (int i = 0; i < Options.Length; i++)
        {
            var option = Options[i];
            var wedge = Wedges[i];
            wedge.X = x;
            var selected = Target == i;
            var oldRot = wedge.inner.Rotation;
            var targetRot = selected ? 0 : -(float)(MaxRotation / Math.PI * 180);
            if (oldRot != targetRot)
            {
                wedge.inner.Rotation = hard ? targetRot : (float)Util.ExpLerp(oldRot, targetRot, 0.99, dt, 0.01);
                wedge.UpdateWidth();
            }
            wedge.Wedge.SetActive(selected, hard);
            x += wedge.Width - WedgeSides;
        }
    }

    protected override void Update()
    {
        base.Update();
        UpdateWedges();
    }

    [Resolved] CommandController Command { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        Command.RegisterHandlers(this);
    }

    protected override void Dispose(bool isDisposing)
    {
        Command.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }

    [CommandHandler] public void SwitchMode() => SetTarget((Target + 1) % Options.Length);
    [CommandHandler] public void SwitchModeBack() => SetTarget(Util.Mod(Target - 1, Options.Length));

    public class CenteredSpriteText : CompositeDrawable
    {
        public CenteredSpriteText(string text, float fontScale = 1)
        {
            AddInternal(new SpriteText
            {
                Text = text,
                Font = FrameworkFont.Regular.With(size: FontSize * fontScale),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre
            });
        }
    }
    public class WedgeOption : CompositeDrawable
    {
        public void Add(Drawable d) => AddInternal(d);
        public WedgeContainer Wedge;
        public Drawable inner;
        public static float XRatio = (float)(1 / Math.Sqrt(1 / (Slope * Slope) + 1));
        public static float YRatio = (float)(1 / Math.Sqrt(Slope * Slope + 1));
        public static float AngledPadding = ModeSelector.Padding / XRatio;
        public void UpdateWidth()
        {
            var rot = inner.Rotation / 180 * Math.PI;
            var cos = (float)Math.Cos(rot);
            var sin = (float)Math.Sin(rot);
            // var matrix = inner.DrawInfo.Matrix;
            // var cos = matrix.M11;
            // var sin = matrix.M12;
            var vec = new Vector2(cos * inner.Width - sin * inner.Height, cos * inner.Height + sin * inner.Width) * 0.5f;

            var topLeft = new Vector2(0, Height / 2) - vec;
            var bottomRight = new Vector2(0, Height / 2) + vec;

            var width = bottomRight.X - topLeft.X + AngledPadding * 2 + WedgeSides + (bottomRight.Y - topLeft.Y) / Slope;
            Width = width;

            Wedge.Width = width;
        }
        public WedgeOption(ModeSelector selector, int index, Drawable drawable, Colour4 colour)
        {
            inner = drawable;
            Height = ModeSelector.Height;

            inner.Anchor = Anchor.Centre;
            inner.Origin = Anchor.Centre;
            AddInternal(Wedge = new WedgeContainer(selector, index, Height, WedgeSides, colour)
            {
                RelativeSizeAxes = Axes.Y
            });
            UpdateWidth();
            AddInternal(inner);
        }
    }
    public class WedgeContainer : CompositeDrawable
    {
        bool Active;
        bool hovered;
        Colour4 BaseColor;
        Colour4 TargetColor;
        public void SetActive(bool active, bool hard)
        {
            Active = active;
            UpdateColor(!hard);
        }
        void UpdateColor(bool animate)
        {
            var newTargetColor = Active ? BaseColor : DrumColors.DarkBackground;
            if (hovered)
                newTargetColor += new Colour4(0.1f, 0.1f, 0.1f, 0);

            if (TargetColor == newTargetColor) return;
            TargetColor = newTargetColor;
            if (animate)
                this.FadeColour(TargetColor, 200);
            else
                Colour = TargetColor;
        }
        public WedgeContainer(ModeSelector selector, int index, float height, float slant, Colour4 colour)
        {
            Padding = new MarginPadding
            {
                Left = slant,
            };
            BaseColor = colour;
            AddInternal(new WedgeBox(selector, index)
            {
                RelativeSizeAxes = Axes.Both,
                Shear = new Vector2(slant / height, 0),
                Hovered = h =>
                {
                    hovered = h;
                    UpdateColor(true);
                }
            });
        }

        private class WedgeBox : Box, IHasMarkupTooltip
        {
            ModeSelector Selector;
            int index;
            public Action<bool> Hovered;

            public string MarkupTooltip => Selector.Options[index].Tooltip;

            public WedgeBox(ModeSelector selector, int index)
            {
                Selector = selector;
                this.index = index;
            }
            protected override bool OnMouseDown(MouseDownEvent e)
            {
                Selector.SetTarget(index);
                return true;
            }
            protected override bool OnHover(HoverEvent e)
            {
                Hovered?.Invoke(true);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                Hovered?.Invoke(false);
                base.OnHoverLost(e);
            }
        }
    }
}
