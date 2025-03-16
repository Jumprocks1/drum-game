using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace DrumGame.Game.Components.Basic;

public class DrumScrollContainer : ScrollContainer<Drawable>
{
    public const float ScrollbarSize = 8;
    public DrumScrollContainer(Direction scrollDirection = Direction.Vertical)
        : base(scrollDirection)
    {
        DistanceDecayScroll = 0.03;
        DistanceDecayJump = 0.03;
        DistanceDecayDrag = 0.01;
    }

    protected override ScrollbarContainer CreateScrollbar(Direction direction) => new DrumScrollbar(direction);

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        base.OnMouseDown(e);
        return false;
    }

    protected class DrumScrollbar : ScrollbarContainer
    {
        public DrumScrollbar(Direction direction)
            : base(direction)
        {
            Size = new Vector2(ScrollbarSize);
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = DrumColors.ActiveButton
            };
        }

        bool mouseDown = false;

        void UpdateColor()
        {
            Child.Colour =
                mouseDown ? DrumColors.ActiveButton * 1.4f :
                IsHovered ? DrumColors.ActiveButton * 1.2f : DrumColors.ActiveButton;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (!base.OnMouseDown(e)) return false;
            mouseDown = true;
            UpdateColor();
            return true;
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (e.Button != MouseButton.Left) return;
            mouseDown = false;
            UpdateColor();
            base.OnMouseUp(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            UpdateColor();
            base.OnHoverLost(e);
        }

        protected override bool OnHover(HoverEvent e)
        {
            UpdateColor();
            return base.OnHover(e) || true;
        }

        public override void ResizeTo(float val, int duration = 0, Easing easing = Easing.None)
        {
            var size = new Vector2(DrumScrollContainer.ScrollbarSize)
            {
                [(int)ScrollDirection] = val
            };
            this.ResizeTo(size, duration, easing);
        }
    }
}
