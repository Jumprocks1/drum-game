using System;
using System.Threading.Tasks;
using DiscordRPC;
using DrumGame.Game.API;
using DrumGame.Game.Commands;
using DrumGame.Game.Utils;
using osu.Framework.Logging;

namespace DrumGame.Desktop;

internal class DiscordRichPresence : IDisposable
{
    private const string applicationID = "898358401761960036";
    DiscordRpcClient client;
    RichPresence presence;
    bool ready = false;
    bool started = false;
    bool disposed = false;

    public void Dispose()
    {
        lock (this)
        {
            disposed = true;
            client?.Dispose();
            UserActivity.ActivityChanged -= activtyChanged;
        }
    }

    public void SetConnected(bool connect)
    {
        lock (this)
        {
            if (disposed) return;
            if (connect) startDiscordApi();
            else disconnect();
        }
    }

    void startDiscordApi()
    {
        if (disposed || started || !Program.Discord) return; // basic check, doesn't require the lock since it's not critical
        Task.Factory.StartNew(() =>
        {
            lock (this)
            {
                // this is only 3ms, we probably don't really need the background thread
                if (disposed || started) return;
                started = true;
                client = new DiscordRpcClient(applicationID);
                client.OnConnectionFailed += (_, __) =>
                {
                    lock (this)
                    {
                        client?.Dispose();
                        client = null;
                        ready = false;
                        started = false;
                    }
                };
                client.OnReady += (_, __) =>
                {
                    lock (this)
                    {
                        ready = true;
                        Logger.Log("Discord RPC Client ready.", LoggingTarget.Network, LogLevel.Debug);
                        updatePresence();
                        UserActivity.ActivityChanged += activtyChanged;
                    }
                };
                client.OnError += (_, e) => Logger.Log($"An error occurred with Discord RPC Client: {e.Code} {e.Message}", LoggingTarget.Network, LogLevel.Error);
                client.Initialize();
                presence = new RichPresence();
            }
        });
    }

    void updatePresence()
    {
        lock (this)
        {
            if (client == null || !client.IsInitialized || !ready) return;
            var activity = UserActivity.Activity;
            if (activity == null) return;

            presence.State = activity.State;
            presence.Details = activity.Details;
            presence.Assets ??= new()
            {
                LargeImageKey = "logo-thick-1024",
                LargeImageText = "Drum Game"
            };
            presence.Timestamps = activity.Start is DateTime s ? new Timestamps(s) { End = activity.End } : null;
            // this checks for duplicate presence according to SkipIdenticalPresence
            client.SetPresence(presence);
        }
    }

    void disconnect()
    {
        lock (this)
        {
            if (!ready) return;
            if (client != null)
            {
                client.ClearPresence();
                client.Dispose();
                client = null;
            }
            ready = false;
            started = false;
            UserActivity.ActivityChanged -= activtyChanged;
        }
    }

    void activtyChanged(UserActivity.ActivityChangedEvent _) => updatePresence();
}
