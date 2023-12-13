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
    }

    public override void SetHost(GameHost host)
    {
        base.SetHost(host);

        if (!Util.IsLocal && host.Window is SDL2DesktopWindow window)
        {
            // costs ~25ms
            var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetType(), "game.ico");
            if (iconStream != null)
                window.SetIconFromStream(iconStream);
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        richPresence?.Dispose();
        richPresence = null;
        base.Dispose(isDisposing);
    }
}
