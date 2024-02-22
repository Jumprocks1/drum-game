using osu.Framework.Graphics.UserInterface;

namespace DrumGame.Game.Interfaces;

public interface IHasContextMenuEvent
{
    void ContextMenuStateChanged(MenuState state);
}