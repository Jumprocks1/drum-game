
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrumGame.Game.API;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Editor.Timing;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Commands.Requests;
using DrumGame.Game.Components;
using DrumGame.Game.Midi;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using osu.Framework.Audio.Track;
using osu.Framework.Logging;

namespace DrumGame.Game.Beatmaps.Editor;

public partial class BeatmapEditor
{
    [CommandHandler] public void EditMode() => Mode = BeatmapPlayerMode.Edit;
    [CommandHandler] public void ToggleEditMode() => Mode = Mode == BeatmapPlayerMode.Edit ? BeatmapPlayerMode.Listening : BeatmapPlayerMode.Edit;
    [CommandHandler] public void ToggleFillMode() => Mode = Mode == BeatmapPlayerMode.Fill ? BeatmapPlayerMode.Edit : BeatmapPlayerMode.Fill;
    [CommandHandler]
    public void RevealInFileExplorer()
    {
        MapStorage.PresentFileExternally(Beatmap.Source.Filename);
    }
    [CommandHandler]
    public void ExportToMidi()
    {
        var outputPath = Resources.GetTemp(Beatmap.Source.FilenameNoExt + ".mid");
        MidiExport.WriteToStream(File.Open(outputPath, FileMode.Create, FileAccess.Write), Beatmap);
        Util.RevealInFileExplorer(outputPath);
    }
    [CommandHandler]
    public bool Rename(CommandContext context)
    {
        context.Palette.Request(new RequestConfig
        {
            Title = "Renaming Beatmap",
            Fields = new FieldBuilder()
                .Add(new StringFieldConfig("Beatmap name", Path.GetFileNameWithoutExtension(Beatmap.Source.Filename)))
                .Add(new BoolFieldConfig("Remove previous file", true))
                .Build(),
            OnCommit = r =>
            {
                var newName = r.GetValue<string>(0);
                var removePrevious = r.GetValue<bool>(1);
                if (removePrevious)
                {
                    var oldPath = Beatmap.Source.Filename;
                    if (Beatmap.TrySetName(newName))
                    {
                        MapStorage.Delete(oldPath);
                        ForceDirty();
                    }
                }
                else
                {
                    var newId = Guid.NewGuid().ToString();
                    var oldId = Beatmap.Id;
                    var oldName = Beatmap.Source.Filename;
                    var oldStoragePath = Beatmap.Source.MapStoragePath;
                    // note that the Filename setter handles the directory/absolute path for the file
                    PushChange(new BeatmapChange(() =>
                    {
                        Beatmap.TrySetName(newName);
                        Beatmap.Id = newId;
                    }, () =>
                    {
                        Beatmap.Source.AbsolutePath = oldName;
                        Beatmap.Source.MapStoragePath = oldStoragePath;
                        Beatmap.Id = oldId;
                    }, $"rename beatmap to {newName}"));
                }
            }
        });
        return true;
    }
    [CommandHandler]
    public bool ABLoop(CommandContext context)
    {
        var s = Display.Selection;
        if (s != null && s.IsComplete)
        {
            Track.LoopStart = Beatmap.MillisecondsFromBeat(s.Left);
            Track.LoopEnd = Beatmap.MillisecondsFromBeat(s.Right);
            context.ShowMessage(Track.LoopSetMessage);
            return true;
        }
        return false;
    }

    private bool SelectionStrideCommand(Func<BeatSelection, int, AffectedRange> action, string description)
    {
        if (!Editing) return false;
        var target = GetSelectionOrCursor();
        var stride = TickStride;
        PushChange(new NoteBeatmapChange(Display, () => action(target, stride), description + " at " + target, target));
        return true;
    }

