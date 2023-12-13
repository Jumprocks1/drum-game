using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace DrumGame.Game.Utils;

public class BackgroundTask : IDisposable
{
    static List<BackgroundTask> Queue;
    public object ActionResult;
    static object _lock = new();

    public virtual string ProgressText => $"{progressPercent * 100:0.00}%";
    double progressPercent;
    public void UpdateProgress(double percent)
    {
        progressPercent = percent;
    }

    public string Name;
    public string NameTooltip;

    public bool NoPopup;

    public event Action OnCompleted; // simple event, will always run on a background thread
    public Action OnCompletedAction { set => OnCompleted += value; }
    public event Action<BackgroundTask> OnSuccess; // simple event, will always run on a background thread
    public Func<BackgroundTask, bool> PreRunCheck; // returns true for success/continue

    public static void Enqueue(BackgroundTask task) // TODO probably need to dispose of these on game exit
    {
        Util.NotificationOverlay?.Register(task);
        bool run = false;
        lock (_lock)
        {
            Queue ??= new();
            if (Queue.Count == 0) run = true;
            Queue.Add(task);
        }
        if (run)
        {
            Task.Run(async () =>
            {
                var i = 0;
                while (true)
                {
                    lock (_lock)
                    {
                        if (i >= Queue.Count)
                        {
                            Queue.Clear();
                            break;
                        }
                    }
                    var task = Queue[i];
                    if (!task.Completed)
                    {
                        try
                        {
                            await task.RunInForeground();
                        }
                        catch (BackgroundTaskException e)
                        { task.Exception = e; }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Uncaught exception in background task");
                            task.Exception = e;
                            task.FailureReason = "Uncaught exception";
                        }
                        task.Complete();
                    }
                    i += 1;
                }
            });
        }
    }



    readonly CancellationTokenSource Cancellation = new();
    public CancellationToken Token => Cancellation.Token;

    readonly Func<BackgroundTask, Task> Action;

    public BackgroundTask(Func<BackgroundTask, Task> action)
    {
        Action = action;
    }
    public BackgroundTask(Action<BackgroundTask> action)
    {
        Action = t =>
        {
            action(t);
            return Task.CompletedTask;
        };
    }

    // this doesn't really get set for CPU tasks (non-async)
    // don't rely on it existing prior to completion
    public Task RunningTask;

    public Task RunInForeground()
    {
        if (Cancellation.IsCancellationRequested) return Task.FromCanceled(Token);
        if (PreRunCheck != null && !PreRunCheck(this))
        {
            FailureReason ??= "Pre-run check failed";
            return Task.CompletedTask;
        }
        RunningTask = Action(this);
        return RunningTask;
    }

    public Exception Exception { get; private set; }
    string _failureReason;
    // this should be short, showed in the notification directly
    public string FailureReason
    {
        get => _failureReason ?? Exception?.Message; set
        {
            _failureReason = value;
        }
    }

    string _failureDetails;
    public string FailureDetails // tooltip
    {
        get => _failureDetails ?? Exception?.ToString(); set
        {
            _failureDetails = value;
        }
    }

    public bool Failed => FailureReason != null;
    public bool Success => RunningTask != null && RunningTask.IsCompletedSuccessfully && !Failed && !Cancelled;
    public bool Completed => Cancelled || Failed || (RunningTask != null && RunningTask.IsCompleted);
    public bool Cancelled => Cancellation.IsCancellationRequested;

    public void Complete()
    {
        lock (this)
        {
            if (!Cancelled) // task.Cancel calls OnCompleted already
            {
                OnCompleted?.Invoke();
                OnCompleted = null;
                if (Success)
                {
                    var t = RunningTask.GetType();
                    var hasResult = t.IsGenericType;
                    if (hasResult) // couldn't find a better way to do this, oh well
                    {
                        var prop = t.GetProperty("Result");
                        if (prop != null)
                            ActionResult = prop.GetValue(RunningTask);
                    }
                    OnSuccess?.Invoke(this);
                    OnSuccess = null;
                }
            }
        }
    }

    public virtual void Cancel()
    {
        lock (this)
        {
            if (!Completed)
            {
                Cancellation.Cancel();
                OnCompleted?.Invoke();
                OnCompleted = null;
            }
        }
    }

    public void Dispose()
    {
        Cancel();
        Cancellation.Dispose();
    }

    public void Enqueue() => Enqueue(this);

    public void Fail(string reason, string details)
    {
        FailureReason = reason;
        if (!string.IsNullOrWhiteSpace(details))
            FailureDetails = details;
    }
    public void Fail(Exception exc)
    {
        Exception = exc;
    }
}

public class BackgroundTaskException : Exception
{
    public BackgroundTaskException(string failureReason) : base(failureReason)
    {

    }
}