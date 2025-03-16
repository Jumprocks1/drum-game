using System.ComponentModel;
using Newtonsoft.Json;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;
using System;
using System.Linq.Expressions;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using osu.Framework.Allocation;
using DrumGame.Game.Containers;

namespace DrumGame.Game.Skinning;

public enum ElementLayout
{
    Default,
    Horizontal,
    Vertical
}

public static class SkinAnchorTarget
{
    public const string Parent = null;
    public const string ModeText = "ModeText";
    public const string LaneContainer = "LaneContainer";
    public const string PositionIndicator = "PositionIndicator";
    public const string SongInfoPanel = "SongInfoPanel";
    public const string CameraPlaceholder = "CameraPlaceholder";
    public const string Video = "Video";
}

public class AdjustableSkinData // this should be serialized to the skin
{
    public ElementLayout Layout;
    public string AnchorTarget;

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

    public float Depth;
    public FillMode FillMode;
    [DefaultValue(1f)]
    public float FillAspectRatio = 1;

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
    public void ChangeTarget(string target, AdjustableSkinElement self)
    {
        var currentTopLeft = TopLeft(self);
        AnchorTarget = target;
        self.ApplyTarget();
        AbsolutePosition += currentTopLeft - TopLeft(self);
    }
    public void ChangeAnchor(Anchor anchor, AdjustableSkinElement self)
    {
        var currentTopLeft = TopLeft(self);
        if (AnchorTarget == null && Origin == Anchor)
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
            - AnchorToRelative(Origin) * self.DrawSize * self.Scale;
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
    public abstract Expression<Func<Skin, AdjustableSkinData>> SkinPathExpression { get; }
    protected static Skin Skin => Util.Skin;

    public virtual string OverlayTooltip => null;

    public virtual void ModifyOverlayContextMenu(ContextMenuBuilder<AdjustableSkinElement> menu) { }

    public virtual ElementLayout[] AvailableLayouts => null;

    public AdjustableSkinData SkinData;
    public ElementLayout Layout => SkinData.Layout;
    public Drawable AnchorTarget;

    public virtual void ResetData()
    {
        SkinData = DefaultData().LoadDefaults();
        SkinPathExpression.Set(null);
        LayoutChanged();
        ApplySkinData(writeToSkin: false);
    }
    public void ReloadFromSkin()
    {
        var diskSkin = SkinManager.ParseSkin(SkinManager.CurrentSkin);
        if (diskSkin != null)
        {
            SkinPathExpression.Set(SkinPathExpression.Get(diskSkin));
            SkinData = SkinPathExpression.GetOrDefault() ?? DefaultData().LoadDefaults();
            LayoutChanged();
            ApplySkinData(writeToSkin: false);
        }
    }

    public void UpdateAnchorPosition()
    {
        if (AnchorTarget == null) return;
        Position = AnchorTarget.ToSpaceOfOtherDrawable(
                new Vector2(SkinData.X + SkinData.RelativePosition.X * AnchorTarget.DrawSize.X,
                    SkinData.Y + +SkinData.RelativePosition.Y * AnchorTarget.DrawSize.Y), Parent);

        // TODO fill mode isn't supported since RelativeSizeAxes won't be set to both
        // we need to add that math here
        if (SkinData.RelativeSizeAxes.HasFlag(Axes.X))
            Width = SkinData.Width * AnchorTarget.DrawSize.X;
        if (SkinData.RelativeSizeAxes.HasFlag(Axes.Y))
            Height = SkinData.Height * AnchorTarget.DrawSize.Y;

        lastInvalidate = AnchorTarget.InvalidationID;
    }
    long lastInvalidate;
    bool targetAttempted;
    protected override void Update()
    {
        // sometimes target isn't found at first
        if (SkinData.AnchorTarget != null && AnchorTarget == null && !targetAttempted)
        {
            targetAttempted = true;
            ApplyTarget();
        }
        if (AnchorTarget == null) return;
        if (lastInvalidate != AnchorTarget.InvalidationID)
            UpdateAnchorPosition();
        base.Update();
    }

    public abstract AdjustableSkinData DefaultData();
    // skip init is for when DefaultData and/or SkinPath aren't valid until the parent constructor runs
    // in those cases, it should be called by the subclass constructor
    public AdjustableSkinElement(bool skipInit = false)
    {
        SkinManager.RegisterElement(this);
        if (!skipInit) InitializeSkinData();
    }
    // can't call this in constructor since SkinPath may not set up correctly yet
    protected void InitializeSkinData()
    {
        SkinData = SkinPathExpression.GetOrDefault() ?? DefaultData().LoadDefaults();
        ApplySkinData(true, false);
    }

    // this registers everything we need, but only while alt is pressed
    public SkinElementOverlay Overlay;
    public virtual void ShowOverlay()
    {
        if (Overlay != null)
        {
            Overlay.ShouldHide = false;
            return;
        }
        AddInternal(Overlay = new(this));
    }
    public virtual void HideOverlay()
    {
        if (Overlay.LockCount > 0) Overlay.ShouldHide = true;
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
        if (AnchorTarget != null)
            Anchor = Anchor.TopLeft;
    }
    // only time we don't write to skin are on initialize and on reset
    // we don't write on initialize since nothing has actually changed yet
    // we don't write on reset since we would rather write `null` instead
    public virtual void ApplySkinData(bool initial = false, bool writeToSkin = true)
    {
        if (writeToSkin)
            SkinPathExpression.Set(SkinData);
        ApplyTarget();
        if (AnchorTarget == null)
        {
            RelativeSizeAxes = SkinData.RelativeSizeAxes;
            RelativeAnchorPosition = SkinData.RelativePosition;
            X = SkinData.X;
            Y = SkinData.Y;
            // in most cases, Width and Height should already be set by the layout updater
            if (SkinData.Width != default)
                Width = SkinData.Width;
            if (SkinData.Height != default)
                Height = SkinData.Height;
        }
        else
        {
            RelativeSizeAxes = Axes.None;
            // can throw exception during initial
            if (!initial) UpdateAnchorPosition();

            // relative axis sizes are handled in UpdateAnchorPosition
            if (SkinData.Width != default && !SkinData.RelativeSizeAxes.HasFlag(Axes.X))
                Width = SkinData.Width;
            if (SkinData.Height != default && !SkinData.RelativeSizeAxes.HasFlag(Axes.Y))
                Height = SkinData.Height;
        }
        Origin = SkinData.Origin;
        Scale = new Vector2(SkinData.Scale);
        if (SkinData.Depth != Depth)
        {
            if (initial)
                Depth = SkinData.Depth;
            else if (Parent != null)
                // this should be called only very rarely
                Util.Call(Parent, nameof(ChangeInternalChildDepth), [this, SkinData.Depth]);
        }
        FillMode = SkinData.FillMode;
        FillAspectRatio = SkinData.FillAspectRatio;
        Rotation = SkinData.Rotation;
        Alpha = SkinData.Hide ? 0 : 1;
        Overlay?.ApplySkinData();
    }

    protected override void Dispose(bool isDisposing)
    {
        SkinManager.UnregisterElement(this);
        base.Dispose(isDisposing);
    }

    // DrawPosition assuming our SkinData.Position = Vector2.Zero
    public Vector2 TargetAnchorPosition()
    {
        var offset = Vector2.Zero;
        if (Parent != null && RelativePositionAxes != Axes.None)
        {
            offset = Parent.RelativeChildOffset;
            if (!RelativePositionAxes.HasFlag(Axes.X))
                offset.X = 0;
            if (!RelativePositionAxes.HasFlag(Axes.Y))
                offset.Y = 0;
        }
        var pos = AnchorTarget?.ToSpaceOfOtherDrawable(SkinData.RelativePosition * AnchorTarget.DrawSize, Parent) ?? Vector2.Zero;
        return ApplyRelativeAxes(RelativePositionAxes, pos - offset, FillMode.Stretch);
    }
}
