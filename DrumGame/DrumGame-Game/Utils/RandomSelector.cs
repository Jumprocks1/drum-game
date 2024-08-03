using System;
using System.Collections.Generic;
using DrumGame.Game.Browsers;
using osu.Framework.Utils;

namespace DrumGame.Game.Utils;

public class RandomSelector<T> where T : class
{
    // could also keep a HashSet for faster checks, but I think the memory usage is not worth it
    List<T> queue;
    public readonly int Capacity;
    public RandomSelector(int capacity)
    {
        Capacity = capacity;
        queue = new List<T>(capacity);
    }

    public void CircularEnqueue(T e)
    {
        if (queue.Count == Capacity) queue.RemoveAt(0);
        queue.Add(e);
    }

    public int Previous(IReadOnlyList<T> options, int current)
    {
        while (queue.Count > 0)
        {
            var index = options.IndexOf(queue.Pop());
            if (index >= 0 && index != current) return index;
        }
        return Next(options, current); // couldn't find any previous, so just go to random
    }

    public int Next(IReadOnlyList<T> options, int current)
    {
        if (options.Count == 0) return -1;
        if (options.Count == 1) return 0;
        if (!queue.Contains(options[current])) CircularEnqueue(options[current]);

        if (options.Count <= queue.Count * 1.25)
        {
            // there are relatively few remaining possiblities, so we will restrict our result set before continuing
            // this can get expensive for large capacity, but for small capacity performance is of 0 concern
            var possiblities = new List<int>();
            while (true)
            {
                for (var i = 0; i < options.Count; i++)
                    if (!queue.Contains(options[i]) && i != current) possiblities.Add(i);
                if (possiblities.Count > 0) break;
                else queue.RemoveAt(0);
            }
            var targetI = possiblities[RNG.Next(possiblities.Count)];

            CircularEnqueue(options[targetI]);
            return targetI;
        }

        // assuming the worst case scenario, at least 20% of the options are not in the queue at this point
        // this should mean we only need to go through at most ~5 RNG cycles on average, which is basically nothing
        while (true)
        {
            var targetI = RNG.Next(options.Count);
            if (!queue.Contains(options[targetI]) && targetI != current)
            {
                CircularEnqueue(options[targetI]);
                return targetI;
            }
        }
    }
}