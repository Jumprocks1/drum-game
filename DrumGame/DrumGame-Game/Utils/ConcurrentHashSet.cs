using System.Collections.Generic;

namespace DrumGame.Game.Utils;

public class ConcurrentHashSet<T>
{
    public HashSet<T> l_HashSet = new(); // just make sure to lock when we use this

    public bool Add(T t)
    {
        lock (l_HashSet) { return l_HashSet.Add(t); }
    }

    public bool Remove(T t)
    {
        lock (l_HashSet)
        {
            return l_HashSet.Remove(t);
        }
    }
    public void Clear()
    {
        lock (l_HashSet)
        {
            l_HashSet.Clear();
        }
    }
}