using System;
using System.Linq;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace DrumGame.Game.Skinning;


public class SkinElementOverlay : CompositeDrawable, IHasMarkupTooltip, IHasContextMenu, IHasContextMenuEvent
{
    public bool ShouldHide;
    // be very careful, make sure to call an Unlock for every lock
    public int LockCount;
    public void Lock() => LockCount += 1;
    public void Unlock()
    {
        LockCount -= 1;
        if (ShouldHide) Element.HideOverlay();
    }
    AdjustableSkinElement Element;
    Circle OriginMarker;
    Box AnchorMarker;
    Box LineToOrigin;
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

    // this is called by Element.ApplySkinData
    public void ApplySkinData()
    {
        OriginMarker.Anchor = Element.Origin;
        AnchorMarker.RelativeAnchorPosition = Element.SkinData.RelativePosition;
        AnchorMarker.Position = -AnchorMarker.RelativeAnchorPosition * AnchorMarker.Size;
        UpdateLine();
    }

    public string MarkupTooltip
    {
        get
        {
            var o = "Hold left click to reposition\nRight click for more options";
            var extraTooltip = Element.OverlayTooltip;
            if (extraTooltip != null)
            {
                o = $"{extraTooltip}\n{o}";
            }
            var path = Element.SkinPathExpression;
            if (path != null)
            {
                var pathString = SkinPathUtil.PathString(path);
                if (pathString != null)
                    o = $"<brightGreen>{pathString}</>\n{o}";
            }
            return o;
        }
    }

    void UpdateLine()
    {
        if (IsDisposed) return;
        var showLine = LockCount > 0 || IsHovered;
        if (!showLine)
        {
            if (LineToOrigin != null)
            {
                RemoveInternal(LineToOrigin, true);
                LineToOrigin = null;
            }
            return;
        }
        if (LineToOrigin == null)
        {
            AddInternal(LineToOrigin = new()
            {
                Colour = new Colour4(50, 255, 50, 120),
                Height = 5,
                Width = 100,
                Origin = Anchor.CentreLeft
            });
        }
        var localAt0 = Element.Parent.ToSpaceOfOtherDrawable(Element.TargetAnchorPosition() + Element.AnchorPosition, this);
        var localAtOrigin = Element.Parent.ToSpaceOfOtherDrawable(Element.DrawPosition + Element.AnchorPosition, this);

        LineToOrigin.Position = localAt0;
        var diff = localAtOrigin - localAt0;
        LineToOrigin.Width = diff.Length;
        LineToOrigin.Rotation = MathF.Atan2(diff.Y, diff.X) * 180f / MathF.PI;
    }