    [CommandHandler] public bool CycleSticking(CommandContext _) => SelectionStrideCommand(Beatmap.CycleSticking, "cycle note sticking");
    [CommandHandler] public bool CycleModifier(CommandContext _) => SelectionStrideCommand(Beatmap.CycleModifier, "cycle note modifier");
    [CommandHandler]
    public bool ToggleBookmark(CommandContext context)
    {
        var beat = SnapTargetClamp();
        for (var i = 0; i < Beatmap.Bookmarks.Count; i++)
        {
            if (Math.Abs(Beatmap.Bookmarks[i].Time - beat) <= Beatmap.BeatEpsilon)
            {
                var description = $"removing bookmark at {beat} with title {Beatmap.Bookmarks[i].Title}";
                PushChange(new BookmarkBeatmapChange(Beatmap, () => Beatmap.Bookmarks.RemoveAt(i), description));
                return true;
            }
        }
        context.GetString(title =>
            {
                var description = $"added bookmark at {beat} with title {title}";
                PushChange(new BookmarkBeatmapChange(Beatmap, () =>
                {
                    var b = new Bookmark(beat, title);
                    Beatmap.Bookmarks.Insert(Beatmap.Bookmarks.InsertSortedPosition(b), b);
                }, description));
            }, $"Adding Bookmark at Beat {beat}", "Title");
        return true;
    }
    [CommandHandler]
    public bool ToggleAnnotation(CommandContext context)
    {
        var beat = SnapTargetClamp();
        for (var i = 0; i < Beatmap.Annotations.Count; i++)
        {
            if (Math.Abs(Beatmap.Annotations[i].Time - beat) <= Beatmap.BeatEpsilon)
            {
                var description = $"removing annotation at {beat} with text {Beatmap.Annotations[i].Text}";
                PushChange(new AnnotationChange(Beatmap, () => Beatmap.Annotations.RemoveAt(i), description));
                return true;
            }
        }
        context.GetString(title =>
            {
                var description = $"added annotation at {beat} with title {title}";
                PushChange(new AnnotationChange(Beatmap, () =>
                {
                    var b = new Annotation(beat, title);
                    Beatmap.Annotations.Insert(Beatmap.Annotations.InsertSortedPosition(b), b);
                }, description));
            }, $"Adding Annotation at Beat {beat}", "Text");
        return true;
    }
    [CommandHandler]
    public bool SeekToNextSnapPoint(CommandContext _)
    {
        if (!BeatSnap.HasValue) return false;
        Track.Seek(Beatmap.MillisecondsFromBeat(SnapTarget + 1 / BeatSnap.Value));
        fillBuffer = null;
        return true;
    }
    [CommandHandler]
    public bool SeekToPreviousSnapPoint(CommandContext _)
    {
        if (!BeatSnap.HasValue) return false;
        Track.Seek(Beatmap.MillisecondsFromBeat(SnapTarget - 1 / BeatSnap.Value));
        fillBuffer = null;
        return true;
    }
    [CommandHandler]
    public bool DeleteNotes(CommandContext _)
    {
        if (!Editing) return false;
        var s = GetSelectionOrCursor();
        var desc = $"delete notes {s.RangeString}";
        PushChange(new NoteBeatmapChange(Display, () => Beatmap.RemoveHits(s), desc, s));
        return true;
    }
    [CommandHandler] public void SetBeatmapPreviewTime() => PushChange(new PreviewTimeChange(Beatmap, Math.Round(Track.CurrentTime)));
    [CommandHandler] public void TimingWizard() => Command.Palette.Push(new TimingWizard(this));
    OffsetWizard offsetWizard;
    public void ResetOffsetWizard()
    {
        if (offsetWizard == null) return;
        offsetWizard.Close();
        offsetWizard.Dispose();
        offsetWizard = null;
    }
    [CommandHandler] public void OffsetWizard() => Command.Palette.Push(offsetWizard ??= new OffsetWizard(this), true);
    [CommandHandler]
    public void AutoMapperPlot()
    {
        var anal = InternalChildren.FirstOrDefault(e => e is AutoMapperPlot);
        if (anal == null)
            AddInternal(new AutoMapperPlot(this));
        else
            RemoveInternal(anal, true);
    }
    [CommandHandler]
    public bool SetBeatmapOffset(CommandContext context)
    {
        var req = context.Palette.RequestNumber("Setting Beatmap Offset", "Beatmap offset", Beatmap.StartOffset,
                o => PushChange(new OffsetBeatmapChange(this, o, UseYouTubeOffset)));
        req.AddFooterButton(new CommandButton(Commands.Command.OffsetWizard)
        {
            Text = "Offset Wizard",
            Width = 120,
            Height = 30
        });
        return true;
    }
    [CommandHandler] public void SetBeatmapOffsetHere() => PushChange(new OffsetBeatmapChange(this, Math.Round(Track.CurrentTime), UseYouTubeOffset));
    [CommandHandler]
    public bool AddBeatToOffset(CommandContext context) => context.GetNumber(e =>
        PushChange(new OffsetBeatmapChange(this, Beatmap.CurrentTrackStartOffset + Track.CurrentBPM.MillisecondsPerQuarterNote * e,
            UseYouTubeOffset))
        , "Adding beats to offset", "Beat count", current: 1);

