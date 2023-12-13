using System.Collections.Generic;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Data;

namespace DrumGame.Game.Interfaces;

public interface IBeatmapChangePoint<T> : IDefault<T>, ITickTime, ICongruent<T>
{
    static abstract List<T> GetList(Beatmap beatmap);
    T WithTime(int time);
}