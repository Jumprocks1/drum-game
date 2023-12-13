namespace DrumGame.Game.Stores.DB;
public class BeatmapInfo
{
    public BeatmapInfo() { }
    public BeatmapInfo(string id) { Id = id; }
    public string Id { get; set; }
    public long PlayTime { get; set; }
    public int Rating { get; set; } // TODO add modified date
    public double LocalOffset { get; set; }
}
