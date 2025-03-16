using System;
using DrumGame.Game.Utils;
using ManagedBass.Mix;

namespace DrumGame.Game.Media;


public class SyncQueue : ConcurrentHashSet<(int, int)>
{
    public void UnbindAndClear()
    {
        lock (l_HashSet)
        {
            foreach (var (handle, sync) in l_HashSet)
                BassMix.ChannelRemoveSync(handle, sync);
            Clear();
        }
    }
}