using System;
using System.Buffers;
using System.IO;
using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using ManagedBass;
using osu.Framework.Allocation;
using osu.Framework.Audio.Callbacks;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osuTK;
using SixLabors.ImageSharp.PixelFormats;

namespace DrumGame.Game.Beatmaps.Editor.Timing;

public class FrequencyImageModal : RequestModal
{
    class FreqSprite : Sprite
    {
        public float[] Data;
    }
    readonly BeatmapEditor Editor;
    FFTProvider FFT => Editor.GetFFT();

    readonly FreqSprite Sprite1;
    readonly FreqSprite Sprite2;

    public readonly int TextureWidth;
    public readonly int TextureHeight;

    public double TimeWidth;
    // this won't end up being exact since we have to divide by the FFT oversample
    public static double RequestedTimeWidth = 1000; // milliseconds

    Container SpriteContainer;

    public FrequencyImageModal(BeatmapEditor editor) : base(new RequestConfig
    {
        Title = "Frequency Image"
        // Width = 500
    })
    {
        Editor = editor;

        if (File.Exists(FFTProvider.AutoMapperSettingsPath))
            AddFooterButton(new DrumButton
            {
                AutoSize = true,
                Text = "Open auto-mapper.json",
                Action = () => Util.Host.OpenFileExternally(FFTProvider.AutoMapperSettingsPath)
            });

        var chunkWidthMs = FFT.ChunkWidthS * 1000;
        var chunksPerTexture = (int)(RequestedTimeWidth / chunkWidthMs);
        TimeWidth = chunksPerTexture * chunkWidthMs;
        TextureWidth = chunksPerTexture;
        TextureHeight = FFT.AvailableBins;

        SpriteContainer = new Container
        {
            Masking = true,
            Height = 300,
            RelativeSizeAxes = Axes.X
        };
        Add(new Box
        {
            Width = 5,
            Height = 1.05f,
            Colour = Colour4.Red,
            RelativePositionAxes = Axes.Both,
            RelativeSizeAxes = Axes.Y,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre
        });

        SpriteContainer.Add(Sprite1 = new FreqSprite
        {
            RelativeSizeAxes = Axes.Both,
            RelativePositionAxes = Axes.Both
        });
        SpriteContainer.Add(Sprite2 = new FreqSprite
        {
            RelativeSizeAxes = Axes.Both,
            RelativePositionAxes = Axes.Both
        });
    }

    class FrequencyImageOverlay : CompositeDrawable, IHasMarkupTooltip
    {
        FrequencyImageModal Modal;
        public FrequencyImageOverlay(FrequencyImageModal modal)
        {
            Modal = modal;
            AddInternal(TopBox);
            AddInternal(LeftBox);
            AddInternal(BottomBox);
            AddInternal(RightBox);
            AddInternal(CenterBox);
            CenterBox.Alpha = 0.2f;
        }

        int TextureWidth => Modal.TextureWidth;
        int TextureHeight => Modal.TextureHeight;

        public string MarkupTooltip
        {
            get
            {
                var mouse = Util.Mouse.Position;
                if (!ReceivePositionalInputAt(mouse)) return null;
                var sprite = Modal.Sprite1.ReceivePositionalInputAt(mouse) ? Modal.Sprite1 : Modal.Sprite2;

                (int, int) PosToPixel(Vector2 pos)
                {
                    var x = Math.Clamp((int)(pos.X / sprite.DrawWidth * TextureWidth), 0, TextureWidth - 1);
                    var y = Math.Clamp((int)(pos.Y / sprite.DrawHeight * TextureHeight), 0, TextureHeight - 1);
                    return (x, y);
                }

                var divisions = Modal.FFT.FFTFlagSize;
                int YToHz(int y)
                {
                    var bin = TextureHeight - 1 - y;
                    return Modal.FFT.SampleRate * bin / divisions;
                }


                var pos = sprite.ToLocalSpace(Util.Mouse.Position);
                var (x, y) = PosToPixel(pos);
                var data = sprite.Data[x + y * TextureWidth];
                var bin = TextureHeight - 1 - y;

                var hz1 = YToHz(y);
                if (MouseDown is Vector2 down)
                {
                    // var sum = 0; // TODO compute sum using rectangle
                    var (x2, y2) = PosToPixel(ToSpaceOfOtherDrawable(down, sprite));
                    var hz2 = YToHz(y2);
                    var (lowHz, highHz) = Util.Order(hz1, hz2);
                    var (bin1, bin2) = Util.Order(TextureHeight - 1 - y, TextureHeight - 1 - y2);
                    return $"{lowHz}-{highHz}hz: {data * 100:0.0}\nbin: {bin1}-{bin2}";
                }

                return $"{hz1}hz: {data * 100:0.0}";
            }
        }

        public Box TopBox = new();
        public Box LeftBox = new();
        public Box BottomBox = new();
        public Box RightBox = new();
        public Box CenterBox = new();
        float borderWidth = 1;

        void DrawBox(RectangleF rectangle)
        {
            TopBox.Width = BottomBox.Width = rectangle.Width;
            LeftBox.Height = RightBox.Height = rectangle.Height;

            TopBox.Height = BottomBox.Height = LeftBox.Width = RightBox.Width = borderWidth;

            TopBox.Position = LeftBox.Position = CenterBox.Position = rectangle.TopLeft;

            RightBox.X = rectangle.Right - borderWidth;
            RightBox.Y = rectangle.Top;

            BottomBox.X = rectangle.Left;
            BottomBox.Y = rectangle.Bottom - borderWidth;

            CenterBox.Size = rectangle.Size;
        }

