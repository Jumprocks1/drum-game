using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Timing;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using DrumGame.Game.Utils;
using DrumGame.Game.Input;
using DrumGame.Game.Beatmaps.Editor;

namespace DrumGame.Game.Beatmaps.Display;

public abstract class BeatmapDisplay : CompositeDrawable
{
    public virtual void DisplayScoreEvent(ScoreEvent e) { }
    public virtual void HandleScoreChange() { }
    public abstract void ReloadNoteRange(AffectedRange range);
    public abstract void PullView(ViewTarget viewTarget);
    public abstract void OnDrumTrigger(DrumChannelEvent ev);

    protected BeatmapPlayerInputHandler InputHandler => Player.BeatmapPlayerInputHandler;
    protected BeatmapScorer Scorer => InputHandler.Scorer;
    public virtual void EnterPlayMode() // TODO should really just swap this with a mode change listener
    {
        Scorer.OnChange += HandleScoreChange;
        HandleScoreChange();
    }
    public virtual void LeavePlayMode()
    {
        Scorer.OnChange -= HandleScoreChange;
    }
    public BeatmapPlayer Player;
    public BeatClock Track => Player.Track;
    public Beatmap Beatmap => Player.Beatmap;
    [BackgroundDependencyLoader]
    private void load()
    {
        Util.CommandController.RegisterHandlers(this);
    }
    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }
}