    Vector2 down;
    protected override bool OnDragStart(DragStartEvent e)
    {
        if (e.Button == MouseButton.Left)
        {
            Lock();
            down = Element.SkinData.AbsolutePosition;
            UpdateLine();
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
        Element.ApplySkinData();
        UpdateLine();
        base.OnDrag(e);
    }
    protected override void OnDragEnd(DragEndEvent e)
    {
        Unlock();
        UpdateLine();
    }

    protected override bool OnHover(HoverEvent e)
    {
        UpdateLine();
        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        UpdateLine();
        base.OnHoverLost(e);
    }

    public void ContextMenuStateChanged(MenuState state) { if (state == MenuState.Closed) Unlock(); else Lock(); }

    // TODO we need some way of locking while menu is open
    // I couldn't find any events for open/close menu that had access to the target
    // ContextMenu.StateChanged almost works, but I don't know how to get the target
    // menuTarget is only stored privately in ContextMenuContainer
    // we could probably override + copy paste the entire ContextMenuContainer.OnMouseDown
    // it uses a lot of private methods though, so at that point we might as well remove ContextMenuContainer and just add the features to DrumContextMenuContainer
    public MenuItem[] ContextMenuItems => ContextMenuBuilder.New(Element)
        .Add("Save changes to skin", e =>
        {
            e.SkinPathExpression.Dirty();
            SkinManager.SavePartialSkin(Util.Skin);
        }).Color(DrumColors.BrightGreen)
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
                    e.ApplySkinData();
                }
            });
        })
        .Add("Set Size", e =>
        {
            Util.Palette.RequestString("Setting Size", "Size", $"{e.SkinData.Width}, {e.SkinData.Height}", s =>
            {
                var spl = s.Split(',');
                if (spl.Length == 2)
                {
                    if (!float.TryParse(spl[0], out var x) || !float.TryParse(spl[1], out var y))
                        return;
                    e.SkinData.Width = x;
                    e.SkinData.Height = y;
                    e.LayoutChanged();
                    e.ApplySkinData();
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
                        e.ApplySkinData();
                    }
                }
            });
        }).Tooltip("Sets the position on the parent element which this drawable is positioned relative to.\n\nImportant for deciding how this element behaves when the game is resized.\nThis also sets the element's origin property if it's the same as the current anchor.")
        .Add("Set Origin", e =>
        {
            Util.Palette.Request(new RequestConfig
            {
                Title = "Setting Origin",
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
                        e.SkinData.ChangeOrigin(v, e);
                        e.ApplySkinData();
                    }
                }
            });
        }).Tooltip("Sets the point on this element that gets moved around by the other settings.")
        .Add("Set Scale", e =>
        {
            Util.Palette.RequestNumber("Setting Scale", "Scale", e.SkinData.Scale, s =>
            {
                e.SkinData.Scale = (float)s;
                e.ApplySkinData();
            });
        })
        // should probably hide this if base class doesn't override reset
        .Add("Set Layout", e =>
        {
            var values = e.AvailableLayouts.ToList();
            values.Insert(0, ElementLayout.Default);
            Util.Palette.Request(new RequestConfig
            {
                Title = "Setting Layout",
                Field = new EnumFieldConfig<ElementLayout>
                {
                    DefaultValue = e.SkinData.Layout,
                    Values = values,
                    OnCommit = v =>
                    {
                        e.SkinData.Layout = v;
                        e.LayoutChanged();
                        e.ApplySkinData();
                    }
                }
            });
        }).Hide(Element.AvailableLayouts == null)
        .Add("Set Anchor Target", e =>
        {
            var options = SkinManager.AnchorTargets.Where(e => e.Item2 != Element)
                .Select(e => new AutocompleteKeyName(e.Item1, e.Item1))
                .Prepend(new AutocompleteKeyName(null, "Parent"))
                .ToList();
            var current = options.FirstOrDefault(o => o.Key == e.SkinData.AnchorTarget);
            if (current == null)
                options.Add(current = new AutocompleteKeyName(e.SkinData.AnchorTarget));
            Util.Palette.Request(new RequestConfig
            {
                Title = "Setting Anchor Target",
                Field = new AutocompleteFieldConfig<AutocompleteKeyName>
                {
                    DefaultValue = current,
                    Options = [.. options],
                    OnCommit = v =>
                    {
                        e.SkinData.ChangeTarget(v.Key, e);
                        e.ApplySkinData();
                    }
                }
            });
        }).Tooltip("Sets the element that this element is positioned relative to.\nUseful when other elements are moved or resized.")
        .Add("Set Depth", e =>
        {
            Util.Palette.RequestNumber("Setting Depth", "Depth", e.SkinData.Depth, s =>
            {
                e.SkinData.Depth = (float)s;
                e.ApplySkinData();
            });
        }).Tooltip("Elements with high depth values are far away from the \"camera\" and are therefore drawn behind elements with lower depths.\nThe default depths for elements in the scene can be examined with Ctrl+F1.\nDepth only applies for ordering of children in a single container. Depth will not change the draw order for elements unless they share a container.")
        .Add("Copy configuration to clipboard", e => Util.SetClipboard(JsonConvert.SerializeObject(Element.SkinData, SkinManager.SerializerSettings)))
            .Tooltip("Useful for storing these settings in a skin")
        .Add("Reset", e => e.ResetData()).Color(DrumColors.BrightYellow)
            .Tooltip("This will reset all adjustments made to this element.\nThis does not save the reset to the skin.")
        .Add("Reload from skin", e => e.ReloadFromSkin()).Color(DrumColors.BrightYellow)
            .Tooltip("This will reset all unsaved adjustments made to this element.")
        .Add("Hide", e =>
        {
            e.SkinData.Hide = true;
            e.ApplySkinData();
        }).Color(DrumColors.BrightRed).Tooltip("The only way to undo this is by editing the current skin directly.\nI plan to improve this in the future.")
        .Modify(Element.ModifyOverlayContextMenu)
        .Build();
}