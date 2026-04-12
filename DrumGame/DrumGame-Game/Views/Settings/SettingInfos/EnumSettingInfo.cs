using System;
using System.Linq;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Stores;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using DrumGame.Game.Interfaces;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class SkinSetting : SettingInfo
{
    public SkinSetting(string label) : base(label)
    {
    }
    record Pair(string Skin, string Name) : IFilterable { }
    public override void Render(SettingControl control)
    {
        var binding = Util.ConfigManager.GetBindable<string>(DrumGameSetting.Skin);
        // bit sketchy because the "Default" skin cannot be edited as it has no file
        // if a user tries to edit the default skin, we will create `default.json` and set the skin to "default" instead of null
        // after this occurs, we probably don't want to show "Default" in this skin list anymore since it may be confusing to show 2 defaults
        var def = new Pair(null, "Default");
        var skins = SkinManager.ListSkinsWithNames();
        var options = skins
            .Where(e => e.skin != SkinManager.DefaultSkinFilename).Select(e => new Pair(e.skin, $"{e.name} ({e.skin})"))
            .Prepend(def);
        var value = binding.Value;
        var autocomplete = new Autocomplete<Pair>()
        {
            Options = options,
            Width = 300,
            Height = Height,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SettingControl.SideMargin,
            CommittedTarget = string.IsNullOrWhiteSpace(value) ? def : options.FirstOrDefault(e => e.Skin == value),
            ClearOnFocus = true
        };
        // technically if the user hits cancel on the popup from this, the skin won't be changed but the dropdown will still be updated
        // not too worried about it, but technically a bug
        // would have to change the autocomplete to only change itself once the target bindable actually updated, but that's a bit complicated
        // also, when changing skin scroll rate field doesn't update to new value. binding still works though
        autocomplete.OnSelect += option => SkinManager.TryChangeSkin(option.Skin);
        var editButton = new IconButton(() =>
        {
            var source = Util.Skin.Source;
            if (source == null)
                Util.CommandController.ActivateCommand(Command.ExportCurrentSkin);
            else
                Util.RevealInFileExplorer(source);
            // make sure we are watching the skin after we open it
            // this makes it easier to make quick changes without having to manually reload
            SkinManager.StartHotWatcher();
        }, FontAwesome.Solid.FolderOpen, Height)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = autocomplete.X - autocomplete.Width - 5 * 2 - Height,
            MarkupTooltip = "<command>Reveal Skin in File Explorer</>\n\nThis will highlight the current skin in your system file explorer.\nYou can edit the skin by opening the file in a text editor.\nI recommend using an editor with JSON Schema support (VS Code)."
        };
        control.Add(editButton);
        control.Add(new IconButton(SkinSettingsView.Open, FontAwesome.Solid.Cog, Height)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = autocomplete.X - autocomplete.Width - 5,
            MarkupTooltip = $"{IHasCommand.GetMarkupTooltipIgnoreUnbound(Command.OpenSkinSettings)}\n\nOnly some skin options are available for edit in-game\nFor more options, edit the skin file directly with a text editor such as VSCode."
        });
        control.Add(autocomplete);
    }
}

public class EnumSettingInfo<T> : SettingInfo where T : struct, Enum
{
    public override string Description => Binding.Description;
    public Bindable<T> Binding;
    public EnumSettingInfo(string label, Bindable<T> binding) : base(label)
    {
        // technically should bind to external changes for this bindable
        // for example, users can change fullscreen with F11
        Binding = binding;
    }
    public Func<T, string> GetLabel;
    public override void Render(SettingControl control)
    {
        control.Add(new EnumAutocomplete<T>(Binding, GetLabel)
        {
            Width = 300,
            Height = Height,
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -SettingControl.SideMargin
        });
    }
}