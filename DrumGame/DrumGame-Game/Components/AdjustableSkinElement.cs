using System.Collections.Generic;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Modals;
using DrumGame.Game.Stores.Skins;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace DrumGame.Game.Components;

public class AdjustableSkinData // this should be serialized to the skin
{
    public string AnchorTarget; // allows changing the parent

    // typically Anchor/Origin should be the same
    public Anchor Anchor
    {
        set
        {
            if (value != Anchor.Custom)
                RelativePosition = AnchorToRelative(value);
        }
    }
    public Anchor Origin; // I recommend setting Anchor instead when possible
    public bool Hide;
    public float Scale;

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
    public Vector2 RelativePosition; // setting this with require Anchor to be custom

    public AdjustableSkinData LoadDefaults()
    {
        if (Scale == default) Scale = 1;
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

    public void ChangeAnchor(Anchor anchor, Drawable self)
    {
        var currentTopLeft = TopLeft(self);
        Anchor = anchor;
        Origin = anchor;
        AbsolutePosition += currentTopLeft - TopLeft(self);
    }

    public Vector2 TopLeft(Drawable self) => AbsolutePosition
            + RelativePosition * self.Parent.DrawSize
            - AnchorToRelative(Origin) * self.DrawSize;

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

    public void SnapTo(Vector2 position, Drawable self)
    {
        var parent = self.Parent;
        // position is in parent space
        var s = parent.DrawSize;
        var scaledRelativeOffset = RelativePosition * s;
        var computedPosition = position + scaledRelativeOffset;

        var size = self.DrawSize;
        var originPosition = AnchorToRelative(Origin) * size;

        AbsolutePosition = Vector2.Clamp(computedPosition, new Vector2(0) + originPosition,
            parent.DrawSize - size + originPosition) - scaledRelativeOffset;
    }
}

public abstract class AdjustableSkinElement : CompositeDrawable
{
    public abstract ref AdjustableSkinData SkinPath { get; }
    public virtual IEnumerable<string> AvailableParents => null;

    // if you want ApplySkinData to always parent, make sure to override and return a default value here
    public virtual Container GetParent(string key) => null;

    protected AdjustableSkinData SkinData;

    public virtual void ResetData()
    {
        SkinData = DefaultData().LoadDefaults();
        SkinPath = null;
        SkinManager.MarkDirty(Util.Skin);
        ApplySkinData(false);
    }

    public abstract AdjustableSkinData DefaultData(); // recommend making this also available statically for other classes
    public AdjustableSkinElement(AdjustableSkinData skinData = null)
    {
        SkinData = skinData ?? SkinPath ?? DefaultData().LoadDefaults();
        SkinManager.RegisterElement(this);

        ApplySkinData(false);
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

    // will need a fancy way of applying resizes eventually if we add those
    protected virtual void ApplySkinData(bool changed)
    {
        if (changed)
        {
            SkinPath = SkinData;
            SkinManager.MarkDirty(Util.Skin);
        }
        RelativeAnchorPosition = SkinData.RelativePosition;
        Origin = SkinData.Origin;
        X = SkinData.X;
        Y = SkinData.Y;
        Scale = new Vector2(SkinData.Scale);
        var parent = GetParent(SkinData.AnchorTarget);
        if (parent != null)
        {
            if (Parent is Container c) c.Remove(this, false);
            if (Parent != null)
                Util.Palette.UserError($"Failed to change skin element's parent. {Parent.GetType()} isn't a container");
            else
            {
                parent.Add(this);
            }
        }
        Alpha = SkinData.Hide ? 0 : 1;
        Overlay?.ApplySkinData();
    }

    protected override void Dispose(bool isDisposing)
    {
        SkinManager.UnregisterElement(this);
        base.Dispose(isDisposing);
    }

    public class SkinElementOverlay : CompositeDrawable, IHasMarkupTooltip, IHasContextMenu, IHasContextMenuEvent
    {
        public bool ShouldHide;
        public bool Locked;
        public void Lock() => Locked = true;
        public void Unlock()
        {
            Locked = false;
            if (ShouldHide) Element.HideOverlay();
        }
        AdjustableSkinElement Element;
        Circle OriginMarker;
        Box AnchorMarker;
        public SkinElementOverlay(AdjustableSkinElement element)
        {
            Element = element;
            AddInternal(new Box
            {
                Colour = new Colour4(255, 100, 100, 50),
                RelativeSizeAxes = Axes.Both
            });
            AddInternal(OriginMarker = new Circle
            {
                Colour = new Colour4(50, 50, 255, 255),
                Width = 8,
                Height = 8,
                Origin = Anchor.Centre
            });
            AddInternal(AnchorMarker = new Box
            {
                Colour = new Colour4(50, 255, 50, 255),
                Width = 6,
                Height = 6
            });
            ApplySkinData();
            Depth = -100;
            RelativeSizeAxes = Axes.Both;
        }

        public void ApplySkinData()
        {
            OriginMarker.Anchor = Element.Origin;
            AnchorMarker.RelativeAnchorPosition = Element.RelativeAnchorPosition;
            AnchorMarker.Position = -AnchorMarker.RelativeAnchorPosition * AnchorMarker.Size;
        }

        public string MarkupTooltip => "Hold left click to reposition\nRight click for more options";

        Vector2 down;
        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button == MouseButton.Left)
            {
                Lock();
                down = Element.SkinData.AbsolutePosition;
                return true;
            }
            return base.OnDragStart(e);
        }
        // prevent mouse event passing through
        protected override bool OnMouseDown(MouseDownEvent e) => true;
        protected override void OnDrag(DragEvent e)
        {
            // this should probably calculate the closest anchor (of the 9 standard positions) and convert to that
            // probably only on drag end
            // we should also try snapping this position to edges of screen/other elements
            // holding shift should prevent snapping
            Element.SkinData.SnapTo(down + (e.MousePosition - e.MouseDownPosition) * Element.Scale, Element);
            Element.ApplySkinData(true);
            base.OnDrag(e);
        }
        protected override void OnDragEnd(DragEndEvent e) => Unlock();

