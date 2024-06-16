using DrumGame.Game.Beatmaps.Practice;
using DrumGame.Game.Modifiers;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Beatmaps.Display;

public partial class MusicNotationBeatmapDisplay
{
    Box LeftPracticeOverlay;
    Box RightPracticeOverlay;
    float Spacing => Font.Spacing;
    public override PracticeInfoPanel StartPractice(PracticeMode practice)
    {
        // very similar to hidden overlay
        NoteContainer.Add(LeftPracticeOverlay = new Box
        {
            Colour = Util.Skin.Notation.PlayfieldBackground,
            Depth = -2,
            Y = -6,
            Height = 16,
        });
        NoteContainer.Add(RightPracticeOverlay = new Box
        {
            Colour = Util.Skin.Notation.PlayfieldBackground,
            Depth = -2,
            Y = -6,
            Height = 16,
        });
        practice.PracticeChanged += UpdatePracticeOverlay;
        UpdatePracticeOverlay(practice);
        Add(PracticeInfoPanel = new PracticeInfoPanel(practice));
        return PracticeInfoPanel;
    }
    public void UpdatePracticeOverlay(PracticeMode practice)
    {
        LeftPracticeOverlay.X = HiddenModifier.StartPosition;
        LeftPracticeOverlay.Width = (float)(practice.StartBeat * Spacing - HiddenModifier.StartPosition);
        LeftPracticeOverlay.Alpha = practice.Config.OverlayStrength;

        RightPracticeOverlay.X = (float)(practice.EndBeat * Spacing);
        RightPracticeOverlay.Width = (float)((Beatmap.QuarterNotes - practice.EndBeat) * Spacing);
        RightPracticeOverlay.Alpha = practice.Config.OverlayStrength;
    }
    // this doesn't get called during disposal (ie. exiting the game)
    public override void ExitPractice(PracticeMode practice)
    {
        practice.PracticeChanged -= UpdatePracticeOverlay;
        if (!IsDisposed) // these fail if we're already disposed/exiting the game (Alt+F4)
        {
            NoteContainer.Remove(LeftPracticeOverlay, true);
            NoteContainer.Remove(RightPracticeOverlay, true);
            Remove(PracticeInfoPanel, true);
        }
        LeftPracticeOverlay = null;
        RightPracticeOverlay = null;
        PracticeInfoPanel = null;
    }
}