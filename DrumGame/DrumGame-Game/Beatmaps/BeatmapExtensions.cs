using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DrumGame.Game.API;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Channels;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Stores;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Logging;

namespace DrumGame.Game.Beatmaps;

public partial class Beatmap
{
    public double BeatFromMilliseconds(double milliseconds)
    {
        milliseconds -= TotalOffset;
        if (milliseconds < 0)
        {
            var negativeTempo = TempoChanges.Count == 0 || TempoChanges[0].Time > 0 ? TempoChange.DefaultTempo : TempoChanges[0].Tempo;
            return milliseconds * 1_000 / negativeTempo.MicrosecondsPerQuarterNote;
        }
        var tempo = TempoChange.Default;
        double lastTime = 0;
        foreach (var tempoChange in TempoChanges)
        {
            var dt = tempoChange.Time - tempo.Time;
            var realTime = lastTime + (double)dt / TickRate * tempo.Tempo.MicrosecondsPerQuarterNote / 1_000;
            if (realTime >= milliseconds)
            {
                return (double)tempo.Time / TickRate + (milliseconds - lastTime) * 1_000 / tempo.Tempo.MicrosecondsPerQuarterNote;
            }
            tempo = tempoChange;
            lastTime = realTime;
        }
        return (double)tempo.Time / TickRate + (milliseconds - lastTime) * 1_000 / tempo.Tempo.MicrosecondsPerQuarterNote;
    }
    public double MillisecondsFromBeat(double beat)
    {
        if (beat < 0)
        {
            var negativeTempo = TempoChanges.Count == 0 || TempoChanges[0].Time > 0 ? TempoChange.DefaultTempo : TempoChanges[0].Tempo;
            return beat * negativeTempo.MicrosecondsPerQuarterNote / 1_000 + TotalOffset;
        }
        var tempo = TempoChange.DefaultTempo.MicrosecondsPerQuarterNote;
        var ticks = beat * TickRate;
        var quarterNote = TickRate;
        var realTime = 0.0;
        var lastEvent = 0;
        foreach (var ev in TempoChanges)
        {
            if (ev.Time > ticks)
            {
                return realTime + (ticks - lastEvent) / TickRate * tempo / 1000 + TotalOffset;
            }
            var delta = ev.Time - lastEvent;
            if (delta > 0)
            {
                realTime += (double)delta / quarterNote * tempo / 1000; ;
            }
            tempo = ev.Tempo.MicrosecondsPerQuarterNote;
            lastEvent = ev.Time;
        }
        return realTime + (ticks - lastEvent) / TickRate * tempo / 1000 + TotalOffset;
    }
    public double MillisecondsFromTick(int tick) => MillisecondsFromBeat((double)tick / TickRate);
    public double MillisecondsFromTick(ITickTime tick) => MillisecondsFromTick(tick.Time);
    public double ToMilliseconds(ITickTime o) => MillisecondsFromBeat((double)o.Time / TickRate);
    public int TickFromBeat(double beat) => (int)(beat * TickRate + 0.5); // works best for positive beats
    public static int TickFromBeat(double beat, int tickRate) => (int)(beat * tickRate + 0.5);
    public int TickFromBeatSlow(double beat) => (int)Math.Round(beat * TickRate);
    public double BeatFromTick(int tick) => (double)tick / TickRate;

    public bool DisableSaving; // used to prevent saving after applying modifiers

    public T RecentChangeTicks<T>(int ticks) where T : IBeatmapChangePoint<T>
    {
        var list = T.GetList(this);
        if (ticks <= 0)
            return list.Count == 0 || list[0].Time > 0 ? T.Default : list[0];
        var change = T.Default;
        foreach (var ev in list)
        {
            if (ev.Time > ticks) return change;
            change = ev;
        }
        return change;
    }
    public T RecentChange<T>(double beats) where T : IBeatmapChangePoint<T> => RecentChangeTicks<T>(TickFromBeat(beats));

