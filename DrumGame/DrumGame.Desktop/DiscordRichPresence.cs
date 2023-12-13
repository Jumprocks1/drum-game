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
    object clientLock = new();
    DiscordRpcClient client;
    RichPresence presence;
    bool ready = false;
    bool started = false;
    bool disposed = false;
    public DiscordRichPresence()
    {
        Util.CommandController.RegisterHandler(Command.ConnectToDiscord, startDiscordApi);
    }

    public void Dispose()
    {
        disposed = true;
        Util.CommandController.RemoveHandler(Command.ConnectToDiscord, startDiscordApi);
        Util.CommandController.RemoveHandler(Command.DisconnectDiscord, disconnect);
        client?.Dispose();
        UserActivity.ActivityChanged -= activtyChanged;
    }

    void startDiscordApi()
    {
        if (started) return;
        started = true;
        Util.CommandController.RemoveHandler(Command.ConnectToDiscord, startDiscordApi);
        Task.Factory.StartNew(() =>
        {
            client = new DiscordRpcClient(applicationID);
            client.OnConnectionFailed += (_, __) =>
            {
                client?.Dispose();
                client = null;
                ready = false;
                started = false;
                if (!disposed)
                {
                    Util.CommandController.RegisterHandler(Command.ConnectToDiscord, startDiscordApi);
                }
            };
            client.OnReady += (_, __) =>
            {
                ready = true;
                Logger.Log("Discord RPC Client ready.", LoggingTarget.Network, LogLevel.Debug);
                Util.CommandController.RegisterHandler(Command.DisconnectDiscord, disconnect);
                updatePresence();
            };
            client.OnError += (_, e) => Logger.Log($"An error occurred with Discord RPC Client: {e.Code} {e.Message}", LoggingTarget.Network);
            client.Initialize();
            presence = new RichPresence();
            UserActivity.ActivityChanged += activtyChanged;
        });
    }

    void updatePresence()
    {
        if (client == null || !client.IsInitialized || !ready) return;
        var activity = UserActivity.Activity;
        if (activity == null) return;

        presence.State = activity.State;
        presence.Details = activity.Details;
        presence.Timestamps = activity.Start is DateTime s ? new Timestamps(s, activity.End) : null;
        // this checks for duplicate presence according to SkipIdenticalPresence
        client.SetPresence(presence);
    }

    void disconnect()
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
        Util.CommandController.RegisterHandler(Command.ConnectToDiscord, startDiscordApi);
        Util.CommandController.RemoveHandler(Command.DisconnectDiscord, disconnect);
    }

    void activtyChanged(UserActivity.ActivityChangedEvent _) => updatePresence();
}
