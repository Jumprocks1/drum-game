using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Browsers;

public class VoteDisplay : CompositeDrawable, IHasCommandInfo
{
    SpriteText Text = new SpriteText
    {
        Font = FrameworkFont.Regular.With(size: 18),
        X = 10
    };
    public void SetCurrent(int rating)
    {
        var ratingLoaded = rating != int.MinValue;
        Text.Text = ratingLoaded ? rating.ToString() : string.Empty;
    }
    public VoteDisplay()
    {
        AutoSizeAxes = Axes.Both;
        Padding = new MarginPadding(6); // padding helps mouse events not miss
        AddInternal(Text);
        AddInternal(new Triangle
        {
            Width = 7,
            Height = 7,
            Colour = DrumColors.Upvote
        });
        AddInternal(new Triangle
        {
            Width = 7,
            Height = 7,
            Y = 18,
            Scale = new osuTK.Vector2(1, -1),
            Colour = DrumColors.Downvote
        });
    }

    bool upvote;

    public CommandInfo CommandInfo => new CommandInfo(upvote ? Command.UpvoteMap : Command.DownvoteMap, upvote ? "Upvote" : "Downvote")
    {
        Parameter = this.FindClosestParent<BeatmapCard>()?.Map
    };

    protected override bool OnMouseMove(MouseMoveEvent e)
    {
        var pos = ToLocalSpace(e.ScreenSpaceMousePosition).Y;
        upvote = pos < this.DrawHeight / 2;
        return base.OnMouseMove(e);
    }
    protected override bool OnHover(HoverEvent e)
    {
        var pos = ToLocalSpace(e.ScreenSpaceMousePosition).Y;
        upvote = pos < this.DrawHeight / 2;
        return base.OnHover(e);
    }
}