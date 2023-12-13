using System;
using System.Diagnostics;
using System.Linq;
using DrumGame.Game.Stores;
using osu.Framework.Threading;

namespace DrumGame.Game.Utils;

// Useful when we know we need to load metadata for >100 files
// This only ever runs on the update thread. To avoid chunking the framework, we use a stopwatch
public class BulkActionScheduler
{
    Stopwatch Stopwatch;
    public double TargetFramerate;
    public TimeSpan Elapsed => Stopwatch.Elapsed;
    public int Progress;
    public int TotalCount;
    public double ProgressRatio => (double)Progress / TotalCount;
    public string ProgressPercent => $"{ProgressRatio * 100:0.0}%";
    public Action<int> Action;
    public Action OnComplete;
    public Action AfterTick;
    ScheduledDelegate Scheduled;
    public BulkActionScheduler(Action<int> action, int count, double targetFramerate = 60)
    {
        Action = action;
        TotalCount = count;
        TargetFramerate = targetFramerate;

    }
    public void Start() // can't start in constructor since OnComplete won't be specified
    {
        if (TotalCount > 0)
        {
            Stopwatch = Stopwatch.StartNew();
            Scheduled = Util.UpdateThread.Scheduler.AddDelayed(Tick, 0, true);
            Scheduled.PerformRepeatCatchUpExecutions = false;
        }
        else
        {
            OnComplete?.Invoke();
        }
    }
    void Tick()
    {
        var endTime = Stopwatch.ElapsedTicks + Stopwatch.Frequency / TargetFramerate;
        // do while loop so we always do at least 1 loop
        do
        {
            if (Progress >= TotalCount) break;
            Action(Progress);
            Progress += 1;
        } while (Stopwatch.ElapsedTicks < endTime);
        AfterTick?.Invoke();
        if (Progress >= TotalCount)
        {
            Stopwatch.Stop();
            Scheduled.Cancel();
            OnComplete?.Invoke();
        }
    }
    public void Cancel() => Scheduled.Cancel();
}