using osu.Framework.Audio.Track;

namespace DrumGame.Game.Media;

public class TrackWrapper
{
    public double CurrentTime { get; protected set; }
    public TrackWrapper(Track track)
    {
        InnerTrack = track;
    }
    public Track InnerTrack { get; }
    public virtual double AbsoluteTime => InnerTrack.CurrentTime;
    public void Dispose()
    {
        InnerTrack?.Dispose();
    }


    public virtual bool IsRunning => InnerTrack.IsRunning;
    public double Length => InnerTrack.Length;
    public double EffectiveRate => IsRunning ? Rate : 0;
    public double Rate => InnerTrack.Rate;
    public virtual void Start() => InnerTrack.Start();
    public virtual void Stop() => InnerTrack.Stop();
    public virtual void Seek(double t) => InnerTrack.Seek(t);
    public virtual void Update()
    {
        CurrentTime = AbsoluteTime;
    }
}
