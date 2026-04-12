using System;
using System.Threading;
using DrumGame.Game.Utils;
using ManagedBass.Mix;

namespace DrumGame.Game.Media;


public class SyncQueue
{
    ConcurrentHashSet<(int, int)> BassEvents = new();
    public void UnbindAndClear()
    {
        lock (BassEvents.l_HashSet)
        {
            foreach (var (handle, sync) in BassEvents.l_HashSet)
                BassMix.ChannelRemoveSync(handle, sync);
            BassEvents.Clear();
        }
        ValidEvents.Clear();
    }
    public void Remove((int, int) bassEvent) => BassEvents.Remove(bassEvent);
    public void Add((int, int) bassEvent) => BassEvents.Add(bassEvent);

    // these are for non BASS events
    // usually only relevant when there's no valid track playing, so we have to use CPU clock
    public int NextAudioEvent;
    public ConcurrentHashSet<int> ValidEvents = new();
    public Action<Action> Add()
    {
        var id = Interlocked.Increment(ref NextAudioEvent);
        ValidEvents.Add(id);
        return e => { if (ValidEvents.Remove(id)) e(); };
    }
}