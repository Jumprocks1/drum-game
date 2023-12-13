using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace DrumGame.Game.Utils;

public class DownloadTask : BackgroundTask
{
    public readonly string Url;
    public string OutputPath;
    HttpClient Client;
    public long? TotalBytes;

    public override string ProgressText =>
        Success ? "Completed" :
        TotalBytes == null ? $"{BytesRead / 1_000_000:0.0}MB downloaded" :
        $"{(double)BytesRead / TotalBytes.Value * 100:0.00}%";
    long BytesRead;

    public bool DeleteIfExists; // skips download if file exists

    async Task Download()
    {
        if (!DeleteIfExists && File.Exists(OutputPath) || Cancelled) return;
        Client = new HttpClient();

        var temp = OutputPath == null;
        try
        {
            using var response = await Client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead);
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength != null) TotalBytes = contentLength;
            if (response.IsSuccessStatusCode)
            {
                using var downloadStream = await response.Content.ReadAsStreamAsync();
                if (temp) OutputPath = Path.GetTempFileName();
                else Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

                using var outputStream = File.Open(OutputPath, FileMode.Create, FileAccess.Write);
                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await outputStream.WriteAsync(buffer, 0, bytesRead);
                    BytesRead += bytesRead;
                    if (Cancelled) return;
                }
            }
            else
            {
                FailureReason = $"Download failed. Status code: {(int)response.StatusCode} ({response.StatusCode})";
            }
        }
        finally
        {
            if (!Success && temp && OutputPath != null) File.Delete(OutputPath);
        }
    }

    static Task Download(BackgroundTask task) => ((DownloadTask)task).Download();
    public DownloadTask(string url, string outputPath = null) : base(Download)
    {
        Url = url;
        var fileName = url.Substring(url.LastIndexOf("/") + 1);
        Name = fileName;
        OutputPath = outputPath;
    }
    public override void Cancel()
    {
        Client?.Dispose();
        Client = null;
        base.Cancel();
    }
}
