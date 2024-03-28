using DrumGame.Game.Channels;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using DrumGame.Game.Skinning;

namespace DrumGame.Game.Beatmaps.Display.Mania;

using LaneInfo = ManiaSkinInfo.ManiaSkinInfo_Lane;

public abstract class ManiaIcon : Container
{
    protected Container Inner = new();
    protected override Container Content => Inner;
    LaneInfo Config;
    public ManiaIcon(LaneInfo lane)
    {
        Config = lane;
        RelativePositionAxes = Axes.Both;
        RelativeSizeAxes = Axes.Both;
        Inner.RelativeSizeAxes = Axes.Both;
        Inner.FillMode = FillMode.Fit;
        Inner.Anchor = Anchor.Centre;
        Inner.Origin = Anchor.Centre;
        AddInternal(Inner);
    }
    protected float CenterTarget = 0.5f;
    protected override void Update()
    {
        var centerPosition = (DrawHeight - Inner.DrawHeight) / 2;
        var targetPosition = CenterTarget * (DrawHeight - Inner.DrawHeight);
        Inner.Y = targetPosition - centerPosition;
    }
    public virtual void Hit(float velocity) // velocity is 0 to 127/159 (with hi-reso)
    {
        Inner.ClearTransforms();
        Inner.Colour = Config.Color;
        Inner.FadeColour(Colour4.White, 300);
        Inner.ScaleTo(1 + (velocity / 92f * 0.25f), 50, Easing.OutQuint)
            .Then(e => e.ScaleTo(1, 150));
    }
}


public class ManiaCymbalIcon : ManiaIcon
{
    public ManiaCymbalIcon(LaneInfo lane) : base(lane)
    {
        if (lane.Channel == DrumChannel.Crash || lane.Channel == DrumChannel.China)
            CenterTarget = 0f;
        Add(new Circle
        {
            Width = 1,
            Height = 1,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Both
        });
        Add(new Circle
        {
            Width = 0.9f,
            Height = 0.9f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = new Colour4(60, 60, 60, 255),
            RelativeSizeAxes = Axes.Both
        });
        Add(new Circle
        {
            Width = 0.2f,
            Height = 0.2f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = lane.Color,
            RelativeSizeAxes = Axes.Both
        });
    }
}

public class ManiaFootIcon : ManiaIcon
{
    public ManiaFootIcon(LaneInfo lane) : base(lane)
    {
        Inner.FillAspectRatio = 0.5f;
        Add(new Box
        {
            Width = 0.8f,
            Height = 0.8f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Both
        });
        Add(new Box
        {
            Width = 0.7f,
            Height = 0.09f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Both,
            Colour = new Colour4(60, 60, 60, 255)
        });
        Add(new Box
        {
            Width = 0.7f,
            Height = 0.09f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativePositionAxes = Axes.Both,
            RelativeSizeAxes = Axes.Both,
            Colour = new Colour4(60, 60, 60, 255),
            Y = -0.15f
        });
        Add(new Box
        {
            Width = 0.7f,
            Height = 0.09f,
            Y = -0.3f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativePositionAxes = Axes.Both,
            RelativeSizeAxes = Axes.Both,
            Colour = new Colour4(60, 60, 60, 255)
        });
    }
}

public class SpriteManiaIcon : ManiaIcon
{
    public SpriteManiaIcon(LaneInfo lane) : base(lane)
    {
        var sprite = lane.Icon.MakeSprite();
        Add(sprite);
    }
}
public class ManiaDrumIcon : ManiaIcon
{
    public ManiaDrumIcon(LaneInfo lane) : base(lane)
    {
        if (lane.Channel == DrumChannel.SmallTom || lane.Channel == DrumChannel.MediumTom)
            CenterTarget = 0.1f;
        Add(new Circle
        {
            Width = 1,
            Height = 1,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Both
        });
        Add(new Circle
        {
            Width = 0.9f,
            Height = 0.9f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = lane.Color,
            RelativeSizeAxes = Axes.Both
        });
        Add(new Circle
        {
            Width = 0.7f,
            Height = 0.7f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = new Colour4(60, 60, 60, 255),
            RelativeSizeAxes = Axes.Both
        });
    }
}