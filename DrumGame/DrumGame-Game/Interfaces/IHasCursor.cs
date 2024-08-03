namespace DrumGame.Game.Interfaces;

public interface IHasHandCursor : IHasCursor
{
    SDL2.SDL.SDL_SystemCursor? IHasCursor.Cursor => SDL2.SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND;
}
public interface IHasCursor
{
    public SDL2.SDL.SDL_SystemCursor? Cursor { get; }
}