using DrumGame.Game.Utils;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace DrumGame.Game.Beatmaps.Display.Mania;

public partial class ManiaBeatmapDisplay : BeatmapDisplay
{
    public override void PullView(ViewTarget target)
    {
        if (target == null) return;
        Track.Seek(Beatmap.MillisecondsFromBeat(target.Left));
    }

    void UpdateDrag()
    {
        var pos = ToLocalSpace(Util.InputManager.CurrentState.Mouse.Position);
        if (LastDragMouse != pos)
        {
            Track.Stop(); // make sure we didn't start the track back up
            var d = pos.Y - DragStart.Start.Y;
            Track.Seek(DragStart.Time + d / DrawHeight / ScrollRate, true);
            LastDragMouse = pos;
        }
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button == MouseButton.Middle || e.Button == MouseButton.Right)
        {
            LastDragMouse = e.MousePosition;
            DragStart = (Track.CurrentTime, LastDragMouse, Track.IsRunning);
            Dragging = true;
            Track.Stop();
            return true;
        }
        return base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button == MouseButton.Middle || e.Button == MouseButton.Right)
        {
            // Technically this isn't perfect since the track could be paused/started during drag
            // Ideally we would add a bindable lock to Track
            if (Dragging)
            {
                Track.CommitAsyncSeek();
                if (DragStart.Running) Track.Start();
                Dragging = false;
            }
        }
        base.OnMouseUp(e);
    }
    public bool Dragging;
    public Vector2 LastDragMouse;
    public (double Time, Vector2 Start, bool Running) DragStart;
}