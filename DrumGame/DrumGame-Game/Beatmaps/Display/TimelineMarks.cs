using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Timing;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;

namespace DrumGame.Game.Beatmaps.Display;

public class TimelineMarks : CompositeDrawable
{
    private readonly Beatmap Beatmap;
    readonly TrackClock Track;
    public static readonly Colour4 BookmarkColour = Colour4.PaleVioletRed.MultiplyAlpha(0.5f);
    public static readonly Colour4 TempoChangeColour = Colour4.LightBlue.MultiplyAlpha(0.5f);
    public static readonly Colour4 MeasureChangeColour = Colour4.LightSeaGreen.MultiplyAlpha(0.5f);

    public TimelineMarks(Beatmap beatmap, TrackClock track)
    {
        Beatmap = beatmap;
        Track = track;
        RelativeSizeAxes = Axes.Both;
        LoadMarks();
    }

    public void Reload() => LoadMarks();
    private void LoadMarks()
    {
        ClearInternal();
        foreach (var bookmark in Beatmap.Bookmarks)
        {
            AddInternal(new BookmarkMark(bookmark)
            {
                X = (float)Track.PercentAt(Beatmap.MillisecondsFromBeat(bookmark.Time))
            });
        }
        var timing = Beatmap.TempoChanges;
        var start = (timing.Count > 0 && timing[0].Time == 0) ? 1 : 0;
        for (var i = start; i < timing.Count; i++)
        {
            AddInternal(new TempoMark(timing[i])
            {
                X = (float)Track.PercentAt(Beatmap.ToMilliseconds(timing[i]))
            });
        }
        var m = Beatmap.MeasureChanges;
        for (var i = 0; i < m.Count; i++)
        {
            AddInternal(new MeasureMark(m[i])
            {
                X = (float)Track.PercentAt(Beatmap.ToMilliseconds(m[i]))
            });
        }
    }

    class BookmarkMark : Mark
    {
        Bookmark bookmark;
        public override LocalisableString TooltipText => bookmark.Title;
        public BookmarkMark(Bookmark bookmark)
        {
            this.bookmark = bookmark;
            Colour = BookmarkColour;
        }
    }

    class TempoMark : Mark
    {
        TempoChange tempo;
        public override LocalisableString TooltipText => tempo.HumanBPM + " bpm";
        public TempoMark(TempoChange tempo)
        {
            this.tempo = tempo;
            Colour = TempoChangeColour;
        }
    }
    class MeasureMark : Mark
    {
        double beats;
        public override LocalisableString TooltipText => beats + " beats per measure";
        public MeasureMark(MeasureChange change)
        {
            beats = change.Beats;
            Colour = MeasureChangeColour;
        }
    }
    abstract class Mark : Box, IHasTooltip
    {
        public Mark()
        {
            Width = 10;
            Height = BeatmapTimeline.TimelineAreaHeight;
            RelativePositionAxes = Axes.X;
            Origin = Anchor.Centre;
            Anchor = Anchor.CentreLeft;
        }
        public abstract LocalisableString TooltipText { get; }
    }
}
