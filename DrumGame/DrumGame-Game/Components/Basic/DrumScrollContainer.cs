using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;

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
