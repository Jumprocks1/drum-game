using DrumGame.Game.Containers;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Notation;

public class NoteContainer : NoMaskContainer
{
    double _beatCount;
    public double BeatCount
    {
        get => _beatCount; set
        {
            _beatCount = value;
            Width = (float)(Font.Spacing * _beatCount);
        }
    }
    MusicFont Font;
    public NoteContainer(MusicFont font, double beats = 0)
    {
        Height = 4;
        Font = font;
        BeatCount = beats;
        for (var i = 0; i < 5; i++)
        {
            Add(new Box
            {
                Colour = font.Skin.Notation.StaffLineColor,
                Height = font.EngravingDefaults.staffLineThickness,
                Y = i - font.EngravingDefaults.staffLineThickness / 2f,
                RelativeSizeAxes = Axes.X,
            });
        }
    }
}

