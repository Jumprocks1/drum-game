namespace FFMediaToolkit.Common.Internal
{
    using System;
    using System.Drawing;
    using FFMediaToolkit.Graphics;
    using FFMediaToolkit.Helpers;
    using FFmpeg.AutoGen;

    /// <summary>
    /// Represent a video frame.
    /// </summary>
    internal unsafe class VideoFrame : MediaFrame
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VideoFrame"/> class with empty frame data.
        /// </summary>
        public VideoFrame()
            : base(ffmpeg.av_frame_alloc())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoFrame"/> class using existing <see cref="AVFrame"/>.
        /// </summary>
        /// <param name="frame">The video <see cref="AVFrame"/>.</param>
        public VideoFrame(AVFrame* frame)
            : base(frame)
        {
        }

        /// <summary>
        /// Gets the frame dimensions.
        /// </summary>
        public Size Layout => Pointer != null ? new Size(Pointer->width, Pointer->height) : default;

        /// <summary>
        /// Gets the frame pixel format.
        /// </summary>
        public AVPixelFormat PixelFormat => Pointer != null ? (AVPixelFormat)Pointer->format : default;

        /// <summary>
        /// Creates a video frame with given dimensions and allocates a buffer for it.
        /// </summary>
        /// <param name="dimensions">The dimensions of the video frame.</param>
        /// <param name="pixelFormat">The video pixel format.</param>
        /// <returns>The new video frame.</returns>
        public static VideoFrame Create(Size dimensions, AVPixelFormat pixelFormat)
        {
            var frame = ffmpeg.av_frame_alloc();

            frame->width = dimensions.Width;
            frame->height = dimensions.Height;
            frame->format = (int)pixelFormat;

            ffmpeg.av_frame_get_buffer(frame, 32);

            return new VideoFrame(frame);
        }

        /// <summary>
        /// Overrides this video frame data with the converted <paramref name="bitmap"/> using specified <see cref="ImageConverter"/> object.
        /// </summary>
        /// <param name="bitmap">The bitmap to convert.</param>
        /// <param name="converter">A <see cref="ImageConverter"/> object, used for caching the FFMpeg <see cref="SwsContext"/> when converting many frames of the same video.</param>
        public void UpdateFromBitmap(ImageData bitmap, ImageConverter converter) => converter.FillAVFrame(bitmap, this);
    }
}