        public void ContextMenuStateChanged(MenuState state) { if (state == MenuState.Closed) Unlock(); else Lock(); }

        // TODO we need some way of locking while menu is open
        // I couldn't find any events for open/close menu that had access to the target
        // ContextMenu.StateChanged almost works, but I don't know how to get the target
        // menuTarget is only stored privately in ContextMenuContainer
        // we could probably override + copy paste the entire ContextMenuContainer.OnMouseDown
        // it uses a lot of private methods though, so at that point we might as well remove ContextMenuContainer and just add the features to DrumContextMenuContainer
        public MenuItem[] ContextMenuItems => ContextMenuBuilder.New(Element)
            .Add("Set Scale", e =>
            {
                Util.Palette.RequestNumber("Setting Scale", "Scale", e.SkinData.Scale, s =>
                {
                    e.SkinData.Scale = (float)s;
                    e.ApplySkinData(true);
                });
            })
            .Add("Set Position", e =>
            {
                Util.Palette.RequestString("Setting Position", "Position", $"{e.SkinData.X}, {e.SkinData.Y}", s =>
                {
                    var spl = s.Split(',');
                    if (spl.Length == 2)
                    {
                        if (!float.TryParse(spl[0], out var x) || !float.TryParse(spl[1], out var y))
                            return;
                        e.SkinData.X = x;
                        e.SkinData.Y = y;
                        e.ApplySkinData(true);
                    }
                });
            })
            .Add("Set Anchor", e =>
            {
                Util.Palette.Request(new RequestConfig
                {
                    Title = "Setting Anchor",
                    Field = new EnumFieldConfig<Anchor>
                    {
                        DefaultValue = e.SkinData.Origin,
                        Values = [
                            Anchor.TopLeft, Anchor.TopCentre, Anchor.TopRight,
                            Anchor.CentreLeft, Anchor.Centre, Anchor.CentreRight,
                            Anchor.BottomLeft, Anchor.BottomCentre, Anchor.BottomRight,
                        ],
                        OnCommit = v =>
                        {
                            e.SkinData.ChangeAnchor(v, e);
                            e.ApplySkinData(true);
                        }
                    }
                });
            }).Tooltip("Important for deciding how this element behaves when the game is resized.\nThis also sets the element's origin property.")
            // should probably hide this if base class doesn't override reset
            .Add("Reset", e => e.ResetData()).Color(DrumColors.BrightYellow).Tooltip("This will reset all adjustments made to this element.")
            .Add("Hide", e =>
            {
                e.SkinData.Hide = true;
                e.ApplySkinData(true);
            }).Color(DrumColors.BrightRed).Tooltip("The only way to undo this is by editing the current skin directly.\nI plan to improve this in the future.")
            .Build();
    }
}