    // Note, this always constructs a new ChangePoint such that it occurs at `beat` with the settings from the previous point
    public T ChangeAt<T>(double beat) where T : IBeatmapChangePoint<T>
    {
        var ticks = TickFromBeat(beat);
        return RecentChangeTicks<T>(ticks).WithTime(ticks);
    }
    public void UpdateChangePoint<T>(int t, Func<T, T> update) where T : class, IBeatmapChangePoint<T>
    {
        T found = null;
        T previous = null;
        var insertAt = 0;
        var list = T.GetList(this);
        foreach (var change in list)
        {
            if (change.Time == t) { found = change; break; }
            if (change.Time > t) break;
            insertAt += 1;
            previous = change;
        }
        if (found != null)
        {
            list[insertAt] = update(found);
        }
        else
        {
            list.Insert(insertAt, update((previous ?? T.Default).WithTime(t)));
        }
    }
    public void AddExtraDefault<T>() where T : class, IBeatmapChangePoint<T> // only use this if we plan on also calling RemoveExtras later
    {
        var list = T.GetList(this);
        if (list.Count == 0 || list[0].Time != 0) list.Insert(0, T.Default);
    }
    public static void AddExtraDefault<T>(List<T> list) where T : class, IBeatmapChangePoint<T> // only use this if we plan on also calling RemoveExtras later
    {
        if (list.Count == 0 || list[0].Time != 0) list.Insert(0, T.Default);
    }
    public void RemoveExtras<T>() where T : class, IBeatmapChangePoint<T>
    {
        var list = T.GetList(this);
        T prev = T.Default;
        var i = 0;
        while (i < list.Count)
        {
            var e = list[i];
            if (e.Congruent(prev)) list.RemoveAt(i);
            else
            {
                if (i > 0 && e.Time == prev.Time) list.RemoveAt(i - 1);
                else i++;
                prev = e;
            }
        }
    }
    public void SnapMeasureChanges()
    {
        var prev = MeasureChange.Default;
        for (var i = 0; i < MeasureChanges.Count; i++)
        {
            var e = MeasureChanges[i];
            var measure = MeasureFromTick(e.Time);
            var tick = TickFromMeasure(measure);
            if (tick != e.Time) // measure change not exactly on a measure
            {
                var next = tick + TickFromBeat(prev.Beats);
                if (next - e.Time < e.Time - tick) tick = next;
                MeasureChanges[i] = e.WithTime(tick);
            }
            prev = e;
        }
    }
    public IOrderedEnumerable<double> GetTimelineMarks()
    {
        return Bookmarks.Select(e => e.Time)
            .Concat(TempoChanges.Select(e => (double)e.Time / TickRate))
            .Concat(MeasureChanges.Select(e => (double)e.Time / TickRate))
            .Append(BeatFromMilliseconds(-LeadIn))
            .OrderBy(e => e);
    }

    public string SaveToDisk(MapStorage mapStorage, MapImportContext context = null)
    {
        if (DisableSaving)
            throw new UserException("Map saving disabled. Likely caused by application of a modifier.");
        // if (Notes == null) throw new Exception("Attempted to save a beatmap before it was exported. Please report this issue to the developer.");
        var target = Source.AbsolutePath;
        if (!target.EndsWith(".bjson", true, CultureInfo.InvariantCulture))
            throw new UserException("Can only save .bjson files");
        using var stream = mapStorage.GetStream(target, FileAccess.Write, FileMode.Create);
        using var writer = new StreamWriter(stream);
        context ??= MapImportContext.Current;
        if (context != null)
        {
            context.NewMaps.Add(mapStorage.RelativePath(target));
            Mapper ??= context.Author;
        }
        var s = stream as FileStream;
        Logger.Log($"Saving to {s.Name}", level: LogLevel.Important);
        var serializer = new JsonSerializer
        {
            ContractResolver = BeatmapContractResolver.Default,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
        };
        serializer.Serialize(writer, this);
        mapStorage.ReplaceMetadata(mapStorage.RelativePath(target), this);
        // Logger.Log($"Save complete", level: LogLevel.Important); // expected that the caller will log this
        return s.Name;
    }

    public static readonly HashSet<DrumChannel> Cymbols = new()
    {
        DrumChannel.ClosedHiHat,
        DrumChannel.OpenHiHat,
        DrumChannel.HalfOpenHiHat,
        DrumChannel.Ride,
        DrumChannel.RideBell,
        DrumChannel.Crash,
        DrumChannel.Splash,
        DrumChannel.China,
        DrumChannel.Rim,
    };

    public static HashSet<DrumChannel>[] ChannelGroups = new HashSet<DrumChannel>[] {
            Cymbols,
            new() { DrumChannel.Snare, DrumChannel.SideStick }
        };

    public static HashSet<DrumChannel> GetGroup(DrumChannel channel)
    {
        foreach (var group in ChannelGroups)
        {
            if (group.Contains(channel)) return group;
        }
        return null;
    }

