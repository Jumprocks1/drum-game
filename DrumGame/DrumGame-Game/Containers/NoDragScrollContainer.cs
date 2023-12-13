using DrumGame.Game.Components.Basic;
using osu.Framework.Input.Events;


namespace DrumGame.Game.Containers;

public class NoDragScrollContainer : DrumScrollContainer
{
    protected override bool OnDragStart(DragStartEvent e) => false;
}