    [CommandHandler]
    public void SnapNotes()
    {
        if (!BeatSnap.HasValue) return;
        var range = AffectedRange.FromSelectionOrEverything(Display.Selection, Beatmap);
        var snap = (int)BeatSnap.Value;
        var description = $"snapping {range.ToString(Beatmap.TickRate)} to {snap} divisor";
        PushChange(new NoteBeatmapChange(Display, () =>
        {
            var remove = new List<int>();
            var me = 0;
            var t = 0;
            var seen = new HashSet<DrumChannel>();
            var maxError = Beatmap.TickRate / (snap * 4);
            for (int i = 0; i < Beatmap.HitObjects.Count; i++)
            {
                var h = Beatmap.HitObjects[i];
                if (!range.Contains(h.Time)) continue;
                if (t != h.Time)
                {
                    seen.Clear();
                    t = h.Time;
                }
                var currentBeat = (double)h.Time / Beatmap.TickRate;
                var newTime = Beatmap.TickFromBeat(Beatmap.RoundBeat(currentBeat, snap));
                var error = Math.Abs(h.Time - newTime);
                if (error > me) me = error;
                if (error >= maxError)
                {
                    Logger.Log($"error threshold exceeded at {(double)newTime / Beatmap.TickRate}" +
                        $" old time: {(double)h.Time / Beatmap.TickRate} error: {h.Time - newTime} ticks", level: LogLevel.Important);
                }
                if (!seen.Add(h.Channel))
                {
                    Logger.Log($"snapping caused overlapping notes at {(double)newTime / Beatmap.TickRate}.", level: LogLevel.Important);
                    remove.Add(i);
                }
                else
                {
                    Beatmap.HitObjects[i] = h.WithTime(newTime);
                }
            }
            for (var j = remove.Count - 1; j >= 0; j--) Beatmap.HitObjects.RemoveAt(remove[j]);
            Beatmap.RemoveDuplicates();
            Logger.Log($"Max error during snapping: {me}");
        }, description));
    }

    [CommandHandler]
    public bool SetLeftBassSticking(CommandContext context)
    {
        var range = GetSelectionOrNull();
        var req = new RequestModal(new RequestConfig
        {
            Title = "Automatically Setting Left Bass Sticking" + (range == null ? null : " " + range.RangeString),
            Fields = new IFieldConfig[] {
                new NumberFieldConfig {
                    Label = "Minimum Divisor",
                    DefaultValue = 3
                },
                new BoolFieldConfig {
                    Label = "Remove Existing Sticking",
                    DefaultValue = true
                },
                new NumberFieldConfig {
                    Label = "Minimum Streak",
                    DefaultValue = 2
                },
                new BoolFieldConfig {
                    Label = "No Hands On Left",
                    DefaultValue = false,
                    Tooltip = "Left pedal hits will only be set if there's no hands at the same time"
                },
                new BoolFieldConfig {
                    Label = "Allow Lead With Left",
                    DefaultValue = false,
                    Tooltip = "Starts streaks with left foot when not on the primary beat.\nRecommended to only use this for certain parts of a song that are weird with right lead."
                },
            },
            OnCommit = e =>
            {
                var div = e.GetValue<double?>(0);
                if (!div.HasValue) return;
                var streak = e.GetValue<double?>(2);
                if (!streak.HasValue) return;

                var settings = new Beatmap.DoubleBassStickingSettings
                {
                    Divisor = div.Value,
                    RemoveExistingSticking = e.GetValue<bool>(1),
                    Streak = (int)Math.Floor(streak.Value),
                    LeftLead = e.GetValue<bool>(4),
                    NoHandsOnLeft = e.GetValue<bool>(3)
                };
                var desc = "setting left bass drum sticking";
                if (range != null)
                    desc += $" {range}";
                PushChange(new NoteBeatmapChange(Display, () => Beatmap.SetDoubleBassSticking(range, settings), desc));
            }
        });
        context.Palette.Push(req);
        return true;
    }
    [CommandHandler]
    public void SimplifyNotes()
    {
        var range = GetSelectionOrNull();
        var desc = "simplifying notes";
        if (range != null)
            desc += $" {range.RangeString}";
        NoteBeatmapChange c = null;
        PushChange(c = new NoteBeatmapChange(Display, () =>
        {
            var res = Beatmap.Simplify(range);
            if (res != null)
            {
                c.OverwriteDescription($"{desc}: {res}");
                return true;
            }
            return false;
        }, desc));
    }