    public bool AddHit(int tick, HitObjectData data, bool toggle = true, bool force = false)
    {
        var hitObject = new HitObject(tick, data);
        var pos = HitObjects.InsertSortedPosition(hitObject);

        var channel = data.Channel;
        var group = force ? null : GetGroup(channel);
        int? replace = null;
        // somehow this got out of bounds at one point
        // I think pos came in at a maximum possible value
        foreach (var j in GetHitObjectsAtTick(hitObject.Time, pos))
        {
            var ho = HitObjects[j];
            var h = ho.Data.Channel;
            if (group?.Contains(h) ?? h == channel)
            {
                // if we find a perfect match, we can stop searching this tick
                if (ho.Data == data)
                {
                    if (toggle) HitObjects.RemoveAt(j);
                    return false;
                }
                else
                {
                    // we can't replace this immediately in-case there's a perfect match on this same tick
                    // if we replaced without searching for the perfect match, we could get a duplicate
                    replace = j;
                }
            }
        }
        if (replace is int replaceV)
        {
            HitObjects[replaceV] = HitObjects[replaceV].With(data);
        }
        else
        {
            HitObjects.Insert(pos, hitObject);
            QuarterNotes = Math.Max(tick / TickRate + 1, QuarterNotes);
        }
        return true;
    }
    public AffectedRange CycleModifier(BeatSelection selection, int stride)
    {
        var hits = GetHitObjectsAt(selection, stride).ToList();
        if (hits.Count == 0) return false;
        // algorithm:
        //    if we find any notes that are not ghost and not accented, remove ghost and add accents to all notes
        //    if we find any accented notes, remove accent and add ghost to all notes
        //    otherwise, remove ghost and accents from all notes
        if (hits.Any(e => (HitObjects[e].Modifiers & NoteModifiers.AccentedGhost) == 0))
        {
            foreach (var hit in hits) HitObjects[hit] = HitObjects[hit].With(HitObjects[hit].Modifiers & ~NoteModifiers.Ghost | NoteModifiers.Accented);
        }
        else if (hits.Any(e => HitObjects[e].Modifiers.HasFlagFast(NoteModifiers.Accented)))
        {
            foreach (var hit in hits) HitObjects[hit] = HitObjects[hit].With(HitObjects[hit].Modifiers & ~NoteModifiers.Accented | NoteModifiers.Ghost);
        }
        else
        {
            foreach (var hit in hits) HitObjects[hit] = HitObjects[hit].With(HitObjects[hit].Modifiers & ~NoteModifiers.AccentedGhost);
        }
        return AffectedRange.FromSelection(selection, this);
    }
    public AffectedRange CycleSticking(BeatSelection selection, int stride)
    {
        var hits = GetHitObjectsAt(selection, stride).ToList();
        if (hits.Count == 0) return false;
        if (hits.Any(e => HitObjects[e].Modifiers.HasFlagFast(NoteModifiers.Left)))
        {
            foreach (var hit in hits) HitObjects[hit] = HitObjects[hit].With(HitObjects[hit].Modifiers & ~NoteModifiers.Left | NoteModifiers.Right);
        }
        else if (hits.Any(e => HitObjects[e].Modifiers.HasFlagFast(NoteModifiers.Right)))
        {
            foreach (var hit in hits) HitObjects[hit] = HitObjects[hit].With(HitObjects[hit].Modifiers & ~NoteModifiers.LeftRight);
        }
        else
        {
            foreach (var hit in hits) HitObjects[hit] = HitObjects[hit].With(HitObjects[hit].Modifiers & ~NoteModifiers.Right | NoteModifiers.Left);
        }
        return AffectedRange.FromSelection(selection, this);
    }

