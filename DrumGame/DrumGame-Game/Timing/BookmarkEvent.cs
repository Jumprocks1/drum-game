using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Channels;

namespace DrumGame.Game.Timing;

public abstract class BookmarkEvent
{
    public readonly double Beat;
    public BookmarkEvent(double beat) { Beat = beat; }

    public const string JumpKey = "jump";
    public const string EndKey = "end";
    public const string VolumeKey = "v";
    public const string CrescendoKey = "cres";

    public static List<BookmarkEvent> CreateList(Beatmap beatmap)
    {
        var o = new List<BookmarkEvent>();

        foreach (var bookmark in beatmap.Bookmarks)
        {
            if (string.IsNullOrWhiteSpace(bookmark.Title)) continue;
            if (!bookmark.Title.StartsWith('!')) continue;

            foreach (var part in bookmark.Title.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var ev = ReadEvent(beatmap, part, bookmark.Time);
                if (ev != null) o.Add(ev);
            }
        }
        return o;
    }
    public virtual void Trigger(BeatClock clock) { }

    public static BookmarkEvent ReadEvent(Beatmap beatmap, string title, double time)
    {
        if (!title.StartsWith('!')) return null;
        var spl = title.Split(' ');
        var next = 1;
        var key = spl[0][1..];

        (HitObjectData?, bool) ReadFilter()
        {
            HitObjectData? data = null;
            var all = true;
            if (spl.Length > next && char.IsLetter(spl[next][0]) && Enum.TryParse<DrumChannel>(spl[next], out var dc))
            {
                data = new HitObjectData(dc);
                next += 1;
            }
            if (spl.Length > next && char.IsLetter(spl[next][0]) && Enum.TryParse<NoteModifiers>(spl[next], out var nm))
            {
                all = false;
                if (data is HitObjectData h)
                    data = new HitObjectData(h.Channel, nm);
                else
                    data = new HitObjectData(DrumChannel.None, nm);
                next += 1;
            }
            return (data, all);
        }

        double? ReadNumber()
        {
            if (spl.Length > next && double.TryParse(spl[next], out var d))
            {
                next += 1;
                return d;
            }
            return null;
        }

        if (key == JumpKey)
        {
            var target = title.AsSpan(JumpKey.Length);
            foreach (var b in beatmap.Bookmarks)
            {
                if (target.Equals(b.Title, StringComparison.OrdinalIgnoreCase))
                {
                    return new BookmarkJumpEvent(time, b.Time);
                }
            }
        }
        else if (key == VolumeKey)
        {
            return new BookmarkVolumeEvent(time)
            {
                Filter = ReadFilter(),
                Volume = (ReadNumber() ?? 100) / 100
            };
        }
        else if (key == CrescendoKey)
        {
            return new BookmarkCrescendoEvent(time)
            {
                Filter = ReadFilter(),
                LengthBeats = ReadNumber() ?? 4,
                StartVolume = (ReadNumber() ?? 0) / 100,
                Volume = (ReadNumber() ?? 100) / 100
            };
        }
        else if (key == EndKey) return new BookmarkEndEvent(time);
        return null;
    }
}

public class BookmarkCrescendoEvent : BookmarkVolumeEvent
{
    public double LengthBeats;
    public double StartVolume;
    public BookmarkCrescendoEvent(double beat) : base(beat)
    {
    }

    public override double? SizeFor(HitObjectData data, double beat)
    {
        var v = VolumeFor(data, beat);
        if (v == null) return null;
        return Math.Sqrt(v.Value);
    }
    public override double? VolumeFor(HitObjectData data, double beat)
    {
        if (!Matches(data)) return null;
        return Math.Clamp((beat - Beat) / LengthBeats, 0, 1) * (Volume - StartVolume) + StartVolume;
    }
}
public class BookmarkEndEvent(double beat) : BookmarkEvent(beat) { }
public class BookmarkVolumeEvent : BookmarkEvent
{
    public double Volume;
    public HitObjectData? FilterData;
    public bool All;
    public (HitObjectData? Data, bool All) Filter
    {
        set
        {
            FilterData = value.Data;
            All = value.All;
        }
    }
    public BookmarkVolumeEvent(double beat) : this(beat, 1) { }
    public BookmarkVolumeEvent(double beat, double volume) : base(beat)
    {
        Volume = volume;
    }

    public virtual double? SizeFor(HitObjectData data, double beat)
    {
        return 1;
    }
    public virtual double? VolumeFor(HitObjectData data, double beat)
    {
        if (!Matches(data)) return null;
        return Volume;
    }

    public bool Matches(HitObjectData data)
    {
        if (FilterData is HitObjectData f)
        {
            if (data.Channel == f.Channel && (All || data.Modifiers == f.Modifiers))
            {
                return true;
            }
            return false;
        }
        return true;
    }
}

public class BookmarkJumpEvent : BookmarkEvent
{
    public readonly double Target;
    public BookmarkJumpEvent(double beat, double target) : base(beat)
    {
        Target = target;
    }
    public override void Trigger(BeatClock clock) => clock.SeekToBeat(Target);
}