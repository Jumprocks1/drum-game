using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace DrumGame.Game.API;

public class RemoteVideoWebSocket : DrumWebSocket
{
    public static RemoteVideoWebSocket Current;
    public static RemoteVideoWebSocket EnsureStarted() => Current ??= new RemoteVideoWebSocket();
    public static double? GetLength(string id)
    {
        lock (_lock)
            return LengthCache.GetValueOrDefault(id);
    }
    static Dictionary<string, double> LengthCache = new();
    public event Action<(string video, PlayerState)> OnStateChange;
    public enum PlayerState : sbyte
    {
        UNSTARTED = -1,
        ENDED = 0,
        PLAYING = 1,
        PAUSED = 2,
        BUFFERING = 3,
        CUED = 5
    }

    event Action<TcpClient> OnClientConnected;
    public List<(string, Action)> PendingVideoLoads = new();
    public string ClientVideo;
    public Action<string, double, long> OnTimeUpdated;

    public void VideoCallback(string id, Action callback)
    {
        var call = false;
        lock (_lock)
        {
            TargetVideoId = id;
            if (ClientVideo == id) call = true;
            else PendingVideoLoads.Add((id, callback)); // TODO technically this can leak if it doesn't get cleared ever
        }
        if (call) callback();
    }
    protected override void AfterConnection(TcpClient client)
    {
        SendVolume();
        if (TargetVideoId != null)
            Navigate();
        OnClientConnected?.Invoke(client);
        OnClientConnected = null;
        base.AfterConnection(client);
    }

    string _targetVideoId;
    public string TargetVideoId
    {
        get => _targetVideoId; set
        {
            if (value == _targetVideoId) return;
            _targetVideoId = value;
            if (Connected) Navigate();
        }
    }

    void Navigate() => SendJson(new
    {
        command = "navigate",
        videoId = _targetVideoId
    });
    public void Seek(string id, double seek, bool hard) => SendJson(new { command = "seek", videoId = id, position = seek / 1000, hard });
    public void StartVideo(string id, double? position = null) => SendJson(new
    {
        command = "start",
        videoId = id,
        position = position
    });
    public void StopVideo(string id) => SendJson(new { command = "stop", videoId = id });

    double _targetVolume = 0;
    public double TargetVolume
    {
        get => _targetVolume; set
        {
            if (_targetVolume == value) return;
            _targetVolume = value;
            SendVolume();
        }
    }
    void SendVolume() => SendJson(new { command = "volume", volume = _targetVolume * 100 });

    protected override void HandleMessage(byte[] message)
    {
        var messageType = message.Length == 0 ? 0 : message[0];
        if (messageType == 1) // video id/state update
        {
            if (message.Length != 21) throw new NotSupportedException();
            var videoId = Encoding.UTF8.GetString(message, 1, 11);
            var length = BitConverter.ToDouble(message, 12) * 1000;
            var state = (PlayerState)message[20];
            lock (_lock)
            {
                LengthCache[videoId] = length;
                ClientVideo = videoId;
                OnStateChange?.Invoke((videoId, state));
                for (var i = PendingVideoLoads.Count - 1; i >= 0; i--)
                {
                    if (PendingVideoLoads[i].Item1 == videoId)
                    {
                        PendingVideoLoads[i].Item2();
                        PendingVideoLoads.RemoveAt(i);
                    }
                }
            }
            // Console.WriteLine($"video: {videoId} duration: {length} {state}");
        }
        else if (messageType == 2) // time update
        {
            var ticks = Stopwatch.GetTimestamp();
            var time = BitConverter.ToDouble(message, 1) * 1000; // time in ms
            OnTimeUpdated?.Invoke(ClientVideo, time, ticks);
        }
        else base.HandleMessage(message);
    }
}