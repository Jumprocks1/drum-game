using System;
using System.Numerics;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace DrumGame.Game.Components.Fields;

public class BasicSlider<T> : CompositeDrawable, IAcceptFocus where T : struct, IComparable<T>, IConvertible, IEquatable<T>, INumber<T>, IMinMaxValue<T>
{
    public override bool AcceptsFocus => true;
    public bool FillLeft = false;
    public virtual Colour4 BackgroundColour => new Colour4(100, 4, 4, 220);
    public Colour4 ThumbColor { get; init; } = DrumColors.DarkBlue;
    public Colour4 HoverThumbColor { get; init; } = DrumColors.Blue;
    protected virtual Container CreateThumb() => new Circle
    {
        Colour = ThumbColor,
        Origin = Anchor.Centre,
        Width = 18,
        Height = 18,
        RelativePositionAxes = SlideAxis,
        Depth = -2,
        EdgeEffect = ThumbEdge
    };
    EdgeEffectParameters ThumbEdge => new()
    {
        Type = EdgeEffectType.Shadow,
        Radius = CurrentShadowRadius,
        Colour = CurrentShadowColor,
    };
    Colour4 CurrentShadowColor => HasFocus ? DrumColors.BrightYellow.Opacity(0.4f) : Colour4.Black.Opacity(held ? 0.5f : 0.3f);
    float CurrentShadowRadius => IsHovered ? 4 : 2;
    public float Thickness { get; init; } = 30;
    public float Length { get; init; } = 300;
    public float BarThickness { get; init; } = 5;
    public Direction Direction { get; init; } = Direction.Horizontal;
    protected Axes SlideAxis;
    private new float Padding;
    public readonly BindableNumber<T> Value;


    Container Thumb;
    Container barContainer;
    Circle FillBar;
    Circle NonFillBar;
    public BasicSlider(BindableNumber<T> target)
    {
        Value = target.GetBoundCopy();
    }
    [BackgroundDependencyLoader]
    private void load()
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
            Children = [
                NonFillBar = new Circle {
                    RelativeSizeAxes = Axes.Both,
                    RelativePositionAxes = Axes.Both,
                    Colour = DrumColors.AnsiWhite,
                },
                Thumb = CreateThumb()
            ]
        });
        if (FillLeft)
            barContainer.Add(FillBar = new Circle
            {
                RelativeSizeAxes = Axes.Both,
                Depth = -1,
                Colour = ThumbColor,
                RelativePositionAxes = Axes.Both,
                Anchor = Direction == Direction.Horizontal ? Anchor.CentreLeft : Anchor.TopCentre,
                Origin = Direction == Direction.Horizontal ? Anchor.CentreLeft : Anchor.TopCentre
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
    // pretty sure I had this return a -0.0 once
    protected virtual double XToValue(float x) => Math.Clamp(Math.Round(x * 100) / 100, 0, 1);
    protected virtual float ValueToX(T value) => value.ToSingle(null);
    protected virtual void UpdateThumb(T value)
    {
        if (SlideAxis == Axes.X)
        {
            Thumb.X = ValueToX(value);
            if (FillBar != null)
            {
                FillBar.Width = Thumb.X;
                FillBar.Height = 1.5f;
                NonFillBar.X = Thumb.X;
                NonFillBar.Width = 1 - Thumb.X;
            }
        }
        else
        {
            Thumb.Y = 1 - ValueToX(value);
            if (FillBar != null)
            {
                FillBar.Height = 1 - Thumb.Y;
                FillBar.Width = 1.5f;
                FillBar.Y = Thumb.Y;
                NonFillBar.Height = Thumb.Y;
            }
        }
    }
    protected void ValueChanged(ValueChangedEvent<T> e) => UpdateThumb(e.NewValue);
    protected override void Dispose(bool isDisposing)
    {
        Value.ValueChanged -= ValueChanged;
        base.Dispose(isDisposing);
    }

    void Set(double value)
    {
        BeforeSet?.Invoke();
        Value.Set(value);
        AfterSet?.Invoke();
    }

    protected void UpdateSliderPosition(UIEvent e)
    {
        var mouse = Parent.ToSpaceOfOtherDrawable(e.MousePosition, barContainer);
        var x = SlideAxis == Axes.X ?
            (mouse.X - Padding) / barContainer.RelativeToAbsoluteFactor.X :
            1 - (mouse.Y - Padding) / barContainer.RelativeToAbsoluteFactor.Y;
        Set(XToValue(x));
    }
    public Action BeforeSet;
    public Action AfterSet;

    void UpdateColor()
    {
        Thumb.FadeColour(IsHovered ? HoverThumbColor : ThumbColor, 300);
        var newShadow = CurrentShadowColor;
        var newRadius = CurrentShadowRadius;
        if (Thumb.EdgeEffect.Colour != newShadow || Thumb.EdgeEffect.Radius != newRadius)
            Thumb.EdgeEffect = ThumbEdge;
    }

    protected override bool OnHover(HoverEvent e)
    {
        UpdateColor();
        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        UpdateColor();
        base.OnHoverLost(e);
    }

    protected override bool OnDragStart(DragStartEvent e)
    {
        if (e.Button == MouseButton.Left)
            return true;
        return false;
    }
    bool held;
    protected override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button == MouseButton.Left)
        {
            held = false;
            UpdateColor();
        }
        base.OnMouseUp(e);
    }
    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button == MouseButton.Left)
        {
            held = true;
            UpdateColor();
            UpdateSliderPosition(e);
            return true;
        }
        return false;
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

    const float IncrementAmount = 0.02f; // % of total visible width
    protected virtual void Increment(bool positive)
    {
        var oldValue = Value.Value;
        var sign = positive ? 1.0 : -1.0;
        var newValue = XToValue(ValueToX(Value.Value) + IncrementAmount * (float)sign);
        Set(newValue);
        // if we hit 0/1 this probably still won't do anything, and that's okay
        if (Value.Value == oldValue)
            Set(oldValue.ToDouble(null) + Value.Precision.ToDouble(null) * sign);
    }
    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (HasFocus)
        {
            if (e.Key == Key.Up || e.Key == Key.Right)
            {
                Increment(true);
                return true;
            }
            else if (e.Key == Key.Down || e.Key == Key.Left)
            {
                Increment(false);
                return true;
            }
        }
        return base.OnKeyDown(e);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        UpdateColor();
        base.OnFocusLost(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        UpdateColor();
        base.OnFocus(e);
    }


    public void Focus(IFocusManager focusManager) => focusManager.ChangeFocus(this);
}

