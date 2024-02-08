using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps;
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
            if (!bookmark.Title.StartsWith("!")) continue;

            var spl = bookmark.Title.Split(' ');
            var next = 1;
            var key = spl[0][1..];

            HitObjectData? ReadFilter()
            {
                HitObjectData? filter = null;
                if (spl.Length > next && char.IsLetter(spl[next][0]) && Enum.TryParse<DrumChannel>(spl[next], out var dc))
                {
                    filter = new HitObjectData(dc);
                    next += 1;
                }
                if (spl.Length > next && char.IsLetter(spl[next][0]) && Enum.TryParse<NoteModifiers>(spl[next], out var nm))
                {
                    if (filter is HitObjectData h)
                        filter = new HitObjectData(h.Channel, nm);
                    else
                        filter = new HitObjectData(DrumChannel.None, nm);
                    next += 1;
                }
                return filter;
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
                var target = bookmark.Title.AsSpan(JumpKey.Length);
                foreach (var b in beatmap.Bookmarks)
                {
                    if (target.Equals(b.Title, StringComparison.OrdinalIgnoreCase))
                    {
                        o.Add(new BookmarkJumpEvent(bookmark.Time, b.Time));
                        break;
                    }
                }
            }
            else if (key == VolumeKey)
            {
                o.Add(new BookmarkVolumeEvent(bookmark.Time)
                {
                    Filter = ReadFilter(),
                    Volume = (ReadNumber() ?? 100) / 100
                });
            }
            else if (key == CrescendoKey)
            {
                o.Add(new BookmarkCrescendoEvent(bookmark.Time)
                {
                    Filter = ReadFilter(),
                    LengthBeats = ReadNumber() ?? 4,
                    StartVolume = (ReadNumber() ?? 0) / 100,
                    Volume = (ReadNumber() ?? 100) / 100
                });
            }
            else if (key == EndKey)
            {
                o.Add(new BookmarkEndEvent(bookmark.Time));
            }
        }
        return o;
    }
    public virtual void Trigger(BeatClock clock) { }
}

public class BookmarkCrescendoEvent : BookmarkVolumeEvent
{
    public double LengthBeats;
    public double StartVolume;
    public BookmarkCrescendoEvent(double beat) : base(beat)
    {
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
    public HitObjectData? Filter;
    public BookmarkVolumeEvent(double beat) : this(beat, 1) { }
    public BookmarkVolumeEvent(double beat, double volume) : base(beat)
    {
        Volume = volume;
    }

    public virtual double? VolumeFor(HitObjectData data, double beat)
    {
        if (!Matches(data)) return null;
        return Volume;
    }

    public bool Matches(HitObjectData data)
    {
        if (Filter is HitObjectData f)
        {
            if (data.Channel == f.Channel && data.Modifiers == f.Modifiers)
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