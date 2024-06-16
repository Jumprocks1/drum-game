using System;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace DrumGame.Game.Modals;

public interface IHasOverlayModalConfig
{
    Colour4 ModalBackgroundColor => DrumColors.ModalBackground;
}

public class OverlayModal<T> : CompositeDrawable, IModal where T : Drawable, new()
{
    public Action CloseAction { get; set; }
    public void Close() => CloseAction?.Invoke();
    public readonly T Child;
    public OverlayModal()
    {
        var bg = new ModalBackground(Close);
        Child = new T();
        if (Child is IHasOverlayModalConfig c)
            bg.Colour = c.ModalBackgroundColor;
        AddInternal(bg);
        AddInternal(Child);
    }
}