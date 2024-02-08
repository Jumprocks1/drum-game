using System;
using System.Linq;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.Skins;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class WindowModeSetting : EnumSettingInfo<WindowMode>
{
    public WindowModeSetting(Bindable<WindowMode> binding) : base("Window Mode", binding) { }
    public override void Render(SettingControl control)
    {
        control.Command = Commands.Command.ToggleFullscreen;
        base.Render(control);
    }
}
public class FrameSyncSetting : EnumSettingInfo<FrameSync>
{
    public FrameSyncSetting(Bindable<FrameSync> binding) : base("Frame Sync", binding) { }
    public override void Render(SettingControl control)
    {
        control.Command = Commands.Command.CycleFrameSync;
        base.Render(control);
    }
}
public class SkinSetting : SettingInfo
{
    public SkinSetting(string label) : base(label)
    {
    }
    public override void Render(SettingControl control)
    {
        var binding = Util.ConfigManager.GetBindable<string>(DrumGameSetting.Skin);
        var def = new BasicAutocompleteOption("Default");
        var options = SkinManager.ListSkins().Select(e => new BasicAutocompleteOption(e)).Prepend(def);
        var value = binding.Value;
        var autocomplete = new Autocomplete()
        {
            Options = options,
            Width = 300,
            Height = Height,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SettingControl.SideMargin,
            CommittedTarget = string.IsNullOrWhiteSpace(value) ? def : options.FirstOrDefault(e => e.Name == value),
            ClearOnFocus = true
        };
        autocomplete.OnSelect += option =>
        {
            if (option == def)
                binding.Value = null;
            else
                binding.Value = option.Name;
        };
        var editButton = new IconButton(() =>
        {
            var source = Util.Skin.Source;
            if (source == null)
                Util.CommandController.ActivateCommand(Command.ExportCurrentSkin);
            else
                Util.RevealInFileExplorer(source);
            // make sure we are watching the skin after we open it
            // this makes it easier to make quick changes without having to manually reload
            SkinManager.SetHotWatcher(Util.Skin);
        }, FontAwesome.Solid.Edit, Height)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = autocomplete.X - autocomplete.Width - 5,
            MarkupTooltip = "<command>Reveal Skin in File Explorer</>\n\nThis will highlight the current skin in your system file explorer.\nYou can edit the skin by opening the file in a text editor.\nI recommend using an editor with JSON Schema support (VS Code)."
        };
        control.Add(editButton);
        control.Add(autocomplete);
    }
}

public class EnumSettingInfo<T> : SettingInfo where T : struct, Enum
{
    public Bindable<T> Binding;
    public EnumSettingInfo(string label, Bindable<T> binding) : base(label)
    {
        // technically should bind to external changes for this bindable
        // for example, users can change fullscreen with F11
        Binding = binding;
    }
    public override void Render(SettingControl control)
    {
        control.Add(new EnumAutocomplete<T>(Binding)
        {
            Width = 300,
            Height = Height,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SettingControl.SideMargin,
        });
    }
}