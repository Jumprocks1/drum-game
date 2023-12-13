using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Browsers;

public class DelayedImage : Sprite
{
    double timeVisible;

    CancellationTokenSource cancellationTokenSource;

    static bool allowDownload = true; // in the future this will be set from config

    protected override void Update()
    {
        base.Update();

        if (timeVisible > LoadDelay || _target.Path == null) return;
        timeVisible += Time.Elapsed;

        if (timeVisible > LoadDelay)
        {
            var (path, url) = _target;

            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;


            if (!File.Exists(path))
            {
                if (url != null && allowDownload)
                {
                    // we don't want to cancel the download task ever
                    DownloadTask task = null;
                    task = new DownloadTask(url, path)
                    {
                        NoPopup = true,
                        PreRunCheck = _ => allowDownload,
                        OnCompletedAction = () =>
                        {
                            if (task.Failed)
                            {
                                allowDownload = false;
                                return;
                            }
                            ApplyTexture(path, token);
                        }
                    };
                    task.Enqueue();
                }
                return;
            }
            else
            {
                ApplyTexture(path, token);
            }
        }
    }

    void ApplyTexture(string path, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        // we have to use sketchy scheduler so that TextureStore.Get doesn't throw exceptions from WaitSafely
        Task.Factory.StartNew(() =>
        {
            var texture = Util.Resources.LargeTextures.Get(path);
            if (token.IsCancellationRequested)
            {
                texture.Dispose();
                return;
            }
            if (texture == null) return;
            if (texture.Width != texture.Height)
            {
                if (texture.Width > texture.Height)
                {
                    var trim = (texture.Width - texture.Height) / 2;
                    texture = texture.Crop(new osu.Framework.Graphics.Primitives.RectangleF(trim, 0, texture.Width - trim * 2, texture.Height));
                }
            }
            Schedule(() =>
            {
                if (token.IsCancellationRequested)
                {
                    texture.Dispose();
                    return;
                }
                this.FadeInFromZero(TransformDuration, Easing.OutQuint);
                Texture = texture;
            });
        }, token, TaskCreationOptions.HideScheduler, Util.LoadScheduler);
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = null;
    }

    public void Reset()
    {
        timeVisible = 0;
        Texture = null;
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = null;
        // we don't use ClearTransforms since that could maybe leave the Sprite with Alpha = 0
        FinishTransforms();
    }


    (string Path, string Url) _target;
    public (string Path, string Url) Target
    {
        get => _target;
        set
        {
            if (_target == value) return;
            Reset();
            _target = value;
        }
    }
    public double LoadDelay = 250;
    public double TransformDuration => 200;
}