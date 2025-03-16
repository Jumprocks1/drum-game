using System.Collections.Generic;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Modals;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public static class HitWindowSettingsInfo
{
    public static DropdownSettingInfo<HitWindowOption> Create()
    {
        var target = Util.ConfigManager.GetBindable<HitWindowPreference>(DrumGameSetting.HitWindowPreference);
        var options = new List<HitWindowOption>() {
                new(HitWindowPreference.Lax),
                new(HitWindowPreference.Standard),
                new(HitWindowPreference.Strict),
                new(HitWindowPreference.Custom),
            };
        var bindable = WrappedBindable.C(target, e => options.Find(o => o.Preference == e), e => e.Preference);
        return new DropdownSettingInfo<HitWindowOption>("Hit Windows", bindable)
        {
            Options = options,
            Tooltip = "Changes the timing required for perfect/good/bad/miss judgements.",
            AfterRender = control => control.AddIconButton(OpenCustom, FontAwesome.Solid.Cog, "<command>Edit Custom Windows</>")
        };
    }
    static void OpenCustom()
    {
        // if we hit the edit button while we are set to "Lax", then the modal will load the lax values as defaults
        var currentWindow = HitWindows.GetWindowsForCurrentPreference();

        static HitWindows getNewWindow(RequestModal e) =>
            HitWindows.MakeCustom(
                e.GetValue<float?>(0) ?? HitWindows.DefaultPerfectWindow,
                e.GetValue<float?>(1) ?? HitWindows.DefaultGoodWindow,
                e.GetValue<float?>(2) ?? HitWindows.DefaultBadWindow,
                e.GetValue<float?>(3) ?? HitWindows.DefaultHitWindow);

        Util.Palette.Request(new RequestConfig
        {
            Title = "Setting Custom Hit Windows",
            CommitText = "Save",
            CanCommit = e =>
            {
                var validate = getNewWindow(e).Validate();
                if (validate == null) return true;
                Util.Palette.ShowMessage(validate);
                return false;
            },
            OnCommit = e =>
            {
                Util.ConfigManager.SetValue(DrumGameSetting.CustomHitWindows, getNewWindow(e).MsWindowString);
                Util.ConfigManager.SetValue(DrumGameSetting.HitWindowPreference, HitWindowPreference.Custom);
            },
            Fields = [
                new FloatFieldConfig {
                    Label = "Perfect Hit Window",
                    DefaultValue = currentWindow.PerfectWindow
                },
                new FloatFieldConfig {
                    Label = "Good Hit Window",
                    DefaultValue = currentWindow.GoodWindow
                },
                new FloatFieldConfig {
                    Label = "Bad Hit Window",
                    DefaultValue = currentWindow.BadWindow
                },
                new FloatFieldConfig {
                    Label = "Total Hit Window",
                    DefaultValue = currentWindow.HitWindow,
                    MarkupTooltip = "Hits outside of this window are ignored. Hits inside this window but outside the other windows will count as misses."
                }
            ]
        });
    }
}

public class HitWindowOption : IFilterable
{
    public HitWindowPreference Preference;
    public string Name => Preference.ToString();
    public HitWindows Windows;
    public string MarkupTooltip => Windows.MarkupTooltip;
    string IFilterable.MarkupTooltip => MarkupTooltip;
    public HitWindowOption(HitWindowPreference preference)
    {
        Preference = preference;
        Windows = HitWindows.GetWindows(Preference);
    }
}
