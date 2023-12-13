using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace DrumGame.Game.Beatmaps.Display;

public class OutsideNoteContainer : Container
{
    public int CurrentTarget;
    public override void Add(Drawable drawable)
    {
        if (mapping.TryGetValue(CurrentTarget, out var list))
        {
            list.Add(drawable);
        }
        else
        {
            mapping[CurrentTarget] = new() { drawable };
        }
        base.Add(drawable);
    }
    public void Clear(int target)
    {
        if (mapping.TryGetValue(target, out var list))
        {
            RemoveRange(list, true);
            list.Clear();
        }
    }
    Dictionary<int, List<Drawable>> mapping = new();
}
