namespace DrumGame.Game.Beatmaps.Editor;

// Classes for handling history generically (not related to beatmaps)
public interface IHistoryChange
{
    // returns true if the operation changed anything
    public bool Do(BeatmapEditor editor);
    public void Undo(BeatmapEditor editor);
    public string Description { get; }
}
public class CompositeHistoryChange : IHistoryChange
{
    IHistoryChange[] changes;
    public string Description { get; }
    public CompositeHistoryChange(string description, params IHistoryChange[] changes)
    {
        this.changes = changes;
        Description = description;
    }

    public bool Do(BeatmapEditor editor)
    {
        var o = false;
        for (int i = 0; i < changes.Length; i++) o |= changes[i].Do(editor);
        return o;
    }

    public void Undo(BeatmapEditor editor)
    {
        for (int i = changes.Length - 1; i >= 0; i--) changes[i].Undo(editor);
    }
}
