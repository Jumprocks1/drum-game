using System;
using System.Collections.Generic;
using DrumGame.Game.Components;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace DrumGame.Game.Containers;

// Tooltips - Depth = 0
// Mirror/Popover - Depth = 1
// Blocker (if needed) - Depth = 1, gets added before mirror
// Lower Content - Depth = 10
public class DrumPopoverContainer : TooltipContainer
{
    public class PopoverInstance(DrumPopoverContainer Container)
    {
        public Mirror Mirror;
        public Drawable Drawable;
        public MouseBlocker MouseBlocker;
        public bool KeepAlive;
        public Action OnClose;
        public void Close() => Container.ClosePopover(this);
    }
    protected override double AppearDelay => 0;
    protected override ITooltip CreateTooltip() => new CustomTooltip(); // tooltip should always be depth = 0
    public DrumPopoverContainer() : base()
    {
        RelativeSizeAxes = Axes.Both;
        ChangeInternalChildDepth(Content, 10); // make sure regular content is at the bottom of this container
    }
    public static FontUsage Font => FrameworkFont.Regular.With(size: 16);

    static MarginPadding InnerPadding => new() { Top = 3, Bottom = 3, Left = 4, Right = 4 };
    List<PopoverInstance> Popovers = new();

    public PopoverInstance Popover(Drawable popover, Drawable attachment, bool keepAlive, bool mouseBlocker = false)
    {
        if (popover == null) return null;
        var instance = Popovers.Find(e => e.Drawable == popover);
        if (instance != null) return instance;
        instance = new PopoverInstance(this)
        {
            Mirror = new Mirror(attachment, popover),
            Drawable = popover,
            KeepAlive = keepAlive
        };
        if (mouseBlocker)
            AddInternal(instance.MouseBlocker = new MouseBlocker(instance));
        AddInternal(instance.Mirror);
        Popovers.Add(instance);
        return instance;
    }

    public void CloseThrough(PopoverInstance popover)
    {
        var index = Popovers.IndexOf(popover);
        if (index < 0) return;
        for (var i = Popovers.Count - 1; i >= index; i--)
            ClosePopover(i);
    }
    public bool ClosePopover(int i)
    {
        if (i < 0) return false;
        var popover = Popovers[i];
        var dispose = !popover.KeepAlive;
        RemoveInternal(popover.Mirror, false);
        if (popover.MouseBlocker != null)
        {
            RemoveInternal(popover.MouseBlocker, true);
        }
        popover.Mirror.Kill(dispose);
        Popovers.RemoveAt(i);
        popover.OnClose?.Invoke();
        return true;
    }
    public bool ClosePopover(PopoverInstance popover) => ClosePopover(Popovers.IndexOf(popover));
    public bool ClosePopover(Drawable drawable) => ClosePopover(Popovers.FindIndex(e => e.Drawable == drawable));

    public class Mirror : CompositeDrawable
    {
        public Drawable Child;
        public Drawable Target; // could also push all events from Handle method to Target
        public void Kill(bool killChild)
        {
            // we can skip killing our children by removing them before we dispose
            if (!killChild && Child.Parent == this) ClearInternal(false);
            Dispose();
        }
        public Mirror(Drawable target, Drawable child)
        {
            Depth = 1; // make sure tooltips appear above us
            Target = target;
            Child = child;
            AddInternal(child);
        }
        public void UpdateMirror()
        {
            var localQuad = Parent.ToLocalSpace(Target.ScreenSpaceDrawQuad);
            Width = localQuad.Width;
            Height = localQuad.Height;
            Position = localQuad.TopLeft;
        }
    }

    protected override void Update()
    {
        base.Update();
        for (var i = Popovers.Count - 1; i >= 0; i--)
        {
            var popover = Popovers[i];
            if (popover.Drawable.Parent != popover.Mirror || !popover.Drawable.IsPresent)
            {
                ClosePopover(i);
                continue;
            }
            popover.Mirror.UpdateMirror();
        }
    }
    public class MouseBlocker : Drawable
    {
        readonly PopoverInstance Instance;
        public MouseBlocker(PopoverInstance instance)
        {
            Instance = instance;
            Depth = 1;
            RelativeSizeAxes = Axes.Both;
        }
        protected override bool Handle(UIEvent e)
        {
            if (e is MouseEvent)
            {
                if (e is MouseButtonEvent || e is ScrollEvent)
                    ((DrumPopoverContainer)Parent).CloseThrough(Instance);
                return true;
            }
            if (e is KeyDownEvent kde && kde.Key == Key.Escape)
            {
                ((DrumPopoverContainer)Parent).CloseThrough(Instance);
                return true;
            }
            return false;
        }
    }

    public class CustomTooltip : CompositeDrawable, ITooltip
    {
        readonly SpriteText text;
        object loaded;
        public void SetContent(object content)
        {
            if (content.Equals(loaded)) return;
            {
                if (loaded is Drawable d)
                    RemoveInternal(d, false); // not disposed. make sure the owner handles disposal
                else if (loaded is MarkupTooltipData markup)
                {
                    var child = (MarkupText)InternalChildren[2];
                    if (content is MarkupTooltipData newMarkup)
                    {
                        loaded = content;
                        child.Data = newMarkup.Data;
                        return;
                    }
                    else
                        RemoveInternal(child, true);
                }
                else if (loaded is MultilineTooltipData loadedData)
                {
                    var child = (TextFlowContainer)InternalChildren[2];
                    if (content is MultilineTooltipData newData)
                    {
                        if (loadedData.Data == newData.Data) return;
                        loaded = content;
                        child.Clear(true);
                        child.AddText(newData.Data);
                        return;
                    }
                    else
                        RemoveInternal(child, true);
                }
            }
            loaded = content;
            {
                if (content is Drawable d)
                {
                    text.Alpha = 0;
                    AddInternal(d);
                }
                else if (content is MarkupTooltipData markup)
                {
                    text.Alpha = 0;
                    AddInternal(new MarkupText(markup.Data) { Padding = InnerPadding });
                }
                else if (content is MultilineTooltipData ml)
                {
                    text.Alpha = 0;
                    AddInternal(new TextFlowContainer { Padding = InnerPadding, Text = ml.Data, AutoSizeAxes = Axes.Both });
                }
                else
                {
                    text.Alpha = 1;
                    // If we need multiline tooltips, we should create a IHasMultilineTooltip
                    // It should have a static ITooltip instance that gets returned by GetCustomTooltip
                    // The content for that custom tooltip will be set based on TooltipContent
                    text.Text = content.ToString();
                }
            }
        }
        public CustomTooltip()
        {
            Alpha = 0;
            AutoSizeAxes = Axes.Both;
            BorderColour = DrumColors.LightBorder;
            BorderThickness = 2;
            Masking = true;
            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = DrumColors.DarkBackground,
            });
            AddInternal(text = new SpriteText
            {
                Font = Font,
                Padding = InnerPadding
            });
        }
        public void Move(Vector2 pos) => Position = pos;
    }
}
