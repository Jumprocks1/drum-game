using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Numerics;
using DrumGame.Game.Commands;
using DrumGame.Game.Components.Basic;
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
    void AddBlockHeader(string text);
}

public class SettingsListBuilder
{
    List<SettingInfo> settings = new();
    List<(string Header, int Index)> blockHeaders = new();
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
    public static RequestModal OpenSettingsMenu(string title, Action<SettingsListBuilder> builder, string description = null) => OpenSubmenu(title, builder, description);
    static RequestModal OpenSubmenu(string title, Action<SettingsListBuilder> build, string description = null)
    {
        var modal = Util.Palette.Request(new RequestConfig
        {
            Title = title,
            CommitText = null,
            MarkupDescription = description
        });
        var scrollContainer = new DrumScrollContainer
        {
            RelativeSizeAxes = Axes.X,
        };
        modal.Add(scrollContainer);
        var subBuilder = new SettingsListBuilder();
        build(subBuilder);
        var subSettings = subBuilder.settings;
        var even = true; // even fields have lighter background
        var depth = 0;
        var y = 0f;
        foreach (var e in subSettings)
        {
            var control = new SettingControl(e)
            {
                Y = y,
                Depth = depth++
            };
            control.UpdateDisplay(even);
            scrollContainer.Add(control);
            y += control.Height;
            even = !even;
        }
        scrollContainer.Height = Math.Min(y, 700);
        return modal;
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
    public SettingsListBuilder AddParsable<T>(string label, Expression<Func<Skin, T>> path) where T : IParsable<T>
        => Add(new ParsableSettingInfo<T>(label, Bind(path)));
    public SettingsListBuilder AddNullableParsable<T>(Expression<Func<Skin, T?>> path) where T : struct, IParsable<T>
        => Add(new NullableParsableSettingInfo<T>(path.GetName(), Bind(path)));
    public SettingsListBuilder Add(Expression<Func<Skin, bool>> path, string label = null)
        => Add(new BooleanSettingInfo(label ?? path.GetName(), Bind(path)));
    public SettingsListBuilder Add(string label, Expression<Func<Skin, Colour4>> path)
        => Add(new ColorSettingInfo(label, Bind(path)));
    public SettingsListBuilder Add(Expression<Func<Skin, Colour4>> path)
        => Add(new ColorSettingInfo(path.GetName(), Bind(path)));
    public SettingsListBuilder AddBlockHeader(string name)
    {
        blockHeaders.Add((name, settings.Count));
        return this;
    }
    public SettingsListBuilder AddTags(string tags)
    {
        var target = settings[^1];
        target.Tags = tags;
        return this;
    }
    public SettingsListBuilder Add(SettingInfo settingInfo)
    {
        settings.Add(settingInfo);
        return this;
    }
    public void BuildTo(IHandleSettingInfo view)
    {
        var j = 0;
        for (var i = 0; i < settings.Count; i++)
        {
            if (j < blockHeaders.Count && blockHeaders[j].Index == i)
            {
                view.AddBlockHeader(blockHeaders[j].Header);
                j += 1;
            }
            var e = settings[i];
            view.AddSetting(e);
        }
        settings = null;
    }
}