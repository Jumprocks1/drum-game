using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DrumGame.Game.API;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Beatmaps.Formats;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands.Requests;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Stores;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
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
    public double BeatFromTick(ITickTime e) => (double)e.Time / TickRate;

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

    // returns true if the map was synchronously saved
    public bool TrySaveToDisk(BeatmapEditor editor = null)
    {
        if (!Source.Format.CanSave)
        {
            Util.Palette.Push(SaveRequest.ConvertSaveRequest(Source, removePrevious =>
            {
                var library = Source.Library;
                var enableBjson = !library.IsMain && !library.ScanBjson;

                var oldPath = Source.MapStoragePath;
                if (TrySetName(Source.FilenameNoExt))
                {
                    editor?.ForceDirty();
                    if (removePrevious) Util.MapStorage.Delete(oldPath);
                    else Id = Guid.NewGuid().ToString();
                    TrySaveToDisk(editor);

                    if (enableBjson)
                    {
                        library.ScanBjson = true;
                        Util.MapStorage.MapLibraries.InvokeChanged(library);
                    }
                }
            }));
            return false;
        }
        try
        {
            var o = SaveToDisk(Util.MapStorage);
            editor?.Display?.LogEvent($"Saved to {o}");
            editor?.MarkSaveHistory();
            return true;
        }
        catch (UserException e)
        {
            Util.Palette.UserError(e);
        }
        return false;
    }

    // Make sure to call .Export() before this
    public string SaveToDisk(MapStorage mapStorage, MapImportContext context = null)
    {
        if (DisableSaving)
            throw new UserException("Map saving disabled. Likely caused by application of a modifier.");
        // if (Notes == null) throw new Exception("Attempted to save a beatmap before it was exported. Please report this issue to the developer.");
        var target = Source.AbsolutePath;
        if (!Source.Format.CanSave) throw new UserException("Can only save .bjson files");
        using var stream = mapStorage.GetStream(target, FileAccess.Write, FileMode.Create);
        using var writer = new StreamWriter(stream);
        context ??= MapImportContext.Current;
        if (context != null)
        {
            context.NewMaps.Add(Source.MapStoragePath ?? mapStorage.RelativePath(target));
            Mapper ??= context.Author;
        }
        var s = stream as FileStream;
        Logger.Log($"Saving to {s.Name}", level: LogLevel.Important);
        JsonSerializer.Create(SerializerSettings).Serialize(writer, this);
        mapStorage.ReplaceMetadata(Source.MapStoragePath, this);
        // Logger.Log($"Save complete", level: LogLevel.Important); // expected that the caller will log this
        return s.Name;
    }

    public static JsonSerializerSettings SerializerSettings => new()
    {
        ContractResolver = BeatmapContractResolver.Default,
        Converters = [new Skinning.SkinManager.ColorHexConverter(), new StringEnumConverter(new CamelCaseNamingStrategy())],
        DefaultValueHandling = DefaultValueHandling.Ignore, // some fields have this set to include on a case-by-case
        Formatting = Formatting.Indented,
    };
    public static readonly HashSet<DrumChannel> Cymbols = [
        DrumChannel.ClosedHiHat,
        DrumChannel.OpenHiHat,
        DrumChannel.HalfOpenHiHat,
        DrumChannel.Ride,
        DrumChannel.RideBell,
        DrumChannel.RideCrash,
        DrumChannel.Crash,
        DrumChannel.Splash,
        DrumChannel.China,
        DrumChannel.Rim,
    ];

    public static HashSet<DrumChannel>[] ChannelGroups = [
        Cymbols,
        [DrumChannel.Snare, DrumChannel.SideStick]
    ];

    public static HashSet<DrumChannel> GetGroup(DrumChannel channel)
    {
        foreach (var group in ChannelGroups)
        {
            if (group.Contains(channel)) return group;
        }
        return null;
    }

    public bool AddHitsAt(int tick, IList<HitObjectData> hits)
    {
        if (hits.Count == 0) return false;
        if (hits.Count == 1) return AddHit(tick, hits.First(), false);
        var pos = HitObjects.InsertSortedPosition(tick);
        var groups = hits.Select(e => GetGroup(e.Channel)).ToArray();
        var replace = new List<int>();
        foreach (var j in GetHitObjectsAtTick(tick, pos))
        {
            var ho = HitObjects[j];
            var h = ho.Data.Channel;
            for (var i = 0; i < hits.Count; i++)
            {
                var group = groups[i];
                var hit = hits[i];
                if (group?.Contains(h) ?? h == hit.Channel)
                {
                    replace.Add(j);
                    break;
                }
            }
        }
        if (replace.Count > 0)
        {
            if (replace.Count >= hits.Count)
            {
                replace.Sort(); // needs to be sorted so we can safely remove
                for (var i = 0; i < hits.Count; i++)
                    HitObjects[replace[i]] = new HitObject(tick, hits[i]);
                for (var i = replace.Count - 1; i >= hits.Count; i--)
                    HitObjects.RemoveAt(replace[i]);
            }
            else
            {
                for (var i = 0; i < replace.Count; i++)
                    HitObjects[replace[i]] = new HitObject(tick, hits[i]);
                HitObjects.InsertRange(pos, hits.Skip(replace.Count).Select(e => new HitObject(tick, e)));
            }
        }
        else
        {
            HitObjects.InsertRange(pos, hits.Select(e => new HitObject(tick, e)));
            QuarterNotes = Math.Max(tick / TickRate + 1, QuarterNotes);
        }
        return true;
    }
    // returns false if we found an exact match
    // if toggle is true, this causes that exact match to be removed
    // if toggle is false, this means we did nothing
    public bool AddHit(int tick, HitObjectData data, bool toggle = true, bool stack = false)
    {
        // this has a few basic rules:
        //   1. if data already exists at tick, do nothing (unless toggle is set, then remove) - return false
        //   2. if data matches other objects at this tick based on group (not exact), remove all other objects and add data
        //   3. otherwise, simply add data
        var hitObject = new HitObject(tick, data);
        var pos = HitObjects.InsertSortedPosition(tick);

        var channel = data.Channel;
        var group = stack ? null : GetGroup(channel);
        var replace = new List<int>();
        // somehow this got out of bounds at one point
        // I think pos came in at a maximum possible value
        foreach (var j in GetHitObjectsAtTick(tick, pos))
        {
            var ho = HitObjects[j];
            var h = ho.Data.Channel;
            if (group?.Contains(h) ?? h == channel)
            {
                // if we find a perfect match, we can stop searching this tick
                if (toggle && ho.Data == data)
                {
                    HitObjects.RemoveAt(j);
                    return false;
                }
                // we can't replace this immediately in-case there's a perfect match on this same tick
                // if we replaced without searching for the perfect match, we could get a duplicate
                replace.Add(j);
            }
        }
        if (replace.Count > 0)
        {
            // only 1 thing we wanted to change, and it's already an exact match
            if (replace.Count == 1 && HitObjects[replace[0]].Data == data) return false;

            if (replace.Count > 1) replace.Sort(); // needs to be sorted so we can safely remove
            HitObjects[replace[0]] = HitObjects[replace[0]].With(data);
            for (var i = replace.Count - 1; i >= 1; i--)
                HitObjects.RemoveAt(replace[i]);
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
            for (var i = startIndex; i < HitObjects.Count; i++)
            {
                if (HitObjects[i].Time >= end) break;
                yield return i;
            }
        }
        if (selection == null) return Enumerable.Range(0, HitObjects.Count);
        return selection.HasVolume ? it() : GetHitObjectsAt(selection.Left);
    }
    public IEnumerable<int> GetHitObjectsInTicks(int start, int end)
    {
        IEnumerable<int> it()
        {
            var startIndex = HitObjects.BinarySearchFirst(start);
            for (var i = startIndex; i < HitObjects.Count; i++)
            {
                if (HitObjects[i].Time >= end) break;
                yield return i;
            }
        }
        return it();
    }

    public bool IsEmptyMeasure(int measure)
    {
        var startTick = TickFromMeasure(measure);
        var endTick = TickFromMeasure(measure + 1);
        var startIndex = HitObjects.BinarySearchFirst(startTick);
        return startIndex >= HitObjects.Count || HitObjects[startIndex].Time >= endTick;
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
        if (!selection.HasVolume) return action(TickFromBeat(selection.Start), allowToggle);
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
    // I'm sure this can be optimized slightly
    public int MeasureStartTickFromTick(int tick) => TickFromMeasure(MeasureFromTick(tick));
    public int BeatStartTickFromTick(int tick)
    {
        var measureStart = MeasureStartTickFromTick(tick);
        return measureStart + (tick - measureStart) / TickRate * TickRate;
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

    // the ms times returned by this should not be directly compared to times from MillisecondsFromTick (without an epsilon)
    // this method accumulates FP error for each hitobject, so they will not match
    // it's definitely possible to restructure this to work the same as MillisecondsFromBeat, but it's not worth it right now
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
                rt.OriginalObjectIndex = o.Count;
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
    public void CollapseCrashes(int epsilon = 0)
    {
        var lastCrash = -1;
        for (var i = 0; i < HitObjects.Count; i++)
        {
            var e = HitObjects[i];
            if (e.Channel == DrumChannel.Crash || e.Channel == DrumChannel.China)
            {
                if (lastCrash != -1 && Math.Abs(HitObjects[lastCrash].Time - e.Time) <= epsilon)
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
        public bool LeftLead;
        public bool NoHandsOnLeft; // we could change this to all hands if the sticking is left
        public double MaximumDivisor;
    }

    int SnapFromTick(ITickTime e) => TickRate / Util.GCD(e.Time % TickRate, TickRate);
    public AffectedRange SetDoubleBassSticking(BeatSelection selection, DoubleBassStickingSettings settings)
    {
        var minStreak = Math.Max(settings.Streak, 2); // can't be less than 2
        var removeExisting = settings.RemoveExistingSticking;
        var checkHands = settings.NoHandsOnLeft;
        int? lastBass = null;
        var maximumDistance = (int)(TickRate / settings.Divisor);
        var minimumDistance = (int)(TickRate / settings.MaximumDivisor);
        var currentStreak = new List<int>(); // store all recent bass drums

        void EndStreak()
        {
            if (currentStreak.Count == 0) return;
            if (currentStreak.Count >= minStreak)
            {
                if (settings.LeftLead && currentStreak.Count > 1)
                {
                    var e = HitObjects[currentStreak[0]];
                    // this checks if the current hit is more "awkward" than the next hit based on snap
                    // for example, if there's a bass hit on the sixteenth note before a new measure, that would be 4 snap => level 4 awkward
                    // this is not enabled at all times because there is nuance to "awkward"
                    // for example (b = bass, r = right bass, l = left bass):
                    // 0 1 2 3 0
                    //  b b b bbb
                    //
                    //  r r r lrl => left lead
                    // vs
                    //  r r r rlr => right lead
                    // in this example, the off beat actually feels less awkward, so it gets the right foot
                    if (SnapFromTick(e) > SnapFromTick(HitObjects[currentStreak[1]]))
                        HitObjects[currentStreak[0]] = e.With(e.Modifiers | NoteModifiers.Left);
                }
                // make sure we are not starting with a left foot if left lead is disabled
                if (!settings.LeftLead)
                {
                    var e = HitObjects[currentStreak[0]];
                    HitObjects[currentStreak[0]] = e.With(e.Modifiers & ~NoteModifiers.LeftRight);
                }
                var lastStickingWasLeft = HitObjects[currentStreak[0]].Sticking.HasFlag(NoteModifiers.Left);
                for (var i = 1; i < currentStreak.Count; i++)
                {
                    var e = HitObjects[currentStreak[i]];
                    var hasOtherHits = checkHands && GetHitObjectsAtTick(e.Time, currentStreak[i]).Count() > 1;
                    var setLeft = !lastStickingWasLeft && !hasOtherHits;
                    if (setLeft)
                        HitObjects[currentStreak[i]] = e = e.With(e.Modifiers | NoteModifiers.Left);
                    // absolutely cannot have 2 lefts in a row
                    if (lastStickingWasLeft && e.Modifiers.HasFlag(NoteModifiers.Left))
                        HitObjects[currentStreak[i]] = e = e.With(e.Modifiers & ~NoteModifiers.Left);
                    lastStickingWasLeft = e.Sticking.HasFlag(NoteModifiers.Left);
                }
            }
            currentStreak.Clear();
        }
        var lastCloseHH = int.MinValue / 2;
        for (var i = 0; i < HitObjects.Count; i++)
        {
            var e = HitObjects[i];
            if (selection == null || selection.Contains(TickRate, e))
            {
                if (e.Channel == DrumChannel.ClosedHiHat || e.Channel == DrumChannel.HiHatPedal)
                {
                    lastCloseHH = e.Time;
                }
                else if (e.Channel == DrumChannel.BassDrum)
                {
                    var time = e.Time;
                    if (removeExisting)
                        HitObjects[i] = e.With(e.Modifiers & ~NoteModifiers.LeftRight);

                    if (lastBass != null)
                    {
                        var distance = time - lastBass.Value;
                        if (distance > maximumDistance || distance < minimumDistance)
                            EndStreak();
                    }

                    // we ignore bass hits that are too close to hi-hat hits/pedals
                    if (time - lastCloseHH >= maximumDistance * 2)
                    {
                        currentStreak.Add(i);
                        lastBass = time;
                    }
                }
            }
        }
        EndStreak();
        return AffectedRange.FromSelectionOrEverything(selection, this);
    }

    public string Simplify(BeatSelection selection)
    {
        var remove = new List<int>();
        bool RemovePending()
        {
            if (remove.Count == 0) return false;
            for (var i = remove.Count - 1; i >= 0; i--)
                HitObjects.RemoveAt(remove[i]);
            return true;
        }
        var hos = GetHitObjectsIn(selection).ToList();
        if (hos.Count == 0) return null;
        foreach (var i in hos)
        {
            if (HitObjects[i].Channel == DrumChannel.BassDrum && HitObjects[i].Modifiers.HasFlag(NoteModifiers.Left))
                remove.Add(i);
        }
        if (RemovePending()) return "remove left bass";
        foreach (var i in hos)
        {
            if (HitObjects[i].Modifiers.HasFlag(NoteModifiers.Ghost))
                remove.Add(i);
        }
        if (RemovePending()) return "remove ghost";
        var hoDiff = hos
            .Select(i =>
            {
                var ho = HitObjects[i];
                var offset = (ho.Time - MeasureStartTickFromTick(ho.Time)) % TickRate;
                var gcd = Util.GCD(offset, TickRate);
                var diff = TickRate / gcd;
                return (i, diff);
            })
            .ToList();
        var hardest = hoDiff.Max(e => e.diff);
        if (hardest > 1)
        {
            foreach (var (i, diff) in hoDiff)
            {
                if (diff == hardest)
                    remove.Add(i);
            }
        }
        if (RemovePending()) return $"remove {hardest} snap";
        return null;
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
    public string DifficultyString => DifficultyName ?? Difficulty.ToDifficultyString();
    public string MetaHash() => Util.MD5(Title ?? "", Artist ?? "", DifficultyName ?? DifficultyString ?? "", Mapper ?? "", Description ?? "", Tags ?? "");
    public string MetaHashNoDiff() => Util.MD5(Title ?? "", Artist ?? "", Mapper ?? "", Description ?? "");
    // note, this doesn't work for things like (TV Size) being in the title, oh well
    // in those cases, we will have to manually set the map set
    public string MapSetHash() => Util.MD5(Title ?? "", Artist ?? "", Mapper ?? "");
    public string MapSetIdNonNull => MapSetId ?? MapSetHash();
    public void HashImageUrl()
    {
        var slash = ImageUrl.LastIndexOf('/');
        var dot = ImageUrl.LastIndexOf('.');
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

    // this could be improved, but honestly it's good enough
    public double EstimateMedianBpm()
    {
        if (TempoChanges == null)
        {
            TickRate = DefaultTickRate;
            BJsonLoadHelpers.LoadTempo(this);
        }
        var tempos = TempoChanges.ToList();
        AddExtraDefault(tempos);
        var min = tempos.MaxBy(e => e.Tempo.MicrosecondsPerQuarterNote);
        var max = tempos.MinBy(e => e.Tempo.MicrosecondsPerQuarterNote);
        if (min == max) return min.BPM;
        return Math.Round((min.Tempo.BPM + max.Tempo.BPM) / 2, 1);
    }

    public string BpmRange
    {
        get
        {
            if (TempoChanges == null)
            {
                TickRate = DefaultTickRate;
                BJsonLoadHelpers.LoadTempo(this);
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
        if (ho.Preset != null && string.IsNullOrWhiteSpace(ho.Preset.Sample))
            v *= ho.Preset.Volume;
        if (volumeEvents.Length == 0) return v;
        var beat = (double)tick / TickRate;
        var bookmarkModifier = 1d;
        foreach (var volumeEvent in volumeEvents)
        {
            if (volumeEvent.Beat > beat) break;
            if (volumeEvent.VolumeFor(ho.Data, beat) is double vol)
                bookmarkModifier = vol;
        }
        return v * bookmarkModifier;
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

    public bool MissingLeftBassSticking()
    {
        var bassHits = HitObjects.Where(e => e.Channel == DrumChannel.BassDrum);
        var last = int.MinValue;
        var smallestGap = int.MaxValue;
        foreach (var ho in bassHits)
        {
            if (ho.Sticking == NoteModifiers.Left) return false; // if there's a single left bass, then it's not missing
            if (last != int.MinValue)
            {
                var diff = ho.Time - last;
                if (diff < smallestGap) smallestGap = diff;
            }
            last = ho.Time;
        }
        // it's fine if there's no left bass if all the notes are slowish
        return smallestGap <= TickRate / 4;
    }

    public void StripHtmlFromMetadata() // primarily for weird CloneHero charts
    {
        var regex = new Regex("<.*?>");
        if (Title != null) Title = regex.Replace(Title, "");
        if (Artist != null) Artist = regex.Replace(Artist, "");
        if (Mapper != null) Mapper = regex.Replace(Mapper, "");
    }
}