        Vector2? MouseDown;
        protected override bool OnMouseDown(MouseDownEvent e)
        {
            MouseDown = e.MouseDownPosition;
            return true;
        }

        void UpdateBox(MouseEvent e)
        {
            if (MouseDown is Vector2 down)
            {
                var current = e.MousePosition;
                Colour = Colour4.Green;
                DrawBox(new RectangleF
                {
                    X = down.X,
                    Y = down.Y,
                    Width = current.X - down.X,
                    Height = current.Y - down.Y
                }.WithPositiveExtent);
            }
            else
            {
                Colour = Colour4.Transparent;
            }
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            MouseDown = null;
            UpdateBox(e);
            base.OnMouseUp(e);
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            UpdateBox(e);
            return base.OnMouseMove(e);
        }
    }


    [BackgroundDependencyLoader]
    private void load(IRenderer renderer)
    {
        Sprite1.Texture = renderer.CreateTexture(TextureWidth, TextureHeight, true, TextureFilteringMode.Nearest);
        Sprite1.Texture.BypassTextureUploadQueueing = true;
        Sprite1.Data = new float[TextureWidth * TextureHeight];
        Sprite2.Texture = renderer.CreateTexture(TextureWidth, TextureHeight, true, TextureFilteringMode.Nearest);
        Sprite2.Texture.BypassTextureUploadQueueing = true;
        Sprite2.Data = new float[TextureWidth * TextureHeight];
        Add(SpriteContainer);
        Add(new FrequencyImageOverlay(this)
        {
            RelativeSizeAxes = Axes.Both
        });
    }

    static Rgba32 Gradient(float v)
    {
        // gradient => add blue to 127, add red and take away blue to 255, add green and blue to 255

        var i = (int)(v * 637 * 2);
        if (i <= 127) return new Rgba32(0, 0, (byte)i);
        if (i <= 382) return new Rgba32((byte)(i - 127), 0, (byte)(127 - (i - 127) / 2));
        if (i <= 637) return new Rgba32(255, (byte)(i - 382), (byte)(i - 382));
        return new Rgba32(255, 255, 255);
    }

    double loadedTime = double.NegativeInfinity; // ms

    // a slice is a column of pixels in the output texture(s)
    int nextSlice = int.MinValue;

    protected override void Update()
    {
        base.Update();
        var t = Editor.Track.CurrentTime;
        if (t != loadedTime)
        {
            // chunk width in seconds
            var increment = FFT.ChunkWidthS;


            var requestedStartTime = (t - TimeWidth / 2) / 1000;
            var requestedStartSlice = Math.Max((int)(requestedStartTime / increment), 0);
            var requestedEndTime = (t + TimeWidth / 2) / 1000;
            var requestedEndSlice = (int)(requestedEndTime / increment) + 1; // add 1 to make this exclusive

            if (requestedStartSlice <= nextSlice && requestedEndSlice >= nextSlice)
                requestedStartSlice = nextSlice;

            if (requestedEndSlice > requestedStartSlice)
            {
                void UploadSliceRange(int start, int end)
                {
                    var w = end - start;
                    var sprite = start / TextureWidth % 2 == 0 ? Sprite1 : Sprite2;
                    var startOffset = start % TextureWidth;
                    var upload = new ArrayPoolTextureUpload(w, TextureHeight)
                    {
                        Bounds = new RectangleI(startOffset, 0, w, TextureHeight)
                    };
                    var i = 0;
                    for (var x = start; x < end; x++)
                    {
                        var bins = FFT.FFTAtChunk(x);

                        for (var y = 0; y < TextureHeight; y += 1)
                        {
                            var bin = TextureHeight - 1 - y;
                            var v = bins[bin] * (bin / 20f + 0.75f);
                            upload.RawData[y * w + i] = Gradient(v);
                            sprite.Data[y * TextureWidth + i + startOffset] = v;
                        }
                        i += 1;
                    }
                    sprite.Texture.SetData(upload);
                }
                if (requestedStartSlice / TextureWidth == (requestedEndSlice - 1) / TextureWidth)
                {
                    UploadSliceRange(requestedStartSlice, requestedEndSlice);
                }
                else
                {
                    var mid = (requestedStartSlice / TextureWidth + 1) * TextureWidth;
                    UploadSliceRange(requestedStartSlice, mid);
                    UploadSliceRange(mid, requestedEndSlice);
                }
            }

            // for high frequencies, you match the peek by subtracing half of the FFT interval (ie. FFT2048, subtract 1024 samples)
            // for low frequencies, the peek is closer at the start of the interval, meaning you should subtract nothing.
            // in practice, I find subtracting the entire half interval works better, you just have to be aware of the offset when working with bass
            // var pos = (t - 1000d * FFTBins / ChannelInfo.Frequency) / TimeWidth;
            var pos = t / TimeWidth;
            Sprite1.X = (float)(1 - ((pos + 0.5) % 2));
            Sprite2.X = (float)(1 - ((pos + 1.5) % 2));

            nextSlice = requestedEndSlice;
            loadedTime = t;
        }

    }
}