using System.Threading.Tasks;
using osu.Framework.Audio.Track;

namespace DrumGame.Game.Timing;

public class TrackManual : Track
{
    public TrackManual(double length) : base("manual")
    {
        Length = length;
    }

    public override bool Seek(double seek)
    {
        if (seek > Length)
        {
            _currentTime = Length;
            Stop();
            RaiseCompleted();
        }
        else _currentTime = seek;
        return true;
    }

    public override bool IsRunning => _isRunning;
    bool _isRunning;

    public override double CurrentTime { get => _currentTime; }
    double _currentTime;
    public override void Start() => _isRunning = true;

    public override void Stop() => _isRunning = false;

    public override Task<bool> SeekAsync(double seek) => Task.FromResult(Seek(seek));
    public override Task StartAsync() => Task.CompletedTask;
    public override Task StopAsync() => Task.CompletedTask;
}