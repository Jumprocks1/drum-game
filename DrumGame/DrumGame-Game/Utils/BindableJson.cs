using DrumGame.Game.Interfaces;
using Newtonsoft.Json;
using osu.Framework.Bindables;

namespace DrumGame.Game.Utils;

public class BindableJson<T> : Bindable<T> where T : class, IInit, IChangedEvent, new()
{
    public override void Parse(object input)
    {
        switch (input)
        {
            case string str:
                T v = null;
                if (!string.IsNullOrWhiteSpace(str))
                    v = JsonConvert.DeserializeObject<T>(str);
                Value = v ?? new T();
                if (Value is IInit init)
                    init.Init();
                break;
            default:
                base.Parse(input);
                break;
        }
    }
    protected override Bindable<T> CreateInstance() => new BindableJson<T>();
    void Register(T oldValue, T newValue)
    {
        if (oldValue != null) oldValue.Changed -= TriggerChange;
        if (newValue == null) newValue.Changed += TriggerChange;
    }

    public BindableJson()
    {
        ValueChanged += e =>
        {
            if (e.OldValue != e.NewValue)
                Register(e.OldValue, e.NewValue);
        };
    }

    public override string ToString() => JsonConvert.SerializeObject(Value, new JsonSerializerSettings
    {
        DefaultValueHandling = DefaultValueHandling.Ignore
    });
}