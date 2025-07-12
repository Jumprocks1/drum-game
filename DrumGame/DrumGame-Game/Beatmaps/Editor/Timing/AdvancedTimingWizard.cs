using System;
using System.Collections.Generic;
using System.IO;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Media;
using DrumGame.Game.Modals;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using ManagedBass;
using NAudio.Wave;
using osu.Framework.Allocation;
using osu.Framework.Audio.Callbacks;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using SixLabors.ImageSharp.PixelFormats;

namespace DrumGame.Game.Beatmaps.Editor.Timing;

public class AdvancedTimingWizard : CompositeDrawable, IModal
{
    const float RowHeight = 50f;
    int MeasuresPerRow = 4;
    double BeatIndent = 0.5; // ensures the start of the row is something like -0.5, 15.5 etc.



    public BeatmapEditor Editor;
    public TrackClock Track => Editor.Track;
    public Beatmap Beatmap => Editor.Beatmap;

    ClickBlockingContainer Container;
    DrumScrollContainer Scroll;

    public AudioDump SampleData;

    List<AdvancedTimingWizardRow> Rows = new();

    public Action CloseAction { get; set; }

    public AdvancedTimingWizard(BeatmapEditor editor)
    {
        Editor = editor;
        RelativeSizeAxes = Axes.Both;
        AddInternal(Container = new ClickBlockingContainer { RelativeSizeAxes = Axes.Both });
        Container.Add(new Box
        {
            Colour = DrumColors.DarkBackground,
            RelativeSizeAxes = Axes.Both
        });
        Container.Add(Scroll = new DrumScrollContainer
        {
            RelativeSizeAxes = Axes.Both
        });
    }

    void CheckTextures()
    {
        var textureRectangle = ToScreenSpace(new RectangleF(0, 0, DrawWidth, RowHeight));
        // we round down so that sometimes pixels are duplicated instead of being skipped
        var targetTextureSize = new Vector2I((int)textureRectangle.Width, (int)textureRectangle.Height);

        // this includes measures before the offset
        // for example, at 120 bpm, if StartOffset = 10_000, there are 5 measures before the notation starts
        var firstMeasureStartOfTrack = Beatmap.MeasureFromTickNegative(Beatmap.TickFromBeatSlow(Beatmap.BeatFromMilliseconds(0)));
        var startMeasure = Math.Min(0, firstMeasureStartOfTrack);
        var endMeasure = Beatmap.MeasureFromBeat(Beatmap.BeatFromMilliseconds(SampleData.Duration * 1000));

        var i = 0;
        for (var measure = startMeasure; measure < endMeasure; measure += MeasuresPerRow)
        {
            if (i >= Rows.Count)
            {
                var newRow = new AdvancedTimingWizardRow(this, i)
                {
                    Y = Rows.Count * RowHeight,
                    Height = RowHeight,
                    RelativeSizeAxes = Axes.X
                };
                Scroll.Add(newRow);
                Rows.Add(newRow);
            }
            var row = Rows[i];
            var startBeat = Beatmap.BeatFromMeasure(measure) - BeatIndent;
            var endBeat = Beatmap.BeatFromMeasure(measure + MeasuresPerRow) - BeatIndent;
            if (row.UpdateTexture(targetTextureSize, startBeat, endBeat))
            {
                return;
            }
            i += 1;
        }
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        using var audioStream = File.OpenRead(Editor.CurrentAudioPath);
        var fileCallbacks = new FileCallbacks(new DataStreamFileProcedures(audioStream));
        var decodeStream = Bass.CreateStream(StreamSystem.NoBuffer, BassFlags.Decode | BassFlags.Float, fileCallbacks.Callbacks, fileCallbacks.Handle);
        Bass.ChannelGetInfo(decodeStream, out var info);
        // TODO share this with the offset wizard
        // TODO allow only pulling in a little bit per frame
        SampleData = new AudioDump(decodeStream, info);
        Bass.StreamFree(decodeStream);
    }
    protected override void Update()
    {
        CheckTextures();
        base.Update();
    }
}