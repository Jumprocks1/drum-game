using System;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;

namespace DrumGame.Game.Interfaces;

public interface IHasUrl : IHasTooltip
{
    string Url { get; }
    LocalisableString IHasTooltip.TooltipText => $"{Url} (Ctrl + Click to follow)";
}