using System;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Modals;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;

namespace DrumGame.Game.Views.Settings;

[Cached]
public class SettingsView : SettingsViewBase
{
    public override string Title => "Settings";

    [BackgroundDependencyLoader]
    private void load()
    {
        var y = 5f;
        var x = -10f;
        const float spacing = 6;
        Inner.Add(new CommandIconButton(Command.EditKeybinds, FontAwesome.Regular.Keyboard, 40)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = y,
            X = x
        });
        x -= 40 + spacing;
        Inner.Add(new CommandIconButton(Command.RevealInFileExplorer, FontAwesome.Solid.FolderOpen, 32)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = x,
            Y = 4 + y
        });
        x -= 32 + spacing;
        Inner.Add(new CommandIconButton(Command.OpenExternally, FontAwesome.Solid.FileAlt, 24)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = x,
            Y = 8 + y
        });
    }

    protected override void RenderSettings() => SettingsList.RenderSettings(this, Util.ConfigManager, Util.DrumGame.FrameworkConfigManager);
    [CommandHandler] public void RevealInFileExplorer() => Util.ConfigManager.RevealInFileExplorer();
    [CommandHandler] public void OpenExternally() => Util.ConfigManager.OpenExternally();
}
