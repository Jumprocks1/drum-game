using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Numerics;
using DrumGame.Game.Commands;
using DrumGame.Game.Modals;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using DrumGame.Game.Views.Settings.SettingInfos;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Views.Settings;

public interface IHandleSettingInfo
{
    void AddSetting(SettingInfo setting);
}

public class SettingsListBuilder
{
    List<SettingInfo> settings = new();
    public SettingsListBuilder Modify(Action<SettingsListBuilder> modify)
    {
        modify?.Invoke(this);
        return this;
    }
    public SettingsListBuilder Tooltip(string markupTooltip)
    {
        settings[^1].Tooltip = markupTooltip;
        return this;
    }
    public SettingsListBuilder AddSubButton(string label, string description, string buttonText, Action<SettingsListBuilder> build)
    {
        Add(new ButtonSettingInfo(label, description, buttonText)
        {
            Action = _ => OpenSubmenu(label, build)
        });
        return this;
    }
    static void OpenSubmenu(string title, Action<SettingsListBuilder> build)
    {
        var modal = Util.Palette.Request(new RequestConfig
        {
            Title = title,
            CommitText = null
        });
        var subBuilder = new SettingsListBuilder();
        build(subBuilder);
        var subSettings = subBuilder.settings;
        var even = true; // even fields have lighter background
        var depth = 0;
        var y = 0f;
        foreach (var e in subSettings)
        {
            var control = new SettingControl(e, even)
            {
                Y = y,
                Depth = depth++
            };
            modal.Add(control);
            y += control.Height;
            even = !even;
        }
    }
    public SettingsListBuilder AdvancedMenu(string title, Action<SettingsListBuilder> build, IconUsage? icon = null)
        => AddIconButton(() => OpenSubmenu(title, build), icon ?? FontAwesome.Solid.Cog, $"<command>Open {title}</>");
    public SettingsListBuilder AddCommandIconButton(Command command, IconUsage icon)
    {
        var target = settings[^1];
        target.AfterRender += control => control.AddCommandIconButton(command, icon);
        return this;
    }

    public SettingsListBuilder AddIconButton(Action action, IconUsage icon, string tooltip)
    {
        var target = settings[^1];
        target.AfterRender += control => control.AddIconButton(action, icon, tooltip);
        return this;
    }

    static Bindable<T> Bind<T>(Expression<Func<Skin, T>> path)
    {
        var res = new Bindable<T>(path.Get())
        {
            Description = path.GetDescriptionFromExpression()
        };
        res.BindValueChanged(e => path.SetAndDirty(e.NewValue));
        return res;
    }
    public SettingsListBuilder AddEnum<T>(Expression<Func<Skin, T>> path) where T : struct, Enum
        => Add(new EnumSettingInfo<T>(path.GetName(), Bind(path)));
    public SettingsListBuilder AddParsable<T>(Expression<Func<Skin, T>> path) where T : IParsable<T>
        => Add(new ParsableSettingInfo<T>(path.GetName(), Bind(path)));
    public SettingsListBuilder AddNullableParsable<T>(Expression<Func<Skin, T?>> path) where T : struct, IParsable<T>
        => Add(new NullableParsableSettingInfo<T>(path.GetName(), Bind(path)));
    public SettingsListBuilder Add(string label, Expression<Func<Skin, Colour4>> path)
        => Add(new ColorSettingInfo(label, Bind(path)));
    public SettingsListBuilder Add(Expression<Func<Skin, Colour4>> path)
        => Add(new ColorSettingInfo(path.GetName(), Bind(path)));
    public SettingsListBuilder Add(SettingInfo settingInfo)
    {
        settings.Add(settingInfo);
        return this;
    }
    public void BuildTo(IHandleSettingInfo view)
    {
        foreach (var e in settings) view.AddSetting(e);
        settings = null;
    }
}