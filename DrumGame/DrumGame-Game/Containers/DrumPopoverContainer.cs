using System.Collections.Generic;
using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Input;

namespace DrumGame.Game.Containers;

public record MultilineTooltipData(string Data) { }
// just my shitty version of markdown
public record MarkupTooltipData(string Data)
{
    public static explicit operator LocalisableString(MarkupTooltipData b) => b.Data;
}
public class DrumPopoverContainer : TooltipContainer
{
    protected override double AppearDelay => 0;
    protected override ITooltip CreateTooltip() => new CustomTooltip();
    public DrumPopoverContainer()
    {
        RelativeSizeAxes = Axes.Both;
        ChangeInternalChildDepth(Content, 10); // make sure regular content is at the bottom of this container
    }
    public static FontUsage Font => FrameworkFont.Regular.With(size: 16);

    static MarginPadding InnerPadding => new() { Top = 3, Bottom = 3, Left = 4, Right = 4 };
    List<Mirror> Popovers = new();

    public void Popover(Drawable popover, Drawable attachment)
    {
        if (popover == null || Popovers.Exists(e => e.Child == popover)) return;
        var mirror = new Mirror(attachment, popover);
        AddInternal(mirror);
        Popovers.Add(mirror);
    }

    public void ClosePopover(Drawable popover, bool dispose = true)
    {
        if (popover == null) return;
        var i = Popovers.FindIndex(e => e.Child == popover);
        if (i == -1) return;
        RemoveInternal(Popovers[i], false);
        Popovers[i].Kill(dispose);
        Popovers.RemoveAt(i);
    }

    class Mirror : CompositeDrawable
    {
        public Drawable Child;
        public Drawable Target; // could also push all events from Handle method to Target
        public void Kill(bool killChild)
        {
            // we can skip killing our children by removing them before we dispose
            if (!killChild) ClearInternal(false);
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
            var mirror = Popovers[i];
            if (mirror.Child.Parent != mirror)
            {
                RemoveInternal(mirror, true);
                Popovers.RemoveAt(i);
                continue;
            }
            mirror.UpdateMirror();
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
