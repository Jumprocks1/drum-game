using System.Collections.Generic;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Containers;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;

namespace DrumGame.Game.Components.Overlays;

public class NotificationOverlay : CompositeDrawable
{
    const float width = 320;
    const float AnimationTime = 200;

    DrumScrollContainer Content;
    Container Container;

    public NotificationOverlay()
    {
        RelativeSizeAxes = Axes.Both;

        Container = new ClickBlockingContainer
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Width = width,
            RelativeSizeAxes = Axes.Y,
        };
        Container.Add(new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = DrumColors.DarkBackground
        });

        Container.Add(Content = new DrumScrollContainer { RelativeSizeAxes = Axes.Both });

        AddInternal(Container);
    }

    List<BackgroundTask> Tasks = new();

    public void Add(Drawable d, bool noPopup = false) // update thread
    {
        if (Content.Children.Count > 0)
        {
            var lastChild = Content.Children[^1];
            d.Y = lastChild.Y + lastChild.Height;
        }
        else d.Y = 0;
        Content.Add(d);
        if (!noPopup) Util.Palette.Notifications(); // force shows notifications
        Content.ScrollIntoView(d);
    }

    public void Register(BackgroundTask task)
    {
        Scheduler.Add(() =>
        {
            if (Tasks.Contains(task)) return;
            Tasks.Add(task);
            Add(new TaskDisplay(task), task.NoPopup);
        }, false);
    }

    // TODO readd animations
    // public override void Hide()
    // {
    //     if (!_visible) return;
    //     _visible = false;
    //     Container.MoveToX(width, AnimationTime, Easing.OutQuint);
    //     this.FadeTo(0, AnimationTime, Easing.OutQuint);
    // }

    // public override void Show()
    // {
    //     if (_visible) return;
    //     _visible = true;
    //     Container.MoveToX(0, AnimationTime, Easing.OutQuint);
    //     this.FadeTo(1, AnimationTime, Easing.OutQuint);
    // }


    class TaskDisplay : CompositeDrawable, IHasContextMenu
    {
        const float Spacing = 3;
        readonly BackgroundTask Task;
        readonly TooltipSpriteText NameText = new()
        {
            Font = FrameworkFont.Regular,
            X = Spacing,
            Y = Spacing
        };
        protected override void Update()
        {
            if (Task.Success)
                ProgressText.Text = "Completed";
            else
                ProgressText.Text = Task.FailureReason ?? Task.ProgressText;
        }
        readonly CustomTooltipSpriteText ProgressText = new()
        {
            Font = FrameworkFont.Regular,
            Y = 20 + Spacing,
            X = Spacing,
        };
        readonly Box Background = new()
        {
            RelativeSizeAxes = Axes.Both,
            Alpha = 0.1f
        };

        public MenuItem[] ContextMenuItems => ContextMenuBuilder.New(Task)
            .Add("Cancel", e => e.Cancel()).Danger().Disabled(Task.Completed)
            .Add("Copy exception text", e => Util.SetClipboard(e.Exception.ToString())).Disabled(Task.Exception == null)
            .Add("Copy failure message", e => Util.SetClipboard(e.FailureReason.ToString())).Disabled(Task.FailureReason == null)
            .Build();

        public TaskDisplay(BackgroundTask task)
        {
            Task = task;
            Task.OnCompleted += OnCompleted;
            Height = 50;
            RelativeSizeAxes = Axes.X;
            NameText.Text = task.Name;
            NameText.Tooltip = task.NameTooltip;
            AddInternal(Background);
            AddInternal(NameText);
            AddInternal(ProgressText);
            UpdateColor();
        }
        public void UpdateColor()
        {
            if (Task.Cancelled)
            {
                Background.Colour = DrumColors.Red;
            }
            else if (Task.Failed)
            {
                Background.Colour = DrumColors.Red;
            }
            else if (Task.Completed)
            {
                Background.Colour = DrumColors.Green;
            }
            else
            {
                Background.Colour = DrumColors.Yellow;
            }
            NameText.Text = Task.Name;
        }
        void OnCompleted()
        {
            if (Task.FailureDetails != null)
                ProgressText.TooltipContent = new MultilineTooltipData($"{Task.FailureDetails}");
            UpdateColor();
        }

        class CustomTooltipSpriteText : SpriteText, IHasCustomTooltip
        {
            public object TooltipContent { get; set; }
            public CustomTooltipSpriteText() : base() { }
            public ITooltip GetCustomTooltip() => null;
        }
    }
}