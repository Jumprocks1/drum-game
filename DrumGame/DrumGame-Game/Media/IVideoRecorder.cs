using System;

namespace DrumGame.Game.Media;

public interface IVideoRecorder : IDisposable
{
    public void Start(Func<bool> requestNextFrame);
    public int FrameRate { get; }
}