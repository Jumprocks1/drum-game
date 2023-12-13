using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Components.Basic;

public class ClickBlockingContainer : Container
{
    protected override bool OnMouseDown(MouseDownEvent e) => true;
}