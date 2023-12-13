namespace FFMediaToolkit.Common.Internal
{
    using System.Drawing;
    using FFMediaToolkit.Graphics;
    using FFmpeg.AutoGen;

    /// <summary>
    /// A class used to convert ffmpeg <see cref="AVFrame"/>s to <see cref="ImageData"/> objects with specified image size and color format.
    /// </summary>
    internal unsafe class ImageConverter : Wrapper<SwsContext>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageConverter"/> class.
        /// This constructor should be used only for video encoding!.
        /// </summary>
        public ImageConverter()
            : base(null)
        {
        }

        /// <summary>
        /// Overrides the <paramref name="destinationFrame"/> image buffer with the converted <see cref="ImageData"/> bitmap. Used in encoding.
        /// </summary>
        /// <param name="bitmap">The input bitmap.</param>
        /// <param name="destinationFrame">The <see cref="AVFrame"/> to override.</param>
        internal void FillAVFrame(ImageData bitmap, VideoFrame destinationFrame)
        {
            UpdateContext(bitmap.ImageSize, (AVPixelFormat)bitmap.PixelFormat, destinationFrame.Layout, destinationFrame.PixelFormat);
            fixed (byte* ptr = bitmap.Data)
            {
                var data = new byte*[4] { ptr, null, null, null };
                var linesize = new int[4] { bitmap.Stride, 0, 0, 0 };
                ffmpeg.sws_scale(Pointer, data, linesize, 0, bitmap.ImageSize.Height, destinationFrame.Pointer->data, destinationFrame.Pointer->linesize);
            }
        }

        /// <inheritdoc/>
        protected override void OnDisposing() => ffmpeg.sws_freeContext(Pointer);

        private void UpdateContext(Size sourceSize, AVPixelFormat sourceFormat, Size destinationSize, AVPixelFormat destinationFormat)
        {
            var scaleMode = sourceSize == destinationSize ? ffmpeg.SWS_POINT : ffmpeg.SWS_BICUBIC;
            var newPointer = ffmpeg.sws_getCachedContext(Pointer, sourceSize.Width, sourceSize.Height, sourceFormat, destinationSize.Width, destinationSize.Height, destinationFormat, scaleMode, null, null, null);

            if (newPointer == null)
            {
                throw new FFmpegException("Cannot get a SwsContext to convert the bitmap image. The ffmpeg.sws_getCachedContext method returned \"null\"");
            }

            UpdatePointer(newPointer);
        }
    }
}
