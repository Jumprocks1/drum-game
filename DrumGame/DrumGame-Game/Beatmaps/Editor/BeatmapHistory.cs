using System;
using System.Collections.Generic;
using DrumGame.Game.Commands;

namespace DrumGame.Game.Beatmaps.Editor;

public partial class BeatmapEditor
{
    public event Action OnHistoryChange;
    public bool Dirty = false;
    public void ForceDirty()
    {
        Dirty = true;
        OnHistoryChange?.Invoke();
    }
    public int LastSave = -1;
    public int TOS = -1;
    public void MarkSaveHistory()
    {
        LastSave = TOS;
        Dirty = false;
        OnHistoryChange?.Invoke();
    }
    public int? MaxDepth;
    public List<IHistoryChange> HistoryStack = new();
    // index of the top element on the stack. Note that when pressing Redo, this will move up the stack
    public void PushChange(IHistoryChange change, bool triggerDo = true)
    {
        var changed = !triggerDo || change.Do();
        if (changed)
        {
            if (compositeTarget != null)
            {
                compositeTarget.Changes.Add(change);
            }
            else
            {
                Dirty = true; // after a regular change, we are always dirty
                              // if we undo our last save, then make a change, it is no longer possible to get to that save point
                if (LastSave > TOS) LastSave = -2;
                TOS += 1;
                Display.LogEvent($"Applied change {change.Description}");
                // we have to clear anything above the new TOS since that data is no longer valid
                HistoryStack.RemoveRange(TOS, HistoryStack.Count - TOS);
                HistoryStack.Add(change);
                OnHistoryChange?.Invoke();
            }
        }
    }

    public void PushChange(Action action, Action undo, string description) => PushChange(new BeatmapChange(action, undo, description));
    [CommandHandler]
    public void Undo()
    {
        if (TOS >= 0)
        {
            var e = HistoryStack[TOS];
            Display.LogEvent($"Undoing change {e.Description}");
            e.Undo();
            TOS -= 1;
            Dirty = LastSave != TOS;
            OnHistoryChange?.Invoke();
        }
    }
    [CommandHandler]
    public void Redo()
    {
        if (TOS < HistoryStack.Count - 1)
        {
            TOS += 1;
            Dirty = LastSave != TOS;
            var e = HistoryStack[TOS];
            Display.LogEvent($"Redoing change {e.Description}");
            e.Do();
            OnHistoryChange?.Invoke();
        }
    }
    CompositeTarget compositeTarget;

    private class CompositeTarget : IDisposable
    {
        public List<IHistoryChange> Changes = new();
        private Action OnComplete;
        public CompositeTarget(Action onComplete) { OnComplete = onComplete; }
        public void Dispose() => OnComplete();
    }

    // Make sure to call this with a using statement
    // This only really needs to be used if you are calling another methods which also pushes to history
    // If you have control over all the history pushes, you might as well just use `CompositeHistoryChange` directly
    // This is a no-op if there is already a CompositeTarget, since the parent call is more important
    public IDisposable UseCompositeChange(string description)
    {
        if (compositeTarget != null) return null;
        compositeTarget = new CompositeTarget(() =>
        {
            var change = new CompositeHistoryChange(description, compositeTarget.Changes.ToArray());
            compositeTarget = null;
            PushChange(change, false);
        });
        return compositeTarget;
    }
    public void MergeChange(IHistoryChange change)
    {
        if (TOS == -1) PushChange(change);
        else
        {
            if (change.Do())
            {
                Dirty = true;
                if (LastSave >= TOS) LastSave = -2;
                var replace = HistoryStack[TOS];
                if (replace.Description != change.Description)
                {
                    Display.LogEvent($"Applied change {change.Description}");
                }
                // should probably try collapsing changes so they don't cause 100 updates when undoing/redoing
                HistoryStack[TOS] = new CompositeHistoryChange(change.Description, replace, change);
                HistoryStack.RemoveRange(TOS + 1, HistoryStack.Count - TOS - 1);
                OnHistoryChange?.Invoke();
            }
        }
    }
    public void MergeChangeIf(IHistoryChange change, Func<IHistoryChange, bool> condition)
    {
        if (TOS >= 0 && condition(HistoryStack[TOS]))
            MergeChange(change);
        else
            PushChange(change);
    }
}