    [CommandHandler]
    public bool ConvertRolls(CommandContext context) => context.GetNumber(ConvertRolls,
        "Converting notes at divisor speed to rolls", "Divisor", BeatSnap ?? 8);
    public void ConvertRolls(double divisor)
    {
        var maxGap = (int)(Beatmap.TickRate / divisor + 0.5);
        // should also keep notes that are at the end of rolls on down beat
        PushChange(new NoteBeatmapChange(Display, () =>
            {
                var output = new List<HitObject>();
                var recentHits = new Dictionary<HitObjectData, HitObject>();
                var currentRolls = new Dictionary<HitObjectData, RollHitObject>();
                var rolled = new HashSet<HitObject>();
                void CloseRoll(HitObjectData d, bool extend)
                {
                    if (currentRolls.TryGetValue(d, out var r))
                    {
                        if (extend)
                        {
                            output.Add(r.WithDuration(r.Duration + maxGap));
                        }
                        else
                        {
                            rolled.Remove(recentHits[d]);
                            output.Add(r);
                        }
                        currentRolls.Remove(d);
                    }
                }
                for (int i = 0; i < Beatmap.HitObjects.Count; i++)
                {
                    var h = Beatmap.HitObjects[i];
                    if (recentHits.TryGetValue(h.Data, out var r))
                    {
                        var gap = h.Time - r.Time;
                        if (gap <= maxGap)
                        {
                            rolled.Add(r);
                            rolled.Add(h);
                            if (currentRolls.TryGetValue(h.Data, out var roll))
                            {
                                currentRolls[h.Data] = roll.WithDuration(roll.Duration + gap);
                            }
                            else
                            {
                                currentRolls[h.Data] = new RollHitObject(r.Time, h.Data, gap);
                            }
                        }
                        else
                        {
                            var offset = Beatmap.TickFromMeasure(Beatmap.MeasureFromTick(r.Time));
                            CloseRoll(h.Data, Util.Mod(r.Time - offset, maxGap * 4) != 0);
                        }
                    }
                    recentHits[h.Data] = h;
                }
                foreach (var c in currentRolls.Keys) CloseRoll(c, true);
                foreach (var h in Beatmap.HitObjects) if (!rolled.Contains(h)) output.Add(h);
                Beatmap.HitObjects = output.OrderBy(e => e.Time).ToList();
            }, $"Converting notes at {divisor} divisor to rolls"));
    }

    [CommandHandler]
    public bool StackDrumChannel(CommandContext context)
    {
        context.GetItem<DrumChannel>(d =>
        {
            if (d != DrumChannel.None)
            {
                var data = new HitObjectData(d);
                SelectionStrideCommand((selection, stride) =>
                    Beatmap.ApplyStrideAction(selection, stride, (a, b) => Beatmap.AddHit(a, data, b, true), false), $"force stack {d}");
            }
        }, "Force stacking drum channel");
        return true;
    }
    [CommandHandler]
    public bool CollapseFlams(CommandContext context) => context.GetNumber(CollapseFlams,
        "Converting close notes at divisor speed to accents", "Divisor", BeatSnap ?? 16);
    public void CollapseFlams(double divisor)
    {
        var maxGap = (int)(Beatmap.TickRate / divisor + 0.5);
        // should also keep notes that are at the end of rolls on down beat
        PushChange(new NoteBeatmapChange(Display, () =>
            {
                var output = new List<HitObject>();
                HitObject last = null;
                for (int i = 0; i < Beatmap.HitObjects.Count; i++)
                {
                    var e = Beatmap.HitObjects[i];
                    if (last != null && e.Data == last.Data && e.Time - last.Time <= maxGap)
                        output[^1] = last.With(modifiers: NoteModifiers.Accented);
                    else output.Add(last = e);
                }
                Beatmap.HitObjects = output;
            }, $"convert notes at {divisor} divisor to accents"));
    }

