using System;
using System.Collections.Generic;
using DrumGame.Game.Commands;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace DrumGame.Game.Components;

public class BasicSlider<T> : CompositeDrawable where T : struct, IComparable<T>, IConvertible, IEquatable<T>
{
    public virtual Colour4 BackgroundColour => new Colour4(100, 4, 4, 220);
    public virtual Colour4 TimelineColour => Colour4.Honeydew;
    protected virtual Drawable CreateThumb() => new Circle
    {
        Colour = Colour4.CornflowerBlue,
        Origin = Anchor.Centre,
        Width = 18,
        Height = 18,
        RelativePositionAxes = SlideAxis
    };
    public float Thickness { get; init; } = 30;
    public float Length { get; init; } = 300;
    public float BarThickness { get; init; } = 5;
    public Direction Direction { get; init; } = Direction.Horizontal;
    protected Axes SlideAxis;
    private new float Padding;
    public readonly BindableNumber<T> Value;


    private Drawable Thumb;
    private Container barContainer;
    public BasicSlider(BindableNumber<T> target)
    {
        Value = target.GetBoundCopy();
    }
    [BackgroundDependencyLoader]
    private void load(CommandController command)
    {
        SlideAxis = Direction == Direction.Horizontal ? Axes.X : Axes.Y;
        Padding = Thickness / 2;
        if (SlideAxis == Axes.X)
        {
            Height = Thickness;
            Width = Length;
        }
        else
        {
            Width = Thickness;
            Height = Length;
        }
        if (BackgroundColour.A > 0)
        {
            AddInternal(new Box // background
            {
                Colour = BackgroundColour,
                RelativeSizeAxes = Axes.Both,
            });
        }
        AddInternal(barContainer = new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = SlideAxis,
            Children = new Drawable[] {
                        new Circle { // timeline bar
                            RelativeSizeAxes = Axes.Both,
                            Colour = TimelineColour
                        },
                        Thumb = CreateThumb()
                    }
        });
        if (SlideAxis == Axes.X)
        {
            barContainer.Height = BarThickness;
            barContainer.Padding = new MarginPadding { Left = Padding, Right = Padding, };
            Thumb.Anchor = Anchor.CentreLeft;
        }
        else
        {
            barContainer.Width = BarThickness;
            barContainer.Padding = new MarginPadding { Top = Padding, Bottom = Padding, };
            Thumb.Anchor = Anchor.TopCentre;
        }

        Value.ValueChanged += ValueChanged;
        UpdateThumb(Value.Value);
    }
    protected virtual void UpdateThumb(T x)
    {
        if (SlideAxis == Axes.X) Thumb.X = x.ToSingle(null);
        else Thumb.Y = 1 - x.ToSingle(null);
    }
    protected override bool OnDragStart(DragStartEvent e)
    {
        if (e.Button == MouseButton.Left) return true;
        return base.OnDragStart(e);
    }
    protected void ValueChanged(ValueChangedEvent<T> e) => UpdateThumb(e.NewValue);
    protected override void Dispose(bool isDisposing)
    {
        Value.ValueChanged -= ValueChanged;
        base.Dispose(isDisposing);
    }
    protected virtual double ComputeValue(float x) => Math.Clamp(Math.Round(x * 100) / 100, 0, 1);
    protected void UpdateSliderPosition(UIEvent e)
    {
        var mouse = this.Parent.ToSpaceOfOtherDrawable(e.MousePosition, barContainer);
        var x = SlideAxis == Axes.X ?
            (mouse.X - Padding) / barContainer.RelativeToAbsoluteFactor.X :
            1 - (mouse.Y - Padding) / barContainer.RelativeToAbsoluteFactor.Y;
        Value.Set(ComputeValue(x));
    }
    protected override bool OnMouseDown(MouseDownEvent e)
    {
        UpdateSliderPosition(e);
        return true;
    }
    protected override bool OnScroll(ScrollEvent e)
    {
        return base.OnScroll(e);
    }
    protected override void OnDrag(DragEvent e)
    {
        UpdateSliderPosition(e);
        base.OnDrag(e);
    }
}

