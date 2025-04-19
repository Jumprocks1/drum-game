using System;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public static class AdvancedChannelSettings
{
    public static (Action Action, string Tooltip)? SettingsFor(DrumChannel channel)
    {
        if (channel.IsHiHat())
        {
            Action open = () =>
            {
                var target = Util.ConfigManager.HiHatRange;
                var open = new BindableNumber<byte>();
                var close = new BindableNumber<byte>();
                open.ValueChanged += ev =>
                {
                    if (close.Value < ev.NewValue) close.Value = ev.NewValue;
                    target.Value = (ev.NewValue, target.Value.Item2);
                };
                close.ValueChanged += ev =>
                {
                    if (open.Value > ev.NewValue) open.Value = ev.NewValue;
                    target.Value = (target.Value.Item1, ev.NewValue);
                };

                void onChange(ValueChangedEvent<(byte, byte)> ev)
                {
                    open.Value = ev.NewValue.Item1;
                    close.Value = ev.NewValue.Item2;
                }
                target.BindValueChanged(onChange, true);
                var modal = SettingsListBuilder.OpenSettingsMenu("Advanced Hi-Hat Settings", e => e
                    .Add(new ParsableSettingInfo<byte>("Open threshold", open)
                    {
                        Tooltip = "Hi-hat hits with a control value less than this will be treated as open."
                        + "\nHits between the open and close thresholds will trigger the half open hi-hat channel"
                        + "\nRecommend 40 or less (or 255 to disable this feature)."
                    })
                    .Add(new ParsableSettingInfo<byte>("Close threshold", close)
                    {
                        Tooltip = "Hi-hat hits with a control value greater than this will be treated as closed."
                        + "\nHits between the open and close thresholds will trigger the half open hi-hat channel"
                        + "\nRecommend 80 or greater."
                    }),
                    "These settings cause pressing the HH pedal to trigger different channels based on how hard it is pressed."
                    + "\nTo disable this feature, set the open threshold to 255."
                    + "\nTypically HH control values range from 0 to 127, with 127 being fully pressed."
                  );
                modal.OnDispose += () =>
                {
                    target.ValueChanged -= onChange;
                    close.UnbindAll();
                    open.UnbindAll();
                };
                // TODO we could add a HH control reader here that shows the current value in DrumMidiHandler.hiHatPosition
            };
            return (open, "Open advanced hi-hat settings");
        }
        return null;
    }
}