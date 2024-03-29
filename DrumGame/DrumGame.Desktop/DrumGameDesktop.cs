using System.Reflection;
using DrumGame.Game;
using DrumGame.Game.Utils;
using osu.Framework.Platform;

namespace DrumGame.Desktop;

internal class DrumGameDesktop : DrumGameGame
{
    DiscordRichPresence richPresence;
    protected override void LoadComplete()
    {
        base.LoadComplete();
        richPresence = new DiscordRichPresence();
        LocalConfig.DiscordRichPresence
            .BindValueChanged(e => richPresence.SetConnected(e.NewValue), true);
    }

    public override void SetHost(GameHost host)
    {
        base.SetHost(host);

        if (!Util.IsLocal)
        {
            // costs ~25ms
            var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetType(), "game.ico");
            if (iconStream != null)
                host.Window.SetIconFromStream(iconStream);
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        richPresence?.Dispose();
        richPresence = null;
        base.Dispose(isDisposing);
    }
}
