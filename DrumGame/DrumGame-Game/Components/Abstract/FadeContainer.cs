using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Components.Abstract;

public abstract class FadeContainer : CompositeDrawable
{
    public virtual Colour4 BackgroundColour => Colour4.Black.MultiplyAlpha(0.75f);
    public virtual double VisibleTime => 2000;
    public virtual double FadeTime => 800;
    public double DisplayTime; // doesn't need to be initialized since Alpha = 0 at start

    public FadeContainer()
    {
        Alpha = 0;
        var bg = BackgroundColour;
        if (bg.A > 0)
        {
            AddInternal(new Box
            {
                Colour = bg,
                RelativeSizeAxes = Axes.Both
            });
        }
    }

    protected override bool OnMouseMove(MouseMoveEvent e)
    {
        Touch();
        return base.OnMouseMove(e);
    }

    public virtual void Touch()
    {
        DisplayTime = Clock.CurrentTime;
        Alpha = 1;
    }

    protected override void Update()
    {
        // updates only occur when Alpha > 0, so we don't have to worry about this running extra
        var dt = Clock.CurrentTime - DisplayTime;
        if (dt > VisibleTime + FadeTime)
        {
            Alpha = 0; // this will stop all future updates
        }
        else if (dt > VisibleTime)
        {
            Alpha = (float)(1 - (dt - VisibleTime) / FadeTime);
        }
        base.Update();
    }
}