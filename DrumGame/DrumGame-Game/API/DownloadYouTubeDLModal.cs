using System;
using System.IO;
using DrumGame.Game.Components;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.API;

public class DownloadYouTubeDLModal : RequestModal
{
    public const string YtDlpExe = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    public const string Releases = "https://github.com/yt-dlp/yt-dlp/releases/latest";
    public Action OnComplete; // runs on background thread
    public DownloadYouTubeDLModal(string title = "YouTubeDL not found") : base(new RequestConfig { Title = title })
    {
        Add(new MarkupText
        {
            Text = "Would you like to download yt-dlp from GitHub?\nAlternatively you can manually download yt-dlp and add it to your system path."
        });
        var button = new DrumButtonTooltip
        {
            Width = 250,
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Height = 30,
            Y = 60,
        };
        var platform = RuntimeInfo.OS;
        if (platform == RuntimeInfo.Platform.Windows)
        {
            button.Text = "Download yt-dlp.exe (~13MB)";
            button.TooltipText = YtDlpExe;
            button.Action = () =>
            {
                var lib = Util.Resources.GetDirectory("lib");
                var task = new DownloadTask(YtDlpExe, Path.Join(lib.FullName, "yt-dlp.exe"));
                task.Enqueue();
                task.OnCompletedAction = OnComplete;
            };
        }
        else
        {
            button.Text = $"View GitHub releases";
            button.TooltipText = "Please install yt-dlp manually. Make sure the executable is in your system path.";
            button.Action = () => Util.Host.OpenUrlExternally(Releases);
        }
        Add(button);
    }
}