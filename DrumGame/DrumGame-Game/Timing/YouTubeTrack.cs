using System;
using System.Threading.Tasks;
using DrumGame.Game.API;
using osu.Framework.Audio.Track;

using PlayerState = DrumGame.Game.API.RemoteVideoWebSocket.PlayerState;

namespace DrumGame.Game.Timing;

public class YouTubeTrack : Track
{
    public event Action RunningChanged;
    string Id;
    RemoteVideoWebSocket WebSocket;
    PlayerState ClientState = PlayerState.BUFFERING;
    public YouTubeTrack(string id) : base(id)
    {
        Id = id;
        WebSocket = RemoteVideoWebSocket.Current;
        Length = RemoteVideoWebSocket.GetLength(id) ?? 5000;
        WebSocket.OnTimeUpdated += OnTimeUpdated;
        WebSocket.OnStateChange += OnStateChange;
        AggregateVolume.BindValueChanged(e => WebSocket.TargetVolume = e.NewValue);
    }

    void OnStateChange((string id, PlayerState state) change)
    {
        if (change.id != Id) return;
        var oldRunning = IsRunning;
        ClientState = change.state;
        localRunning = null;
        if (oldRunning != IsRunning)
            RunningChanged?.Invoke();
    }

    void OnTimeUpdated(string id, double time, long _)
    {
        if (Id != id) return;
        if (!pendingAsyncSeek || asyncSeekSent)
            localTime = null;
        if (asyncSeekSent)
            asyncSeekSent = pendingAsyncSeek = false;
        remoteTime = time;
    }

    protected override void Dispose(bool disposing)
    {
        WebSocket.OnTimeUpdated -= OnTimeUpdated;
        WebSocket.StopVideo(Id);
        base.Dispose(disposing);
    }

    public override bool Seek(double seek)
    {
        localTime = seek;
        if (seek > Length)
        {
            Stop();
            RaiseCompleted();
        }
        else
        {
            WebSocket.Seek(Id, seek, true);
        }
        return true;
    }

    bool? localRunning;

    public override bool IsRunning => localRunning ?? ClientState == PlayerState.PLAYING;
    public override double CurrentTime { get => localTime ?? remoteTime; }
    double remoteTime;
    double? localTime;
    bool pendingAsyncSeek;
    bool asyncSeekSent;

    public override void Start()
    {
        localRunning = true;
        if (ClientState == PlayerState.PLAYING) return;
        WebSocket.StartVideo(Id);
    }

    public override void Stop()
    {
        localRunning = false;
        if (ClientState == PlayerState.UNSTARTED || ClientState == PlayerState.ENDED || ClientState == PlayerState.PAUSED) return;
        WebSocket.StopVideo(Id);
    }

    public void CommitAsyncSeek()
    {
        if (!pendingAsyncSeek || asyncSeekSent || !localTime.HasValue) return;
        WebSocket.Seek(Id, localTime.Value, true);
        asyncSeekSent = true; // will end async seek on next update
    }
    public override Task<bool> SeekAsync(double seek)
    {
        asyncSeekSent = false;
        pendingAsyncSeek = true;
        localTime = seek;
        // WebSocket.Seek(Id, seek, false); // these aren't even worth sending to YouTube.
        return Task.FromResult(true);
    }
    public override Task StartAsync() => throw new NotImplementedException();
    public override Task StopAsync() => throw new NotImplementedException();
}