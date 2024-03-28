using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Timing;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using DrumGame.Game.Utils;
using DrumGame.Game.Input;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Skinning;
using DrumGame.Game.Components;
using DrumGame.Game.Beatmaps.Practice;

namespace DrumGame.Game.Beatmaps.Display;

public abstract class BeatmapDisplay : CompositeDrawable
{
    public virtual void DisplayScoreEvent(ScoreEvent e) { }
    public virtual void HandleScoreChange() { }
    public abstract void ReloadNoteRange(AffectedRange range);
    public abstract void PullView(ViewTarget viewTarget);
    public abstract void OnDrumTrigger(DrumChannelEvent ev);

    protected BeatmapPlayerInputHandler InputHandler => Player.BeatmapPlayerInputHandler;
    public BeatmapScorer Scorer => InputHandler.Scorer;

    HitErrorDisplay HitErrorDisplay;
    public virtual void EnterPlayMode() // TODO should really just swap this with a mode change listener
    {
        AddInternal(HitErrorDisplay = new HitErrorDisplay(this, BeatmapScorer.HitWindows));
        Scorer.OnChange += HandleScoreChange;
        HandleScoreChange();
    }
    public virtual void LeavePlayMode()
    {
        RemoveInternal(HitErrorDisplay, true);
        HitErrorDisplay = null;
        Scorer.OnChange -= HandleScoreChange;
    }
    public BeatmapPlayer Player;
    public BeatClock Track => Player.Track;
    public Beatmap Beatmap => Player.Beatmap;
    [BackgroundDependencyLoader]
    private void load()
    {
        Util.CommandController.RegisterHandlers(this);
        SkinManager.SkinChanged += SkinChanged;
    }
    protected virtual void SkinChanged() { }
    protected override void Dispose(bool isDisposing)
    {
        SkinManager.SkinChanged -= SkinChanged;
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }
    public PracticeInfoPanel PracticeInfoPanel;
    public abstract PracticeInfoPanel StartPractice(PracticeMode practice);
    public abstract void EndPractice(PracticeMode practice);
}