    public IEnumerable<int> GetHitObjectsAtTick(int t, int pos)
    {
        var j = pos - 1;
        while (j >= 0 && HitObjects[j].CompareTo(t) == 0)
        {
            yield return j;
            j -= 1;
        }
        j = pos;
        while (j < HitObjects.Count && HitObjects[j].CompareTo(t) == 0)
        {
            yield return j;
            j += 1;
        }
    }
    public IEnumerable<int> GetHitObjectsAtTick(int t) => GetHitObjectsAtTick(t, HitObjects.BinarySearch(t));
    public IEnumerable<int> GetHitObjectsAt(double beat) => GetHitObjectsAtTick(TickFromBeat(beat));
    public IEnumerable<int> GetHitObjectsAt(BeatSelection selection, int tickStride)
    {
        IEnumerable<int> it()
        {
            var target = TickFromBeat(selection.Left);
            var end = TickFromBeat(selection.Right);
            var startIndex = HitObjects.BinarySearchFirst(target);
            for (int i = startIndex; i < HitObjects.Count; i++)
            {
                var ho = HitObjects[i];
                if (ho.Time > target) { target += ((ho.Time - target - 1) / tickStride + 1) * tickStride; }
                if (target >= end) break;
                if (ho.Time == target)
                {
                    yield return i;
                }
            }
        }
        return selection.HasVolume ? it() : GetHitObjectsAt(selection.Left);
    }
    public IEnumerable<int> GetHitObjectsIn(BeatSelection selection)
    {
        IEnumerable<int> it()
        {
            var end = TickFromBeat(selection.Right);
            var startIndex = HitObjects.BinarySearchFirst(TickFromBeat(selection.Left));
            for (int i = startIndex; i < HitObjects.Count; i++)
            {
                if (HitObjects[i].Time >= end) break;
                yield return i;
            }
        }
        return selection.HasVolume ? it() : GetHitObjectsAt(selection.Left);
    }

    public AffectedRange RemoveHits(BeatSelection selection)
    {
        var t0 = TickFromBeat(selection.Left);
        if (selection.HasVolume)
        {
            var t1 = TickFromBeat(selection.Right);
            return HitObjects.RemoveAll(e => e.Time >= t0 && e.Time < t1) > 0 ? AffectedRange.FromSelection(selection, this) : false;
        }
        else
        {
            return HitObjects.RemoveAll(e => e.Time == t0) > 0 ? t0 : false;
        }
    }
    public AffectedRange ApplyStrideAction(BeatSelection selection, int stride, Func<int, bool, bool> action, bool allowToggle = false)
    {
        if (!selection.HasVolume) return action(TickFromBeat(selection.Start), true);
        var start = TickFromBeat(selection.Left);
        var end = TickFromBeat(selection.Right);
        var changed = false;
        for (int t = start; t < end; t += stride)
        {
            if (action(t, false)) changed = true;
        }
        if (allowToggle && !changed)
        {
            changed = true;
            for (int t = start; t < end; t += stride) action(t, true);
        }
        return changed ? AffectedRange.FromSelection(selection, this) : false;
    }
    public AffectedRange AddHits(BeatSelection selection, int stride, HitObjectData data, bool allowToggle = false)
        => ApplyStrideAction(selection, stride, (t, toggle) => AddHit(t, data, toggle), allowToggle);
    public AffectedRange AddRoll(BeatSelection selection, HitObjectData data)
    {
        var left = TickFromBeat(selection.Left);
        var duration = TickFromBeat(selection.Right) - left;
        return AddRoll(left, duration, data);
    }
    public AffectedRange AddRoll(int time, int duration, HitObjectData data)
    {
        if (duration > 0)
        {
            var channel = data.Channel;
            var end = time + duration;
            var hitObject = new RollHitObject(time, data, duration);
            HitObjects.RemoveAll(e => e.Time > hitObject.Time && e.Time < end && e.Data.Channel == channel);
            var pos = HitObjects.InsertSortedPosition(hitObject);
            var group = GetGroup(channel);
            foreach (var j in GetHitObjectsAtTick(hitObject.Time, pos))
            {
                var ho = HitObjects[j];
                var h = ho.Data.Channel;
                if (group?.Contains(h) ?? h == channel)
                {
                    HitObjects[j] = hitObject;
                    return new AffectedRange(time, end);
                }
            }
            HitObjects.Insert(pos, hitObject);
            return new AffectedRange(time, end);
        }
        return false;
    }

