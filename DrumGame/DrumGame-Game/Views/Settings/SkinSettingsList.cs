using System;
using System.Linq.Expressions;
using System.Numerics;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Skinning;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using DrumGame.Game.Views.Settings.SettingInfos;
using osu.Framework.Bindables;
using osu.Framework.Configuration;

namespace DrumGame.Game.Views.Settings;

public static class SkinSettingsList
{
    public static void RenderSettings(SkinSettingsView view)
    {
        view.AddSetting(new StringSettingInfo("Skin Name", Bind(e => e.Name)));
        view.AddBlockHeader("Hit Colors");
        view.AddSetting(new ColorSettingInfo("Perfect Color", Bind(e => e.HitColors.Perfect)));
        view.AddSetting(new ColorSettingInfo("Good Color", Bind(e => e.HitColors.Good)));
        view.AddSetting(new ColorSettingInfo("Bad Color", Bind(e => e.HitColors.Bad)));
        view.AddSetting(new ColorSettingInfo("Miss Color", Bind(e => e.HitColors.Miss)));
        view.AddBlockHeader("Notation Display Settings");
        view.AddSetting(new BooleanSettingInfo("Show Measure Lines", Bind(e => e.Notation.MeasureLines)));
        view.AddSetting(new DoubleSettingInfo("Note Spacing Multiplier", Bind(e => e.Notation.NoteSpacingMultiplier)));
        view.AddSetting(new BooleanSettingInfo("Smooth Scroll", Bind(e => e.Notation.SmoothScroll)));
        new SettingsListBuilder()
            .AddSubButton("Notation Color Settings", "Contains settings for background color, notation color, and note color", "Open Color Settings", e => e
                .Add(e => e.Notation.NotationColor)
                .Add(e => e.Notation.NoteColor)
                .Add("Playfield Background Color", e => e.Notation.PlayfieldBackground)
                .Add("Small Tom Color", e => e.Notation.Channels[DrumChannel.SmallTom].Color)
                .Add("Medium Tom Color", e => e.Notation.Channels[DrumChannel.MediumTom].Color)
                .Add("Large Tom Color", e => e.Notation.Channels[DrumChannel.LargeTom].Color)
            )
            .BuildTo(view);
        view.AddSetting(new DoubleSettingInfo("Zoom Multiplier", Bind(e => e.Notation.ZoomMultiplier)));
        view.AddSetting(new DoubleSettingInfo("Cursor Inset", Bind(e => e.Notation.CursorInset)));
        new SettingsListBuilder()
            .AddEnum(e => e.Notation.Judgements.Bar.Style)
            .AdvancedMenu("Advanced Bar Judgement Settings", e => e

                .Add(e => e.Notation.Judgements.Bar.EarlyColor)
                .Add(e => e.Notation.Judgements.Bar.LateColor)
                .Add(e => e.Notation.Judgements.Bar.BackgroundColor)
                .AddParsable(e => e.Notation.Judgements.Bar.Duration)
                .AddParsable(e => e.Notation.Judgements.Bar.MinimumError)
                .AddNullableParsable(e => e.Notation.Judgements.Bar.MaximumError)
                .AddParsable(e => e.Notation.Judgements.Bar.MaxWidth)
                .AddParsable(e => e.Notation.Judgements.Bar.MaxHeight)
                .AddParsable(e => e.Notation.Judgements.Bar.LaneOffset)
                .AddParsable(e => e.Notation.Judgements.Bar.SharedPosition)
                .AddParsable(e => e.Notation.Judgements.Bar.SharedPositionFeet)
                .AddParsable(e => e.Notation.Judgements.Bar.MinmumAspectRatio)
                .AddParsable(e => e.Notation.Judgements.Bar.Padding)
            )
            .BuildTo(view);
        view.AddBlockHeader("Mania Display Settings");
        view.AddSetting(new DoubleSettingInfo("Scroll Rate Multiplier", Bind(e => e.Mania.ScrollMultiplier)));
        view.AddSetting(new BooleanSettingInfo("Show Judgement Textures", Bind(e => e.Mania.Judgements.Textures)));
        view.AddSetting(new BooleanSettingInfo("Show Judgement Chips", Bind(e => e.Mania.Judgements.Chips)));
        view.AddSetting(new BooleanSettingInfo("Show Judgement Error Numbers", Bind(e => e.Mania.Judgements.ErrorNumbers.Show)));
        view.AddSetting(new BooleanSettingInfo("Show Judgement Error FAST/SLOW Text", Bind(e => e.Mania.Judgements.ErrorNumbers.ShowFastSlow)));
        view.AddSetting(new BooleanSettingInfo("Hide Hit Chips", Bind(e => e.Mania.Judgements.HideHitChips)));
        // if background is null, it has to be set externally for this to do anything
        if (Util.Skin.Mania.Background != null)
        {
            var bind = BindNumber(e => e.Mania.Background.Alpha);
            bind.Description = "Sets how visible the background graphic is in the mania display.\nSet to 0% to hide.\nTo modify the color/pattern of the background graphic, open the skin file directly using the buttons in the top right of the skin settings view.\n"
                + $"{IHasCommand.GetMarkupTooltipNoModify(Command.OpenExternally)}";
            view.AddSetting(new SliderSettingInfo<float>("Background Alpha", bind));
        }

        view.AddBlockHeader("Other Settings");
        view.AddSetting(new EnumSettingInfo<LayoutPreference>("Layout Preference", Bind(e => e.LayoutPreference)));
        // if background is null, it has to be set externally for this to do anything
        if (Util.Skin.SelectorBackground != null)
        {
            var bind = BindNumber(e => e.SelectorBackground.Alpha);
            bind.Description = "Sets how visible the background graphic is for song select.\nSet to 0% to hide.\nTo modify the color/pattern of the background graphic, open the skin file directly using the buttons in the top right of the skin settings view.\n"
                + $"{IHasCommand.GetMarkupTooltipNoModify(Command.OpenExternally)}";
            view.AddSetting(new SliderSettingInfo<float>("Selector Background Alpha", bind));
        }
    }

    public static Bindable<T> Bind<T>(Expression<Func<Skin, T>> path)
    {
        var res = new Bindable<T>(path.Get())
        {
            Description = path.GetDescriptionFromExpression()
        };
        res.BindValueChanged(e => path.SetAndDirty(e.NewValue));
        return res;
    }
    public static BindableNumber<T> BindNumber<T>(Expression<Func<Skin, T>> path) where T : struct, INumber<T>, IMinMaxValue<T>
    {
        var res = new BindableNumber<T>(path.Get())
        {
            Description = path.GetDescriptionFromExpression()
        };
        res.BindValueChanged(e => path.SetAndDirty(e.NewValue));
        return res;
    }
}
