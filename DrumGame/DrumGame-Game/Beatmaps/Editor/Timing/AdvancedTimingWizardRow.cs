using System;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Media;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using SixLabors.ImageSharp.PixelFormats;

namespace DrumGame.Game.Beatmaps.Editor.Timing;

public class AdvancedTimingWizardRow : CompositeDrawable, IHasMarkupTooltip
{
    public const double SecondsPerRow = 5; // TODO remove, not actually used anymore other than in the broken tooltip
    public int RowIndex { get; }
    public bool TextureValid; // careful to only set/read on update thread


    Sprite Sprite;
    // https://manual.audacityteam.org/man/audacity_waveform.html
    public AdvancedTimingWizardRow(AdvancedTimingWizard wizard, int rowIndex)
    {
        RowIndex = rowIndex;
        Wizard = wizard;
        AddInternal(Sprite = new Sprite
        {
            RelativeSizeAxes = Axes.Both
        });
    }
    AdvancedTimingWizard Wizard;
    Texture Texture { get => Sprite.Texture; set => Sprite.Texture = value; }
    AudioDump Audio => Wizard.SampleData;
    Beatmap Beatmap => Wizard.Editor.Beatmap;

    public string MarkupTooltip { get; set; }

    protected override bool OnMouseMove(MouseMoveEvent e)
    {
        UpdateTooltip();
        return base.OnMouseMove(e);
    }

    // 0-1 inside this row, will be more complicated after we do beat-based width/scaling
    public double TimeAt(float x) => (RowIndex + x) * SecondsPerRow;

    public void UpdateTooltip()
    {
        var mousePosition = Util.Mouse.Position;
        var p = Parent.ToLocalSpace(mousePosition);
        var t = TimeAt((float)(p.X / DrawWidth));
        // would be great if we could align the values here so they weren't spaced awkwardly
        MarkupTooltip = $"<faded>Time:</> {t:0.00}s\n" +
                        $"<faded>Beat:</> {Beatmap.BeatFromMilliseconds(t * 1000):0.00}";
    }

    public bool UpdateTexture(Vector2I targetTextureSize, double startBeat, double endBeat)
    {
        if (Texture != null && (Texture.Width != targetTextureSize.X || Texture.Height != targetTextureSize.Y))
        {
            Texture.Dispose();
            Texture = null;
        }
        if (Texture == null || !TextureValid)
        {
            LoadTexture(targetTextureSize, startBeat, endBeat);
            return true;
        }
        return false;
    }

    IRenderer Renderer;
    [BackgroundDependencyLoader]
    private void load(IRenderer renderer)
    {
        Renderer = renderer;
    }

    static readonly Rgba32 Filled = new(255, 120, 120);
    static readonly Rgba32 RmsFilled = new(255, 255, 255);
    static readonly Rgba32 Unfilled = new();


    public void LoadTexture(Vector2I targetTextureSize, double startBeat, double endBeat)
    {
        TextureValid = true;

        var textureWidth = targetTextureSize.X;
        var textureHeight = targetTextureSize.Y;

        // TODO this creates a linear view, which is not what we want if there's changing BPM
        var start = (int)(Audio.SampleRate * Beatmap.MillisecondsFromBeat(startBeat) / 1000);
        var end = (int)(Audio.SampleRate * Beatmap.MillisecondsFromBeat(endBeat) / 1000);

        var buffer = Audio.SampleBuffer;

        if (Texture == null)
        {
            Texture = Renderer.CreateTexture(textureWidth, textureHeight, true, TextureFilteringMode.Nearest, initialisationColour: Colour4.Transparent);
            Texture.BypassTextureUploadQueueing = true;
        }

        var upload = new ArrayPoolTextureUpload(textureWidth, textureHeight);

        var length = end - start;
        var blockStart = start;
        for (var i = 0; i < textureWidth; i++)
        {
            var blockEnd = start + (int)((long)(i + 1) * length / textureWidth);

            float max, min;
            double rms = 0;
            // Audacity seems to do RMS, but limits the output value to between min/max
            // this is relevant since if there's DC offset or asymetrical samples, the RMS can be larger than the min/max
            // RMS seems to be 100% mirrored over the x axis

            if (blockEnd < Audio.SampleCount && blockStart >= 0)
            {
                min = max = buffer[0, blockStart];
                rms = min * min;
                for (var j = blockStart + 1; j < blockEnd; j++)
                {
                    var v = buffer[0, j];
                    if (v > max) max = v;
                    if (v < min) min = v;
                    rms += v * v;
                }
            }
            else
            {
                max = min = 0;
            }


            rms = Math.Sqrt(rms / (blockEnd - blockStart));
            var a = (int)((1 - max) / 2 * textureHeight);
            var b = (int)((1 - min) / 2 * textureHeight);
            var rmsA = (int)((1 - rms) / 2 * textureHeight);
            var rmsB = (int)((1 + rms) / 2 * textureHeight);
            for (var y = 0; y < textureHeight; y++)
            {
                if (y >= a && y <= b)
                {
                    if (y >= rmsA && y <= rmsB)
                        upload.RawData[y * textureWidth + i] = RmsFilled;
                    else
                        upload.RawData[y * textureWidth + i] = Filled;
                }
                else
                    upload.RawData[y * textureWidth + i] = Unfilled;
            }

            blockStart = blockEnd;
        }
        Texture.SetData(upload);
    }
}