    // These methods largely rely on MeasureChanges being on measure lines, so be careful
    public int MeasureFromBeat(double beat)
    {
        if (beat <= 0) return 0;
        if (MeasureChanges.Count == 0)
        {
            return (int)(beat / 4);
        }
        else if (MeasureChanges.Count == 1 && MeasureChanges[0].Time == 0)
        {
            return (int)(beat / MeasureChanges[0].Beats); // this might be a bit sketchy due to floating point division
        }
        else
        {
            return MeasureFromTick(TickFromBeat(beat));
        }
    }
    public int MeasureFromTickNegative(int tick)
    {
        if (tick < 0)
        {
            int measureTicks;
            if (MeasureChanges.Count == 0 || MeasureChanges[0].Time != 0)
                measureTicks = TickRate * 4;
            else
                measureTicks = TickFromBeat(MeasureChanges[0].Beats);
            return (tick - measureTicks + 1) / measureTicks;
        }
        else return MeasureFromTick(tick);
    }
    public int MeasureFromTick(int tick)
    {
        if (tick <= 0) return 0;
        if (MeasureChanges.Count == 0)
        {
            return tick / (TickRate * 4);
        }
        else if (MeasureChanges.Count == 1 && MeasureChanges[0].Time == 0)
        {
            return tick / TickFromBeat(MeasureChanges[0].Beats);
        }
        else
        {
            var previousTick = 0;
            var beats = MeasureChange.DefaultBeats;
            var measure = 0;
            for (int i = 0; i < MeasureChanges.Count; i++)
            {
                if (MeasureChanges[i].Time >= tick) break;
                measure += (MeasureChanges[i].Time - previousTick) / ((int)(beats * TickRate + 0.5));
                previousTick = MeasureChanges[i].Time;
                beats = MeasureChanges[i].Beats;
            }
            return measure + (tick - previousTick) / ((int)(beats * TickRate + 0.5));
        }
    }
    public double BeatFromMeasure(int measure)
    {
        if (MeasureChanges.Count == 0)
        {
            return measure * 4;
        }
        else if (MeasureChanges.Count == 1 && MeasureChanges[0].Time == 0)
        {
            return measure * MeasureChanges[0].Beats;
        }
        else
        {
            var remainingMeasures = measure;
            var beat = 0d;
            var previousTick = 0;
            var beats = MeasureChange.DefaultBeats;
            for (int i = 0; i < MeasureChanges.Count; i++)
            {
                var measureGap = (MeasureChanges[i].Time - previousTick) / ((int)(beats * TickRate + 0.5));
                if (measureGap >= remainingMeasures)
                {
                    break;
                }
                else
                {
                    beat += measureGap * beats;
                    remainingMeasures -= measureGap;
                }
                beats = MeasureChanges[i].Beats;
                previousTick = MeasureChanges[i].Time;
            }
            return beat + remainingMeasures * beats;
        }
    }
    public int TickFromMeasureNegative(int measure)
    {
        if (measure < 0)
        {
            int measureTicks;
            if (MeasureChanges.Count == 0 || MeasureChanges[0].Time != 0)
                measureTicks = TickRate * 4;
            else
                measureTicks = TickFromBeat(MeasureChanges[0].Beats);
            return measureTicks * measure;
        }
        else return TickFromMeasure(measure);
    }
    public int TickFromMeasure(int measure)
    {
        if (MeasureChanges.Count == 0)
        {
            return measure * 4 * TickRate;
        }
        else if (MeasureChanges.Count == 1 && MeasureChanges[0].Time == 0)
        {
            var beats = MeasureChanges[0].Beats;
            var iBeats = (int)beats;
            if (beats == iBeats)
            {
                return measure * iBeats * TickRate;
            }
            else
            {
                return TickFromBeat(measure * beats);
            }
        }
        else
        {
            var remainingMeasures = measure;
            var tick = 0;
            var previousTick = 0;
            var ticksPerMeasure = TickFromBeat(MeasureChange.DefaultBeats);
            for (int i = 0; i < MeasureChanges.Count; i++)
            {
                // Assert((MeasureChanges[i].Time - previousTick) % ticksPerMeasure == 0);
                var measureGap = (MeasureChanges[i].Time - previousTick) / ticksPerMeasure;
                if (measureGap >= remainingMeasures)
                {
                    break;
                }
                else
                {
                    tick += measureGap * ticksPerMeasure;
                    remainingMeasures -= measureGap;
                }
                ticksPerMeasure = TickFromBeat(MeasureChanges[i].Beats);
                previousTick = MeasureChanges[i].Time;
            }
            return tick + remainingMeasures * ticksPerMeasure;
        }
    }
    public int? LastHitInRange(int filterStart, int filterEnd)
    {
        int? tick = null;
        var start = HitObjects.BinarySearchFirst(filterStart);
        for (int i = start; i < HitObjects.Count; i++)
        {
            var note = HitObjects[i];
            if (note.Time >= filterEnd) return tick;
            if (note.Time >= filterStart)
            {
                tick = note.Time;
            }
        }
        return tick;
    }

    public string FullAssetPath(string asset) => Source.FullAssetPath(asset);

