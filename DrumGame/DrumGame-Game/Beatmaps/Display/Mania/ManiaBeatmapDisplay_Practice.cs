using System;
using DrumGame.Game.Beatmaps.Practice;

namespace DrumGame.Game.Beatmaps.Display.Mania;

public partial class ManiaBeatmapDisplay : BeatmapDisplay
{
    PracticeMode PracticeMode;
    public override PracticeInfoPanel StartPractice(PracticeMode practice)
    {
        PracticeMode = practice;
        AddInternal(PracticeInfoPanel = new PracticeInfoPanel(practice));
        return PracticeInfoPanel;
    }

    public override void ExitPractice(PracticeMode practice)
    {
        if (!IsDisposed) // these fail if we're already disposed/exiting the game (Alt+F4)
            RemoveInternal(PracticeInfoPanel, true);
        PracticeMode = null;
        PracticeInfoPanel = null;
    }
}