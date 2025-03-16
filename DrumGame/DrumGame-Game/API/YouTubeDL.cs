using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Browsers;
using DrumGame.Game.Commands.Requests;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Logging;

namespace DrumGame.Game.API;

public static class YouTubeDL
{
    static string _executable;
    public static string Executable => _executable ??= LocateExecutable();
    static string LocateExecutable() => Util.Resources.LocateExecutable("yt-dlp", "youtube-dl");
    public class DownloadConfig
    {
        public bool Video;
        public bool? Vorbis;
        public string Format => Video ? "136" : "bestaudio";
    }
    public static BackgroundTask DownloadBackground(string url, string outputPath = null, DownloadConfig downloadConfig = null)
    {
        var backgroundTask = new BackgroundTask(t =>
        {
            return Download(t, url, outputPath, downloadConfig);
        })
        {
            Name = "YouTube-DL",
            NameTooltip = $"Downloading {url}"
        };
        backgroundTask.Enqueue();
        return backgroundTask;
    }

    public static void AskForDownload(string url, string outputPath, Action<BackgroundTask> onSuccess)
    {
        var req = Util.Palette.Request(new Modals.RequestConfig
        {
            Title = $"Do you want to download audio from {url}?",
            Description = "This will attempt to use YouTubeDL",
            CloseText = null
        });
        req.Add(new ButtonArray(i =>
        {
            req.Close();
            if (i == 0) DownloadBackground(url, outputPath).OnSuccess += onSuccess;
        },
            new ButtonOption { Text = "Download" },
            new ButtonOption { Text = "Cancel" })
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Y = 5
        });
    }

    public static bool PreferVorbis => Util.ConfigManager.Get<bool>(Stores.DrumGameSetting.PreferVorbisAudio);

    public async static Task<string> Download(BackgroundTask task, string url, string outputPath, DownloadConfig config = null)
    {
        if (File.Exists(outputPath))
            return outputPath;

        config ??= new();

        var ex = Executable;
        if (ex == null)
        {
            task.Fail("Failed to find YouTubeDL executable",
                $"Please install yt-dlp.\nThe executable should be visible from the system path.\nAlternatively, you can place it in {Util.Resources.GetAbsolutePath("lib")}");
            return null;
        }
        var processInfo = new ProcessStartInfo(ex);
        processInfo.ArgumentList.Add("-f");
        processInfo.ArgumentList.Add(config.Format);
        processInfo.ArgumentList.Add("-o");
        processInfo.ArgumentList.Add(outputPath);
        processInfo.RedirectStandardError = true;
        if (config.Vorbis ?? PreferVorbis)
        {
            processInfo.ArgumentList.Add("-x");
            processInfo.ArgumentList.Add("--audio-format");
            processInfo.ArgumentList.Add("vorbis");
        }
        if (url.Length == 11)
            url = "youtu.be/" + url; // without this, IDs starting with `-` will fail
        processInfo.ArgumentList.Add(url);

        var process = Process.Start(processInfo);

        var errors = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
                {
                    errors.AppendLine(e.Data);
                    Console.Error.WriteLine(e.Data);
                };
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (!File.Exists(outputPath))
        {
            // not sure why this gets messed up by YouTubeDL sometimes
            // seems like they changed the file names in a recent update
            // basically it downloads to `outputPath` but then extracts audio to `outputPath.ogg`
            if (File.Exists(outputPath + ".ogg"))
                File.Move(outputPath + ".ogg", outputPath);
            if (File.Exists(outputPath + ".webm"))
                File.Move(outputPath + ".webm", outputPath);
        }

        if (!File.Exists(outputPath))
        {
            if (process.ExitCode != 0)
            {
                FailTask(task, process.ExitCode, errors.ToString(), url);
            }
            else
            {
                task.Fail("Output file from YouTubeDL does not exist", errors.ToString().Trim());
            }
            return null;
        }

        return outputPath;
    }

    static string GetLink(string url)
    {
        var yt = BeatmapSelector.YouTubeRegex.Match(url);
        if (yt != null)
            return $"https://youtube.com/watch?v={yt.Groups[1]}";
        return url;
    }

    public static void FailTask(BackgroundTask task, int exitCode, string errors, string url)
    {
        errors = errors.Trim();
        if (errors.Contains("Video unavailable"))
        {
            var link = GetLink(url);
            task.Fail($"Video unavailable", $"The video {url} is unavailable.\nIt may be blocked, deleted, or privated.\nFor more details, try opening {link} in a web browser.");
        }
        else
            task.Fail($"YouTubeDL failed, exit code: {exitCode}", errors);
    }

    public static void ForceLoadYouTubeAudio(Beatmap beatmap, Action<BackgroundTask> onSuccess, bool askBeforeDownload)
    {
        if (beatmap.YouTubeID != null)
        {
            var youTubePath = beatmap.YouTubeAudioPath;
            if (File.Exists(youTubePath)) return;

            var exe = LocateExecutable();
            void RequestAudioDownload()
            {
                if (exe == null)
                {
                    Logger.Log("Failed to locate YouTubeDL", level: LogLevel.Important);
                    return;
                }
                if (askBeforeDownload)
                    AskForDownload(beatmap.YouTubeID, youTubePath, onSuccess);
                else
                    DownloadBackground(beatmap.YouTubeID, youTubePath).OnSuccess += onSuccess;
            }

            if (exe == null)
            {
                var modal = Util.Palette.Push(new DownloadYouTubeDLModal("YouTubeDL can be used to download audio"));
                modal.OnComplete = () =>
                {
                    exe = LocateExecutable();
                    Util.EnsureUpdateThread(() =>
                    {
                        modal.Close();
                        // this leaves the notification sidebar open still
                        RequestAudioDownload();
                    });
                };
            }
            else RequestAudioDownload();
        }
    }

    public static void TryFixAudio(Beatmap beatmap, Action<BackgroundTask> onSuccess)
    {
        if (File.Exists(beatmap.FullAudioPath())) return;
        ForceLoadYouTubeAudio(beatmap, onSuccess, true);
    }
}