    public List<HitObjectRealTime> GetRealTimeHitObjects()
    {
        var o = new List<HitObjectRealTime>();
        var tempo = TempoChange.DefaultTempo.MicrosecondsPerQuarterNote;
        var events = TempoChanges.Concat<ITickTime>(HitObjects).OrderBy(e => e.Time);
        var realTime = TotalOffset;
        var lastEvent = 0;
        foreach (var ev in events)
        {
            var delta = ev.Time - lastEvent;
            if (delta > 0)
            {
                realTime += (double)delta / TickRate * tempo / 1000;
            }
            if (ev is TempoChange tc)
            {
                tempo = tc.Tempo.MicrosecondsPerQuarterNote;
            }
            else if (ev is HitObject ho)
            {
                var rt = new HitObjectRealTime(realTime, ho);
                if (ev is RollHitObject roll) rt.Duration = MillisecondsFromTick(ev.Time + roll.Duration) - realTime;
                o.Add(rt);
            }
            lastEvent = ev.Time;
        }
        return o;
    }

    public void RemoveDuplicates()
    {
        CollapseCrashes(); // add accents to crashes
        var o = new List<HitObject>();
        var currentChannels = new HashSet<DrumChannel>();
        var t = 0;
        foreach (var note in HitObjects)
        {
            if (note.Time != t)
            {
                t = note.Time;
                currentChannels.Clear();
            }
            if (!currentChannels.Add(note.Data.Channel))
            {
                Logger.Log($"Removing duplicate: {t} {(double)t / TickRate} {note.Data.Channel}", level: LogLevel.Important);
            }
            else
            {
                o.Add(note);
            }
        }
        HitObjects = o;
        RemoveHiHatCloseWithHit();
    }

    // don't make us hit and close the hi hat at the exact same time
    public void RemoveHiHatCloseWithHit()
    {
        var o = new List<HitObject>();
        int? pendingClose = null;
        var canClose = true;
        var t = 0;
        for (var i = 0; i < HitObjects.Count; i++)
        {
            var note = HitObjects[i];
            if (note.Time != t)
            {
                t = note.Time;
                pendingClose = null;
                canClose = true;
            }
            if (note.Channel == DrumChannel.ClosedHiHat)
            {
                if (pendingClose is int j)
                {
                    Logger.Log($"removing hi-hat close at {(double)note.Time / TickRate}", level: LogLevel.Important);
                    o.RemoveAt(j);
                    pendingClose = null;
                }
                else
                {
                    canClose = false;
                }
            }
            if (note.Channel == DrumChannel.HiHatPedal)
            {
                if (canClose)
                {
                    pendingClose = o.Count;
                    o.Add(note);
                }
                else Logger.Log($"removing hi-hat close at {(double)note.Time / TickRate}", level: LogLevel.Important);
            }
            else o.Add(note);
        }
        HitObjects = o;
    }

    public double FullComputedBeatLength()
    {
        var tickLength = 0;
        if (HitObjects.Count > 0) tickLength = Math.Max(tickLength, HitObjects[^1].Time);
        if (TempoChanges.Count > 0) tickLength = Math.Max(tickLength, TempoChanges[^1].Time);
        return Math.Ceiling(Math.Max(Math.Max(4, QuarterNotes), (double)tickLength / TickRate + 1));
    }

    public void UpdateLength() => QuarterNotes = FullComputedBeatLength();

    public double EndTime()
    {
        if (HitObjects.Count > 0)
        {
            var lastHitObject = HitObjects[^1];
            var time = lastHitObject.Time;
            if (lastHitObject is RollHitObject roll) time += roll.Duration;
            return MillisecondsFromTick(time);
        }
        else return 0;
    }

    public int NextHit(double beat) => HitObjects.BinarySearchThrough(TickFromBeat(beat));
    public void Move(MapStorage mapStorage)
    {
        var oldAudio = FullAudioPath();
        Source = new BJsonSource(Path.Join(mapStorage.AbsolutePath, Path.GetFileName(Source.Filename)));
        Audio = "audio/" + Path.GetFileName(Audio);
        if (Audio != null)
        {
            try
            {
                File.Copy(oldAudio, FullAudioPath());
                Logger.Log($"copied audio to {FullAudioPath()}");
            }
            catch { }
        }
        SaveToDisk(mapStorage);
    }
    public void CollapseCrashes()
    {
        var lastCrash = -1;
        for (var i = 0; i < HitObjects.Count; i++)
        {
            var e = HitObjects[i];
            if (e.Channel == DrumChannel.Crash || e.Channel == DrumChannel.China)
            {
                if (lastCrash != -1 && HitObjects[lastCrash].Time == e.Time)
                {
                    HitObjects[lastCrash] = new HitObject(e.Time, new HitObjectData(DrumChannel.Crash, NoteModifiers.Accented));
                    HitObjects.RemoveAt(i);
                    i -= 1;
                }
                else lastCrash = i;
            }
        }
    }

