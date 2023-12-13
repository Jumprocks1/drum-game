namespace FFMediaToolkit.Common.Internal
{
    using FFmpeg.AutoGen;

    /// <summary>
    /// Represents a FFMpeg media packet.
    /// </summary>
    internal unsafe sealed class MediaPacket : Wrapper<AVPacket>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaPacket"/> class.
        /// </summary>
        /// <param name="packet">The <see cref="AVPacket"/> object.</param>
        private MediaPacket(AVPacket* packet)
            : base(packet)
        {
        }

        /// <summary>
        /// Gets or sets the stream index.
        /// </summary>
        public int StreamIndex
        {
            get => Pointer->stream_index;
            set => Pointer->stream_index = value;
        }

        /// <summary>
        /// Converts an instance of <see cref="MediaPacket"/> to the unmanaged pointer.
        /// </summary>
        /// <param name="packet">A <see cref="MediaPacket"/> instance.</param>
        public static implicit operator AVPacket*(MediaPacket packet) => packet.Pointer;

        /// <summary>
        /// Allocates a new empty packet.
        /// </summary>
        /// <returns>The new <see cref="MediaPacket"/>.</returns>
        public static MediaPacket AllocateEmpty()
        {
            var packet = ffmpeg.av_packet_alloc();
            packet->stream_index = -1;
            return new MediaPacket(packet);
        }

        /// <summary>
        /// Sets valid PTS/DTS values. Used only in encoding.
        /// </summary>
        /// <param name="codecTimeBase">The encoder time base.</param>
        /// <param name="streamTimeBase">The time base of media stream.</param>
        public void RescaleTimestamp(AVRational codecTimeBase, AVRational streamTimeBase) => ffmpeg.av_packet_rescale_ts(Pointer, codecTimeBase, streamTimeBase);

        /// <summary>
        /// Wipes the packet data.
        /// </summary>
        public void Wipe() => ffmpeg.av_packet_unref(Pointer);

        /// <inheritdoc/>
        protected override void OnDisposing()
        {
            var ptr = Pointer;
            ffmpeg.av_packet_free(&ptr);
        }
    }
}
