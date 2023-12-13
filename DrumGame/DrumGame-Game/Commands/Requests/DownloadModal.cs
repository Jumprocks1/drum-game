using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using DrumGame.Game.Modals;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;

namespace DrumGame.Game.Commands.Requests;

public class DownloadModal : RequestModal
{
    public readonly string Url;
    public readonly string OutputPath;
    bool Downloading = false;
    HttpClient Client;
    public Action<string> OnComplete;
    SpriteText ProgressText;
    public long? TotalBytes;
    bool complete = false;
    long BytesRead;
    void StartDownload()
    {
        if (Downloading) return;
        Downloading = true;
        Add(ProgressText = new()
        {
            Height = 30,
            RelativeSizeAxes = Axes.X
        });
        Client = new HttpClient();

        Task.Run(async () =>
        {
            var success = false;
            var outputPath = OutputPath;
            var temp = outputPath == null;
            try
            {
                using var response = await Client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead);
                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength != null) TotalBytes = contentLength;
                if (response.IsSuccessStatusCode)
                {
                    using var downloadStream = await response.Content.ReadAsStreamAsync();
                    if (temp) outputPath = Path.GetTempFileName();
                    else Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                    using var outputStream = File.Open(outputPath, FileMode.Create, FileAccess.Write);
                    var buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await outputStream.WriteAsync(buffer, 0, bytesRead);
                        BytesRead += bytesRead;
                        if (IsDisposed)
                        {
                            Logger.Log("Download canceled", level: LogLevel.Important);
                            return;
                        }
                    }
                    success = true;
                    Schedule(() =>
                    {
                        try
                        {
                            OnComplete?.Invoke(outputPath);
                        }
                        catch (Exception e) { Logger.Error(e, "Error after downloading"); }
                        ProgressText.Text = "Download complete!";
                        complete = true;
                    });
                }
                else
                {
                    ProgressText.Text = $"Download failed. Status code: {(int)response.StatusCode} ({response.StatusCode})";
                    complete = true;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Download failed");
                if (!success && temp && outputPath != null) File.Delete(outputPath);
            }
        });
    }
    protected override void Update()
    {
        if (ProgressText != null && !complete)
        {
            if (TotalBytes == null)
                ProgressText.Text = $"{BytesRead:0.0}MB downloaded...";
            else
                ProgressText.Text = $"Progress: {(double)BytesRead / TotalBytes * 100:0.00}%";
        }
        base.Update();
    }
    public DownloadModal(string url, string outputPath = null) : base(new RequestConfig
    {
        Title = $"Downloading {url}",
        Description = outputPath == null ? null : $"Output path: {outputPath}",
        CloseText = "Cancel"
    })
    {
        Url = url;
        OutputPath = outputPath;
        StartDownload();
    }
    protected override void Dispose(bool isDisposing)
    {
        Client?.Dispose();
        Client = null;
        base.Dispose(isDisposing);
    }
}