    [CommandHandler] public void DoubleTime() => ChangeTiming(2);

    [CommandHandler]
    public bool MultiplyBPM(CommandContext context) => context.GetNumber(ChangeTiming,
        "Adjusting the BPM of the song without changing the position of notes.", "BPM multiplier", 1);

    public void ChangeTiming(double bpmMulitplier)
    {
        // A beat at `TickRate` would now be located at this tick
        var outputTick = (int)Math.Round(Beatmap.TickRate * bpmMulitplier);
        var gcd = Util.GCD(outputTick, Beatmap.TickRate);
        var numer = outputTick / gcd;
        var denom = Beatmap.TickRate / gcd;
        var description = $"multiplying BPM and note timing by {numer} / {denom}";
        PushChange(new CompositeHistoryChange(description,
            new TempoBeatmapChange(Beatmap, () =>
            {
                Beatmap.AddExtraDefault<TempoChange>();
                for (int i = 0; i < Beatmap.TempoChanges.Count; i++)
                {
                    var t = Beatmap.TempoChanges[i];
                    Beatmap.TempoChanges[i] = new TempoChange(t.Time * numer / denom,
                        new Tempo() { MicrosecondsPerQuarterNote = t.MicrosecondsPerQuarterNote * denom / numer });
                }
                Beatmap.RemoveExtras<TempoChange>();
            }, null),
            new MeasureBeatmapChange(Beatmap, () =>
            {
                for (int i = 0; i < Beatmap.MeasureChanges.Count; i++)
                {
                    var t = Beatmap.MeasureChanges[i];
                    Beatmap.MeasureChanges[i] = new MeasureChange(t.Time * numer / denom, t.Beats * numer / denom);
                }
            }, null),
            new NoteBeatmapChange(Display, () =>
            {
                for (int i = 0; i < Beatmap.HitObjects.Count; i++)
                {
                    var h = Beatmap.HitObjects[i];
                    Beatmap.HitObjects[i] = h.WithTime(h.Time * numer / denom);
                }
            }, null)
            , new BookmarkBeatmapChange(Beatmap, () =>
            {
                for (var i = 0; i < Beatmap.Bookmarks.Count; i++)
                {
                    var e = Beatmap.Bookmarks[i];
                    Beatmap.Bookmarks[i] = e.With(e.Time * numer / denom);
                }
            }, null)
        ));
    }
    BeatmapAutoDrumPlayer _drumPlayer;
    public bool AutoPlayHitSounds
    {
        get => _drumPlayer?.Enabled ?? false; set
        {
            _drumPlayer ??= new BeatmapAutoDrumPlayer(Beatmap, Track);
            _drumPlayer.Enabled = value;
        }
    }
    [CommandHandler] public void ToggleAutoPlayHitSounds() => AutoPlayHitSounds = !AutoPlayHitSounds;
    [CommandHandler] public void EditorTools() => Util.Palette.Palette.ShowCommandList(true, CommandList.EditorTools);
    [CommandHandler] public bool ExportToDtx(CommandContext context) => DtxExporter.Export(context, Beatmap);
    [CommandHandler]
    public bool ExportMap(CommandContext context) => BeatmapExporter.Export(context, Beatmap);
    [CommandHandler]
    public bool ConvertAudioToOgg(CommandContext context)
    {
        var fields = new FieldBuilder()
            .Add(new BoolFieldConfig { Label = "Delete Old Audio" })
            .Add(new IntFieldConfig { Label = "Vorbis Quality", DefaultValue = 8, MarkupTooltip = "8 is ~256kb/s.\b9 is ~320kb/s" })
            .Add(new BoolFieldConfig { Label = "Swap L/R channels" });

        context.Palette.Request(new RequestConfig
        {
            Title = "Converting Audio",
            CommitText = "Convert",
            Fields = fields.Build(),
            OnCommit = e =>
            {
                var removeOld = e.GetValue<bool>(0);
                var relativePath = "audio/" + Path.ChangeExtension(Path.GetFileName(Beatmap.Audio), ".ogg");
                var process = new FFmpegProcess("converting bgm");
                var oldAudio = Beatmap.FullAudioPath();
                process.AddInput(oldAudio);
                process.Vorbis(q: e.GetValue<int?>(1) ?? 8);
                process.SimpleAudio();
                if (e.GetValue<bool>(2)) process.SwapChannels();
                process.AddOutput(Beatmap.FullAssetPath(relativePath));
                process.Run();
                PushChange(new AudioBeatmapChange(Beatmap, relativePath, this));
                if (removeOld) File.Delete(oldAudio);
            }
        });
        return true;
    }

