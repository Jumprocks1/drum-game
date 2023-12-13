using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Stores;
using osu.Framework.Audio;
using osu.Framework.Bindables;

namespace DrumGame.Game.Components;

public class VolumeController : IDisposable
{
    // AudioManager saves volume settings. We will need to learn how to save more
    // See framework.ini
    readonly AudioManager manager;
    public ConfigVolumeBinding MasterVolume;
    public ConfigVolumeBinding TrackVolume;
    public ConfigVolumeBinding SampleVolume;
    public ConfigVolumeBinding HitVolume;
    public ConfigVolumeBinding MetronomeVolume;
    public CommandController command;
    // TODO can add separate binding just for metronome or other sounds
    public VolumeController(AudioManager manager, CommandController command, DrumGameConfigManager config)
    {
        this.command = command;
        this.manager = manager;

        MasterVolume = new ConfigVolumeBinding(
            config.GetBindable<double>(DrumGameSetting.MasterVolume),
            config.GetBindable<bool>(DrumGameSetting.MasterMuted));
        manager.Volume.BindTo(MasterVolume.Aggregate);

        TrackVolume = new ConfigVolumeBinding(
            config.GetBindable<double>(DrumGameSetting.TrackVolume),
            config.GetBindable<bool>(DrumGameSetting.TrackMuted));
        manager.Tracks.Volume.BindTo(TrackVolume.Aggregate);

        SampleVolume = new ConfigVolumeBinding(
            config.GetBindable<double>(DrumGameSetting.SampleVolume),
            config.GetBindable<bool>(DrumGameSetting.SampleMuted));
        manager.Samples.Volume.BindTo(SampleVolume.Aggregate);

        HitVolume = new ConfigVolumeBinding(
            config.GetBindable<double>(DrumGameSetting.HitVolume),
            config.GetBindable<bool>(DrumGameSetting.HitMuted));

        MetronomeVolume = new ConfigVolumeBinding(
            config.GetBindable<double>(DrumGameSetting.MetronomeVolume),
            config.GetBindable<bool>(DrumGameSetting.MetronomeMuted));

        command.RegisterHandlers(this);
    }

    public void Dispose()
    {
        command.RemoveHandlers(this);
        MetronomeVolume.Dispose();
        HitVolume.Dispose();
        TrackVolume.Dispose();
        MasterVolume.Dispose();
    }
    [CommandHandler] public void ToggleMute() => MasterVolume.ToggleMute();
    [CommandHandler] public void IncreaseVolume() => MasterVolume.IncreaseLevel();
    [CommandHandler] public void DecreaseVolume() => MasterVolume.DecreaseLevel();
    [CommandHandler] public void Unmute() => MasterVolume.Unmute();
    [CommandHandler] public void Mute() => MasterVolume.Mute();
    [CommandHandler] public void ToggleMetronome() => MetronomeVolume.ToggleMute();
}

public interface IVolumeBinding : IDisposable
{
    public double ComputedValue { get; }
    public BindableNumber<double> Aggregate { get; }
    public event Action<double> ComputedValueChanged;
    public void Mute();
    public void Unmute();
    public void ToggleMute()
    {
        if (ComputedValue > 0) Mute(); else Unmute();
    }
}
public class ConfigVolumeBinding : IDisposable, IVolumeBinding
{
    public readonly Bindable<bool> Muted;
    public readonly Bindable<double> Level;
    public BindableNumber<double> Aggregate { get; }
    public event Action<double> ComputedValueChanged;
    public double ComputedValue => Aggregate.Value;
    public ConfigVolumeBinding(Bindable<double> level, Bindable<bool> muted)
    {
        Level = level;
        Muted = muted;
        Aggregate = new BindableNumber<double>(muted.Value ? 0 : level.Value);
        Muted.ValueChanged += MutedChanged;
        Level.ValueChanged += LevelChanged;
        Aggregate.ValueChanged += AggregateChanged;
    }

    private void AggregateChanged(ValueChangedEvent<double> e)
    {
        if (Aggregate.Value <= 0) Muted.Value = true;
        else
        {
            if (Muted.Value) Muted.Value = false;
            if (Level.Value != e.NewValue) Level.Value = e.NewValue;
        }
        ComputedValueChanged?.Invoke(e.NewValue);
    }
    private void MutedChanged(ValueChangedEvent<bool> e)
    {
        Aggregate.Value = e.NewValue ? 0 : Level.Value;
    }
    private void LevelChanged(ValueChangedEvent<double> e)
    {
        if (!Muted.Value) Aggregate.Value = e.NewValue;
        else Muted.Value = false;
    }

    public void Dispose()
    {
        Aggregate.UnbindAll();
        Level.ValueChanged -= LevelChanged;
        Muted.ValueChanged -= MutedChanged;
    }
    public void Mute() => Muted.Value = true;
    public void Unmute()
    {
        if (Aggregate.Value > 0) return;
        if (Level.Value <= 0) Level.Value = 0.01;
        Muted.Value = false;
    }
    public void ToggleMute()
    {
        if (ComputedValue > 0) Mute(); else Unmute();
    }
    public void IncreaseLevel() { Level.Value += 0.02; if (Muted.Value) { Muted.Value = false; } }
    public void DecreaseLevel() => Level.Value -= 0.02;
}
public class LevelVolumeBinding : IDisposable, IVolumeBinding
{
    public BindableNumber<double> Aggregate { get; }
    double unmuteMemory = 0.01;
    public event Action<double> ComputedValueChanged;
    public double ComputedValue => Aggregate.Value;
    public LevelVolumeBinding(BindableNumber<double> level)
    {
        Aggregate = level;
        Aggregate.ValueChanged += LevelChanged;
    }
    public void Mute()
    {
        Aggregate.Value = 0;
    }
    public void Unmute()
    {
        Aggregate.Value = unmuteMemory;
    }
    private void LevelChanged(ValueChangedEvent<double> e)
    {
        if (e.NewValue > 0) unmuteMemory = e.NewValue;
        ComputedValueChanged?.Invoke(e.NewValue);
    }

    public void Dispose()
    {
        Aggregate.ValueChanged -= LevelChanged;
    }
}

