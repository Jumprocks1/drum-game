using System;

namespace DrumGame.Game.API;

public abstract class UserActivity
{
    public abstract string State { get; }
    public abstract string Details { get; }
    public virtual DateTime? Start => null;
    public virtual DateTime? End => null;


    static UserActivity _activity = StaticActivity.Instance;
    public static UserActivity Activity
    {
        get => _activity; set
        {
            _activity = value;
            TriggerActivityChanged();
        }
    }

    public class ActivityChangedEvent
    {
    }

    public static event Action<ActivityChangedEvent> ActivityChanged;
    public static void TriggerActivityChanged() => ActivityChanged?.Invoke(null);

    public static void Set(StaticActivityType type)
    {
        if (_activity is StaticActivity sa)
        {
            if (StaticActivity.Type == type) return;
            StaticActivity.Type = type;
        }
        else
        {
            StaticActivity.Type = type;
            Activity = StaticActivity.Instance;
        }
    }
}
