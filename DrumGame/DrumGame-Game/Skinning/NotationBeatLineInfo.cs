using System.ComponentModel;
using System.Text.Json.Serialization;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;

namespace DrumGame.Game.Skinning;

public class NotationBeatLineInfo
{
    public Colour4 Color;

    float? _alpha;
    public float Alpha { get => _alpha ?? 0; set { _alpha = value; } }
    public float Width;
    public float Height;
    public void LoadDefaults(bool measure)
    {
        if (_alpha == default) Alpha = measure ? 0.4f : 0f;
        if (Color == default) Color = DrumColors.Blue;
        if (Height == default) Height = measure ? 2 : 1;
        if (Width == default) Width = measure ? 0.5f : 0.2f;
    }
}