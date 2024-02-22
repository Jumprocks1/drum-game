
using System.IO;
using System.Threading.Tasks;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Browsers.BeatmapSelection;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;

namespace DrumGame.Game.Browsers;

// component since we use volume easing (TransformBindableTo)
public class PreviewLoader : Component
{
    class PreviewInfo
    {
        public string AudioPath;
        public string YouTubeId;
        public double? PreviewTime;
        public double YouTubeOffset;
        public double RelativeVolume;
    }
    public const double DefaultPreviewTime = 0.4; // as a proportion
    const double VolumeEaseDuration = 500;

    PreviewInfo previewTarget; // only use inside quickLock
    PreviewInfo Loading; // only update inside quickLock in background task
    PreviewInfo Loaded; // only update inside quickLock in background task
    Track CurrentTrack; // only assign inside trackLock
    string leasedTrack; // only assign inside trackLock
    public void LeaseTrack(Track track, string path)
    {
        lock (trackLock)
        {
            if (CurrentTrack == null)
            {
                leasedTrack = path;
                CurrentTrack = track;
            }
        }
    }

    bool disposed = false;


    object trackLock = new(); // hold this when working with CurrentTrack
    object quickLock = new(); // hold this for everything else
    public void SetPendingTarget(BeatmapSelectorMap beatmap)
    {
        lock (quickLock)
        {
            // don't clear target if paths match
            if (beatmap == null || previewTarget == null ||
                // unfortunately this check doesn't work for DTX files with preview tracks
                // since this uses BeatmapSelectorMap, we don't have access to the PreviewAudio field
                previewTarget.AudioPath != beatmap.FullAssetPath(beatmap.LoadedMetadata.Audio))
                SetTarget(null);
        }
    }

    public void RetryAudio() // this is very cheap if the audio is already working correctly
    {
        lock (quickLock)
        {
            var target = previewTarget;
            if (Loaded == target || target == null) return;
            SetTarget(target, true);
        }
    }

    PreviewInfo GetTarget(Beatmap beatmap)
    {
        if (beatmap == null) return null;
        if (beatmap.PreviewAudio != null)
        {
            var previewAudio = beatmap.FullAssetPath(beatmap.PreviewAudio);
            if (File.Exists(previewAudio)) return new PreviewInfo
            {
                AudioPath = previewAudio,
                RelativeVolume = beatmap.CurrentRelativeVolume,
                PreviewTime = 0
            };
        }
        return new PreviewInfo
        {
            AudioPath = beatmap.FullAudioPath(),
            RelativeVolume = beatmap.CurrentRelativeVolume,
            PreviewTime = beatmap.PreviewTime,
            YouTubeId = beatmap.YouTubeID,
            YouTubeOffset = beatmap.YouTubeOffset
        };
    }

    public void SetTarget(Beatmap beatmap)
    {
        lock (quickLock) { SetTarget(GetTarget(beatmap), false); }
    }
    void SetTarget(PreviewInfo newTarget, bool force)
    {
        lock (quickLock)
        {
            if (!force)
            {
                if (newTarget == previewTarget) return;
                if (previewTarget != null && newTarget != null && previewTarget.AudioPath == newTarget.AudioPath) return; // don't change if same audio path
            }
            previewTarget = newTarget;
            TargetChanged();
        }
    }

    void TargetChanged()
    {
        if (disposed) return;
        PreviewInfo target;
        lock (quickLock)
        {
            target = previewTarget;
            if (target == Loading) return;
            if (Loaded == target) return;
            if (leasedTrack != null && leasedTrack == target?.AudioPath) return;
            Loading = target;
        }
        // this will drop any locks we are holding
        Task.Factory.StartNew(async () =>
        {
            var previewOffset = 0d;
            TrackBass track = null;
            if (target != null)
            {
                track = Util.Resources.GetTrack(target.AudioPath) as TrackBass;
                if (track == null) // try loading YouTube
                {
                    track = Util.Resources.GetTrack(Util.Resources.YouTubeAudioPath(target.YouTubeId)) as TrackBass;
                    previewOffset = target.YouTubeOffset;
                }
            }
            if (track == null)
            {
                lock (trackLock)
                {
                    lock (quickLock)
                    {
                        Loading = null;
                        Loaded = null;
                    }
                    CurrentTrack?.Dispose();
                    CurrentTrack = null;
                    leasedTrack = null;
                }
                return;
            }
            var volume = track.Volume;
            var targetVolume = target.RelativeVolume;
            volume.Value = 0;
            if (previewTarget != target) // don't need to lock since this is just a random bailout check
            {
                track.Dispose();
                return;
            }
            var previewTime = target.PreviewTime ?? -1;
            if (previewTime != 0)
            {
                double targetTime;
                if (previewTime < 0)
                {
                    // this is mostly the same code as used in osu
                    if (track.Length == 0)
                    {
                        // force length to be populated (https://github.com/ppy/osu-framework/issues/4202)
                        await track.SeekAsync(track.CurrentTime);
                    }
                    targetTime = DefaultPreviewTime * track.Length;
                }
                else
                {
                    targetTime = previewTime;
                }
                targetTime += previewOffset;
                track.RestartPoint = targetTime;
                await track.SeekAsync(targetTime);
            }
            bool updateTrack = false;
            lock (trackLock)
            {
                lock (quickLock)
                {
                    updateTrack = previewTarget == target && Loaded != target;
                    if (updateTrack) Loaded = target;
                }
                if (updateTrack)
                {
                    var oldTrack = CurrentTrack;
                    if (oldTrack != null) oldTrack.Dispose();
                    if (disposed) track.Dispose();
                    else
                    {
                        CurrentTrack = track;
                        leasedTrack = null;
                        Schedule(() => this.TransformBindableTo(volume, targetVolume, VolumeEaseDuration, Easing.Out));
                        track.StartAsync();
                    }
                }
            }
            if (!updateTrack) track.Dispose();
        });
    }

    protected override void Dispose(bool isDisposing)
    {
        disposed = true;
        CurrentTrack?.Dispose();
        CurrentTrack = null;
        base.Dispose(isDisposing);
    }
}

