using System.ComponentModel;
using Newtonsoft.Json;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace DrumGame.Game.Skinning;

public enum ElementLayout
{
    Default,
    Horizontal,
    Vertical
}

public enum SkinAnchorTarget
{
    Parent,
    ModeText,
    LaneContainer,
    PositionIndicator,
    SongInfoPanel
}

public class AdjustableSkinData // this should be serialized to the skin
{
    public ElementLayout Layout;
    public SkinAnchorTarget AnchorTarget;

    // typically Anchor/Origin should be the same
    public Anchor Anchor
    {
        get => AnchorFromPos(RelativePosition);
        set
        {
            if (value != Anchor.Custom)
                RelativePosition = AnchorToRelative(value);
        }
    }
    public bool ShouldSerializeAnchor() => Anchor != Anchor.Custom;

    public Anchor Origin; // I recommend setting Anchor instead when possible
    public bool ShouldSerializeOrigin() => Origin != Anchor;

    public bool Hide;
    [DefaultValue(1f)]
    public float Scale = 1;
    public float Rotation;

    // using this is mostly up to the implementer
    [JsonIgnore]
    public Vector2 Size
    {
        get => new(Width, Height); set
        {
            Width = value.X;
            Height = value.Y;
        }
    }
    public Axes RelativeSizeAxes;
    public float Width;
    public float Height;

    [JsonIgnore]
    public Vector2 AbsolutePosition
    {
        get => new(X, Y); set
        {
            X = value.X;
            Y = value.Y;
        }
    }
    public float X;
    public float Y;
    public Vector2 RelativePosition; // setting this will require Anchor to be custom
    public bool ShouldSerializeRelativePosition() => Anchor == Anchor.Custom;

    public AdjustableSkinData LoadDefaults()
    {
        if (Origin == default)
        {
            if (RelativePosition.X == 0)
                Origin |= Anchor.x0;
            else if (RelativePosition.X == 0.5)
                Origin |= Anchor.x1;
            else if (RelativePosition.X == 1)
                Origin |= Anchor.x2;
            if (RelativePosition.Y == 0)
                Origin |= Anchor.y0;
            else if (RelativePosition.Y == 0.5)
                Origin |= Anchor.y1;
            else if (RelativePosition.Y == 1)
                Origin |= Anchor.y2;
        }
        return this;
    }

    public static Anchor AnchorFromPos(Vector2 rel)
    {
        var res = (Anchor)0;
        if (rel.X == 0)
            res |= Anchor.x0;
        else if (rel.X == 0.5)
            res |= Anchor.x1;
        else if (rel.X == 1)
            res |= Anchor.x2;
        else return Anchor.Custom;
        if (rel.Y == 0)
            res |= Anchor.y0;
        else if (rel.Y == 0.5)
            res |= Anchor.y1;
        else if (rel.Y == 1)
            res |= Anchor.y2;
        else return Anchor.Custom;
        return res;
    }

    public void ChangeOrigin(Anchor origin, AdjustableSkinElement self)
    {
        var currentTopLeft = TopLeft(self);
        Origin = origin;
        AbsolutePosition += currentTopLeft - TopLeft(self);
    }
    public void ChangeTarget(SkinAnchorTarget target, AdjustableSkinElement self)
    {
        var currentTopLeft = TopLeft(self);
        AnchorTarget = target;
        self.ApplyTarget();
        AbsolutePosition += currentTopLeft - TopLeft(self);
    }
    public void ChangeAnchor(Anchor anchor, AdjustableSkinElement self)
    {
        var currentTopLeft = TopLeft(self);
        if (AnchorTarget == SkinAnchorTarget.Parent && Origin == Anchor)
            Origin = anchor;
        Anchor = anchor;
        AbsolutePosition += currentTopLeft - TopLeft(self);
    }

    public Vector2 TopLeft(AdjustableSkinElement self)
    {
        var parent = self.Parent;
        var s = (self.AnchorTarget ?? parent).DrawSize;
        var scaledRelativeOffset = RelativePosition * s;
        if (self.AnchorTarget != null)
            scaledRelativeOffset += parent.ToLocalSpace(self.AnchorTarget.ScreenSpaceDrawQuad.TopLeft);

        return AbsolutePosition
            + scaledRelativeOffset
            - AnchorToRelative(Origin) * self.DrawSize;
    }

    public static Vector2 AnchorToRelative(Anchor anchor)
    {
        var res = Vector2.Zero;
        if (anchor.HasFlag(Anchor.x1))
            res.X = 0.5f;
        else if (anchor.HasFlag(Anchor.x2))
            res.X = 1;
        if (anchor.HasFlag(Anchor.y1))
            res.Y = 0.5f;
        else if (anchor.HasFlag(Anchor.y2))
            res.Y = 1;
        return res;
    }

