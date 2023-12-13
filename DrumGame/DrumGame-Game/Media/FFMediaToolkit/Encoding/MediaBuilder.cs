﻿namespace FFMediaToolkit.Encoding
{
    using System;
    using System.IO;
    using FFMediaToolkit.Common;
    using FFMediaToolkit.Encoding.Internal;
    using FFMediaToolkit.Helpers;

    /// <summary>
    /// Represents a multimedia file creator.
    /// </summary>
    public class MediaBuilder
    {
        private readonly OutputContainer container;
        private readonly string outputPath;

        private MediaBuilder(string path)
        {
            if (!Path.IsPathRooted(path))
                throw new ArgumentException($"The path \"{path}\" is not valid.");

            container = OutputContainer.Create(Path.GetExtension(path));
            outputPath = path;
        }

        /// <summary>
        /// Sets up a multimedia container with the format guessed from the file extension.
        /// </summary>
        /// <param name="path">A path to create the output file.</param>
        /// <returns>The <see cref="MediaBuilder"/> instance.</returns>
        public static MediaBuilder CreateContainer(string path) => new MediaBuilder(path);

        /// <summary>
        /// Adds a new video stream to the file.
        /// </summary>
        /// <param name="settings">The video stream settings.</param>
        /// <returns>This <see cref="MediaBuilder"/> object.</returns>
        public MediaBuilder WithVideo(VideoEncoderSettings settings)
        {
            if (FFmpegLoader.IsFFmpegGplLicensed == false && (settings.Codec == VideoCodec.H264 || settings.Codec == VideoCodec.H265))
            {
                throw new NotSupportedException("The LGPL-licensed FFmpeg build does not contain libx264 and libx265 codecs.");
            }

            container.AddVideoStream(settings);
            return this;
        }

        /// <summary>
        /// Creates a multimedia file for specified video stream.
        /// </summary>
        /// <returns>A new <see cref="MediaOutput"/>.</returns>
        public MediaOutput Create()
        {
            container.CreateFile(outputPath);

            return new MediaOutput(container);
        }
    }
}
