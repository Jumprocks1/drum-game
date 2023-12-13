namespace DrumGame.Game.Beatmaps.Editor;

// Classes for handling history generically (not related to beatmaps)
public interface IHistoryChange
{
    // returns true if the operation changed anything
    public bool Do();
    public void Undo();
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

    public bool Do()
    {
        var o = false;
        for (int i = 0; i < changes.Length; i++) o |= changes[i].Do();
        return o;
    }

    public void Undo()
    {
        for (int i = changes.Length - 1; i >= 0; i--) changes[i].Undo();
    }
}
