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
    public override void EndPractice(PracticeMode practice)
    {
        practice.PracticeChanged -= UpdatePracticeOverlay;
        NoteContainer.Remove(LeftPracticeOverlay, true);
        LeftPracticeOverlay = null;
        NoteContainer.Remove(RightPracticeOverlay, true);
        RightPracticeOverlay = null;
        Remove(PracticeInfoPanel, true);
        PracticeInfoPanel = null;
    }
}