    public void SnapTo(Vector2 position, AdjustableSkinElement self)
    {
        var parent = self.Parent;
        // position is in parent space
        var s = (self.AnchorTarget ?? parent).DrawSize;
        var scaledRelativeOffset = RelativePosition * s;
        if (self.AnchorTarget != null)
            scaledRelativeOffset += parent.ToLocalSpace(self.AnchorTarget.ScreenSpaceDrawQuad.TopLeft);
        var computedPosition = position + scaledRelativeOffset;


        var size = self.DrawSize * Scale; // not sure why we need this
        var originPosition = AnchorToRelative(Origin) * size;

        AbsolutePosition = Vector2.Clamp(computedPosition, originPosition,
            parent.DrawSize - size + originPosition) - scaledRelativeOffset;
    }
}

public abstract class AdjustableSkinElement : CompositeDrawable
{
    // TODO make this use an interface that works for both notation and mania displays
    public abstract ref AdjustableSkinData SkinPath { get; }

    public virtual ElementLayout[] AvailableLayouts => null;

    public AdjustableSkinData SkinData;
    public ElementLayout Layout => SkinData.Layout;
    public Drawable AnchorTarget;

    public virtual void ResetData()
    {
        SkinData = DefaultData().LoadDefaults();
        SkinPath = null;
        SkinManager.MarkDirty(Util.Skin);
        LayoutChanged();
        ApplySkinData(writeToSkin: false);
    }

    public void UpdateAnchorPosition()
    {
        if (AnchorTarget == null) return;
        Position = AnchorTarget.ToSpaceOfOtherDrawable(
                new Vector2(SkinData.X + SkinData.RelativePosition.X * AnchorTarget.DrawSize.X,
                    SkinData.Y + +SkinData.RelativePosition.Y * AnchorTarget.DrawSize.Y), Parent);
        cachedTargetInfo = AnchorTarget.DrawInfo.Matrix;
    }
    Matrix3 cachedTargetInfo; // awful since it's struct
    protected override void Update()
    {
        if (AnchorTarget == null) return;
        // this is still kinda expensive
        // this doesn't update when size changes
        // I'm okay having ~100 of these
        if (!AnchorTarget.DrawInfo.Matrix.Equals(cachedTargetInfo))
            UpdateAnchorPosition();
        base.Update();
    }

    public abstract AdjustableSkinData DefaultData();
    // skip init is for when DefaultData and/or SkinPath aren't valid until the parent constructor runs
    public AdjustableSkinElement(bool skipInit = false)
    {
        SkinManager.RegisterElement(this);
        if (!skipInit) InitializeSkinData();
    }
    // can't call this in constructor since SkinPath may not set up correctly yet
    protected void InitializeSkinData()
    {
        SkinData = SkinPath ?? DefaultData().LoadDefaults();
        ApplySkinData(true, false);
    }

    // this registers everything we need, but only while alt is pressed
    public SkinElementOverlay Overlay;
    public void ShowOverlay()
    {
        if (Overlay != null)
        {
            Overlay.ShouldHide = false;
            return;
        }
        AddInternal(Overlay = new(this));
    }
    public void HideOverlay()
    {
        if (Overlay.Locked) Overlay.ShouldHide = true;
        else
        {
            RemoveInternal(Overlay, true);
            Overlay = null;
        }
    }
    // layout changed occurs when we change things that aren't simple drawable properties
    // this should include the Layout value and Size (once we add it)
    public virtual void LayoutChanged() { }
    // will need a fancy way of applying resizes eventually if we add those
    public void ApplyTarget()
    {
        AnchorTarget = SkinManager.GetTarget(SkinData.AnchorTarget);
    }
    public virtual void ApplySkinData(bool initial = false, bool writeToSkin = true)
    {
        if (writeToSkin)
        {
            SkinPath = SkinData;
            SkinManager.MarkDirty(Util.Skin);
        }
        ApplyTarget();
        if (AnchorTarget == null)
        {
            RelativeAnchorPosition = SkinData.RelativePosition;
            X = SkinData.X;
            Y = SkinData.Y;
        }
        else
        {
            Anchor = Anchor.TopLeft;
            // can throw exception during initial
            if (!initial) UpdateAnchorPosition();
        }
        Origin = SkinData.Origin;
        Scale = new Vector2(SkinData.Scale);
        RelativeSizeAxes = SkinData.RelativeSizeAxes;
        Rotation = SkinData.Rotation;
        Alpha = SkinData.Hide ? 0 : 1;
        // in most cases, Width and Height should already be set by the layout updater
        if (SkinData.Width != default)
            Width = SkinData.Width;
        if (SkinData.Height != default)
            Height = SkinData.Height;
        Overlay?.ApplySkinData();
    }

    protected override void Dispose(bool isDisposing)
    {
        SkinManager.UnregisterElement(this);
        base.Dispose(isDisposing);
    }
}
