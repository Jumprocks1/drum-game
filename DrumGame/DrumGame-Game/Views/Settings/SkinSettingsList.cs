using System;
using System.Linq.Expressions;
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
        view.AddSetting(new ColorSettingInfo("Notation Color", Bind(e => e.Notation.NotationColor)));
        view.AddSetting(new ColorSettingInfo("Note Color", Bind(e => e.Notation.NoteColor)));
        view.AddSetting(new ColorSettingInfo("Playfield Background Color", Bind(e => e.Notation.PlayfieldBackground)));
        view.AddSetting(new ColorSettingInfo("Small Tom Color", Bind(e => e.Notation.Channels[DrumChannel.SmallTom].Color)));
        view.AddSetting(new ColorSettingInfo("Medium Tom Color", Bind(e => e.Notation.Channels[DrumChannel.MediumTom].Color)));
        view.AddSetting(new ColorSettingInfo("Large Tom Color", Bind(e => e.Notation.Channels[DrumChannel.LargeTom].Color)));
    }

    public static SkinSettingBindable<T> Bind<T>(Expression<Func<Skin, T>> path) => new(path);
    public class SkinSettingBindable<T> : Bindable<T>
    {
        readonly Expression<Func<Skin, T>> Path;
        public SkinSettingBindable(Expression<Func<Skin, T>> path) : base(path.Get())
        {
            Path = path;
            Description = path.GetDescriptionFromExpression();
            BindValueChanged(e => Path.SetAndDirty(e.NewValue));
        }
    }
}