    [CommandHandler]
    public bool FrequencyImage(CommandContext context) => context.Palette.Toggle(() => new FrequencyImageModal(this));

    [CommandHandler]
    public void AutoMapper()
    {
        var range = GetSelectionOrNull();
        var settings = new AutoMapper.AutoMapperSettings
        {
            BeatSnap = BeatSnap ?? 4
        };
        PushChange(new NoteBeatmapChange(Display, () => new AutoMapper(this, settings).Run(range),
            $"auto mapper - snap: {settings.BeatSnap} {range?.RangeString}", range));
    }

    void SetRelativeVolume(double? value)
    {
        Beatmap.RelativeVolume = value;
        Track.Track.Volume.Value = Beatmap.CurrentRelativeVolume;
        // by setting this last, we can avoid triggering editor.Dirty()
        Display.VolumeControls.RelativeSongVolume.Value = Beatmap.CurrentRelativeVolume;
    }

    // this is -4.54 LUFS at 0.3 relative volume
    public const double TargetLufs = -15;
    [CommandHandler]
    public void SetNormalizedRelativeVolume()
    {
        Task.Run(() =>
        {
            var lufs = API.EbUr128.LoudnessNormalization.GetLufs(CurrentAudioPath);
            // schedule will safely be skipped if we die during the LUFS calculation
            Schedule(() =>
            {
                var volume = Math.Round(Math.Pow(10, (TargetLufs - lufs) / 20), 4);
                var oldVolume = Beatmap.RelativeVolume;
                PushChange(() => SetRelativeVolume(volume), () => SetRelativeVolume(oldVolume), $"Set relative volume to {volume} (source: {lufs:0.00} LUFS)");
            });
        });
    }

    Track DrumOnlyAudio;
    [CommandHandler]
    public void ListenToDrumOnlyAudio()
    {
        if (!string.IsNullOrWhiteSpace(Beatmap.DrumOnlyAudio))
            DrumOnlyAudio ??= Resources.GetTrack(Util.Resources.GetAbsolutePath(Beatmap.DrumOnlyAudio));
        if (DrumOnlyAudio == null)
        {
            Util.Palette.RequestFile("Drum Only Audio", "Drum only audio not found, please drop in an audio file to load", e =>
            {
                var oldAudio = Beatmap.DrumOnlyAudio;
                string newDrumAudioPath;
                if (Util.Resources.Contains(e))
                    newDrumAudioPath = Util.Resources.GetRelativePath(e);
                else
                {
                    var name = Path.GetFileNameWithoutExtension(e);
                    var ext = Path.GetExtension(e);
                    var outputPath = Util.Resources.GetTemp($"{name}-drums{ext}");
                    newDrumAudioPath = Util.Resources.GetRelativePath(outputPath);
                    if (!File.Exists(outputPath))
                        File.Copy(e, outputPath);
                }
                PushChange(() => Beatmap.DrumOnlyAudio = newDrumAudioPath,
                    () => Beatmap.DrumOnlyAudio = oldAudio, $"set drum only audio to {newDrumAudioPath}");
                ListenToDrumOnlyAudio();
            });
            return;
        }
        if (Track.PrimaryTrack == null) Track.TemporarySwap(DrumOnlyAudio);
        else Track.ResumePrimary();
        Track.Track.Volume.Value = Beatmap.CurrentRelativeVolume;
    }
}
