using System;
using DrumGame.Game.Utils;
using ManagedBass.Mix;
using osu.Framework.Audio.Track;

namespace DrumGame.Game.Media;


public class SyncQueue : ConcurrentHashSet<int>
{
    public void UnbindAndClear(Track track)
    {
        lock (l_HashSet)
        {
            var trackHandle = track is not TrackBass tb ? 0 : Util.Get<int>(tb, "activeStream");
            if (trackHandle != 0)
            {
                // if (l_HashSet.Count > 0)
                //     Console.WriteLine($"Dequeue {l_HashSet.Count}");
                foreach (var sync in l_HashSet)
                    BassMix.ChannelRemoveSync(trackHandle, sync);
            }
            Clear();
        }
    }
}