    public class DoubleBassStickingSettings
    {
        public double Divisor;
        public int Streak;
        public bool RemoveExistingSticking;
        public bool NoHandsOnLeft; // we could change this to all hands if the sticking is left
    }

    public void SetDoubleBassSticking(BeatSelection selection, DoubleBassStickingSettings settings)
    {
        var divisor = settings.Divisor;
        var minStreak = Math.Max(settings.Streak, 2); // can't be less than 2
        var removeExisting = settings.RemoveExistingSticking;
        var checkHands = settings.NoHandsOnLeft;
        int? lastBass = null;
        var threshold = (int)(TickRate / divisor);
        var currentStreak = new List<int>(); // store all recent bass drums

        void EndStreak()
        {
            if (currentStreak.Count == 0) return;
            if (currentStreak.Count >= minStreak)
            {
                var lastStickingWasLeft = HitObjects[currentStreak[0]].Sticking.HasFlag(NoteModifiers.Left);
                for (var i = 1; i < currentStreak.Count; i++)
                {
                    var e = HitObjects[currentStreak[i]];
                    var hasOtherHits = checkHands && GetHitObjectsAtTick(e.Time, currentStreak[i]).Count() > 1;
                    var setLeft = !lastStickingWasLeft && !hasOtherHits;
                    if (setLeft)
                        HitObjects[currentStreak[i]] = e = e.With(e.Modifiers | NoteModifiers.Left);
                    lastStickingWasLeft = e.Sticking.HasFlag(NoteModifiers.Left);
                }
            }
            currentStreak.Clear();
        }

        for (var i = 0; i < HitObjects.Count; i++)
        {
            var e = HitObjects[i];
            if (selection == null || selection.Contains(TickRate, e))
            {
                if (e.Channel == DrumChannel.BassDrum)
                {
                    var time = e.Time;
                    if (removeExisting)
                        HitObjects[i] = e.With(e.Modifiers & ~NoteModifiers.LeftRight);

                    if (lastBass != null && time - lastBass > threshold)
                        EndStreak();
                    currentStreak.Add(i);
                    lastBass = time;
                }
            }
        }
        EndStreak();
    }

    public IEnumerable<(double, bool)> BeatLinesMs()
    {
        // this only takes like 0.5ms

        // Could make this faster by also caching/storing the tempo as we go so we don't have to use ToMilliseconds
        var fullLength = (int)(FullComputedBeatLength() * TickRate);
        var measureChanges = MeasureChanges;
        var ticksPerMeasure = (int)(MeasureChange.DefaultBeats * TickRate);
        var nextI = 0;
        var nextChange = nextI < measureChanges.Count ? measureChanges[nextI].Time : int.MaxValue;
        var tick = 0;
        var measureTick = 0;
        while (tick < fullLength)
        {
            while (measureTick >= ticksPerMeasure) measureTick -= ticksPerMeasure;
            if (tick > nextChange)
            {
                ticksPerMeasure = (int)(measureChanges[nextI].Beats * TickRate);
                nextI += 1;
                nextChange = nextI < measureChanges.Count ? measureChanges[nextI].Time : int.MaxValue;
            }
            yield return (MillisecondsFromTick(tick), measureTick == 0);
            tick += TickRate;
            measureTick += TickRate;
        }
    }

