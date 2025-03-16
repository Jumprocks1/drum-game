using DrumGame.Game.Channels;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using DrumGame.Game.Skinning;
using osuTK;
using DrumGame.Game.Utils;

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
        CenterTarget = Config.IconPosition;
        RelativePositionAxes = Axes.Both;
        RelativeSizeAxes = Axes.Both;
        Inner.RelativeSizeAxes = Axes.Both;
        // this is to keep Y distances consistent on any scaling.
        // on super thin scalings, the Y axis lengthens so we have to
        // rely on it being relative to be consistent.
        Inner.RelativePositionAxes = Axes.Y;
        Inner.FillMode = FillMode.Fit;
        Inner.Anchor = Anchor.Centre;
        Inner.Origin = Anchor.Centre;
        AddInternal(Inner);
    }
    protected float CenterTarget;
    protected override void Update()
    {
        var centerPosition = (DrawHeight - Inner.DrawHeight) / 2;
        var targetPosition = CenterTarget * (DrawHeight - Inner.DrawHeight);
        Inner.Y = (targetPosition - centerPosition) / DrawHeight;
    }
    public virtual void Hit(float velocity) // velocity is 0 to 127/159 (with hi-reso)
    {
        Inner.ClearTransforms();
        Inner.Colour = Config.Color;

        var normVelocity = velocity / 92f;
        var IconAnimation = Util.Skin.Mania.IconAnimation;
        if (IconAnimation != IconAnimationStyle.DtxBounceDown)
            Inner.FadeColour(Colour4.White, 300);
        else
            Inner.FadeColour(Colour4.White, normVelocity * (15 * 7), Easing.InQuad);
        switch (IconAnimation)
        {
            case IconAnimationStyle.Expand:
                {
                    Inner.ScaleTo(1 + (normVelocity * 0.25f), 50, Easing.OutQuint)
                        .Then(e => e.ScaleTo(1, 150));
                }
                break;
            case IconAnimationStyle.BounceDown:
                {
                    var animationDistance = 15f / DrawHeight;
                    Inner.MoveToOffset(new Vector2(0, normVelocity * animationDistance), 50, Easing.OutQuint)
                        .Then(e => e.MoveToOffset(new Vector2(0, normVelocity * -animationDistance), 150));
                }
                break;
            case IconAnimationStyle.DtxBounceDown:
                {
                    var inOutRatio = 1d / 3d;
                    var animationTime = 5 * (8 + 15);
                    var animationDistance = 15f / DrawHeight;
                    Inner.MoveToOffset(new Vector2(0, normVelocity * animationDistance), inOutRatio * animationTime)
                        .Then(e => e.MoveToOffset(new Vector2(0, normVelocity * -animationDistance), (1 - inOutRatio) * animationTime));
                }
                break;
        }
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