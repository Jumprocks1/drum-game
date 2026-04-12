using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace DrumGame.Game.Components;

// TextFlowContainer has no way of adding a drawable as a new line with no weird paragraphing
public class DrumTextFlowContainer : TextFlowContainer
{
    public void AddLine(Drawable line)
        => AddPart(new TextPartManual([new NewLineContainer(false), line]));
}