    public void HashId() => Id = MetaHash();
    public string MetaHash() => Util.MD5(Title ?? "", Artist ?? "", DifficultyName ?? Difficulty ?? "", Mapper ?? "", Description ?? "", Tags ?? "");
    public void HashImageUrl()
    {
        var slash = ImageUrl.LastIndexOf("/");
        var dot = ImageUrl.LastIndexOf(".");
        if (slash > dot)
            Image = "images/" + Util.MD5(ImageUrl).ToLower();
        else
            Image = "images/" + Util.MD5(ImageUrl).ToLower() + ImageUrl[dot..];
    }
    public void ComputeStats()
    {
        double start = 0; // in milliseconds
        double end = 0;
        if (HitObjects.Count > 0)
        {
            start = ToMilliseconds(HitObjects[0]);
            end = ToMilliseconds(HitObjects[^1]);
        }
        PlayableDuration = end - start;
        if (TempoChanges.Count == 1 && TempoChanges[0].Time == 0)
            MedianBPM = TempoChanges[0].HumanBPM;
        else
        {
            var tempos = TempoChanges.ToList();
            AddExtraDefault(tempos);
            if (PlayableDuration == 0 || tempos.Count == 1)
                MedianBPM = tempos[0].HumanBPM;
            else
            {
                // sorted slow to fast
                var msTempos = new (double, Tempo)[tempos.Count];
                // we could significantly optimize this ToMilliseconds call
                for (var i = 0; i < tempos.Count; i++)
                    msTempos[i] = (ToMilliseconds(tempos[i]), tempos[i].Tempo);
                // here we convert msTempos to durationMsTempos
                for (var i = 0; i < msTempos.Length; i++)
                {
                    var (ms, tempo) = msTempos[i];
                    var next = msTempos.Length - 1 == i ? end : Math.Min(end, msTempos[i + 1].Item1);
                    var duration = Math.Max(0, next - Math.Max(start, ms));
                    msTempos[i] = (duration, tempo);
                }
                var sorted = msTempos.OrderByDescending(e => e.Item2.MicrosecondsPerQuarterNote).ToList();
                {
                    var medianTarget = PlayableDuration / 2;
                    var s = 0d;
                    var i = 0;
                    while (s < medianTarget && i < sorted.Count)
                    {
                        s += sorted[i].Item1;
                        i += 1;
                    }
                    MedianBPM = sorted[i - 1].Item2.HumanBPM;
                }
            }
        }
        PlayableDuration = Math.Floor(PlayableDuration);
    }

    public string BpmRange
    {
        get
        {
            if (TempoChanges == null)
            {
                TickRate = DefaultTickRate;
                BeatmapLoader.LoadTempo(this);
            }
            if (TempoChanges.Count == 1 && TempoChanges[0].Time == 0 || TempoChanges.Count == 0)
                return null;
            else
            {
                var tempos = TempoChanges.ToList();
                AddExtraDefault(tempos);
                var min = tempos.MaxBy(e => e.Tempo.MicrosecondsPerQuarterNote);
                var max = tempos.MinBy(e => e.Tempo.MicrosecondsPerQuarterNote);
                if (min == max) return null;
                return min.Tempo.HumanBPM + "-" + max.Tempo.HumanBPM;
            }
        }
    }
    public string GetDtxLevel() => GetDtxLevel(SplitTags());
    public static string GetDtxLevel(string[] tags)
    {
        foreach (var tag in tags)
            if (tag.StartsWith("dtx-level-"))
                return tag[10..];
        return null;
    }
    public static string FormatDtxLevel(string level)
    {
        if (string.IsNullOrWhiteSpace(level)) return null;
        var numbers = string.Concat(level.Where(char.IsDigit)).PadRight(3, '0');
        return $"{numbers[0]}.{numbers[1..]}";
    }

    public double GetVolumeMultiplier(BookmarkVolumeEvent[] volumeEvents, HitObject ho)
    {
        var tick = ho.Time;
        var v = 1d;
        if (volumeEvents.Length == 0) return v;
        var beat = (double)tick / TickRate;
        foreach (var volumeEvent in volumeEvents)
        {
            if (volumeEvent.Beat > beat) return v;
            if (volumeEvent.VolumeFor(ho.Data, beat) is double vol)
                v = vol;
        }
        return v;
    }

    public int[] TicksToNoteLengths(List<int> ticks)
    {
        // TODO this does not currently account for measure changes
        // see `NoteGroup.cs` for how to fix this

        var lengths = new int[ticks.Count];
        for (var i = 0; i < ticks.Count; i++)
        {
            var tick = ticks[i];
            var beat = tick / TickRate;
            var set = false;
            for (var j = i + 1; j < ticks.Count; j++)
            {
                var jTick = ticks[j];
                if (jTick != tick)
                {
                    set = true;
                    // if the consecutive notes are within the same beat, the first note is short
                    var jBeat = jTick / TickRate;
                    if (jBeat == beat)
                        lengths[i] = jTick - tick;
                    // if not in same beat, then the first note goes to the end of it's beat
                    else
                        lengths[i] = (beat + 1) * TickRate - tick;
                    break;
                }
            }
            // typically just for the last note
            if (!set)
                lengths[i] = (beat + 1) * TickRate - tick;
        }
        return lengths;
    }
}
