using System;
using System.Linq;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Views.Settings;

public class SettingsBlockHeader : SpriteText
{
    public string FilterString => Text.ToString();
}