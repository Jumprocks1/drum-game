using System.Linq;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Modals;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Views.Settings;

public class SkinSettingsView : SettingsViewBase
{
    public static void Open()
    {
        Util.Palette.CloseAll();
        Util.Palette.OpenSkinSettings();
    }

    public SkinSettingsView()
    {
        if (Util.Skin.Source == null)
        {
            if (SkinManager.EnsureDefaultSkinExists())
            {
                SkinManager.ChangeSkinTo(SkinManager.DefaultSkinFilename);
                // this happens if the skin was already set to default.json, but on initial game load it was missing
                // the above call won't do anything since the target skin was already set to default
                // since we just created the skin file in the above call, we need to reload
                if (Util.Skin.Source == null)
                    SkinManager.ReloadSkin();
            }
        }
        Skin = Util.Skin;
        Util.CommandController.RegisterHandlers(this);
    }

    readonly Skin Skin;

    public override string Title => $"Skin Settings - Editing {Skin.Name}";

    [BackgroundDependencyLoader]
    private void load()
    {
        Inner.Add(new CommandIconButton(Command.RevealInFileExplorer, FontAwesome.Solid.FolderOpen, 40)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = CommandPalette.Margin,
            X = -40 - CommandPalette.Margin * 2,
        });
        Inner.Add(new CommandIconButton(Command.OpenExternally, FontAwesome.Regular.Edit, 40)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = CommandPalette.Margin,
            X = -CommandPalette.Margin
        });
        Inner.Add(new DrumButton
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            Width = 100,
            X = -CommandPalette.Margin,
            Text = "Save",
            Height = FooterButtonHeight,
            Y = -CommandPalette.Margin,
            // close will save in the dispose method
            Action = () =>
            {
                // some fields only save on focus drop
                GetContainingFocusManager()?.ChangeFocus(null);
                Util.Palette.GetModal<OverlayModal<SkinSettingsView>>()?.Close();
            }
        });
        Inner.Add(new DrumButton
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            X = -100 - CommandPalette.Margin * 2,
            Width = 150,
            Text = "Discard Changes",
            Height = FooterButtonHeight,
            Y = -CommandPalette.Margin,
            Action = () =>
            {
                Util.Skin.DirtyPaths?.Clear();
                SkinManager.ReloadSkin(); // this will reload from disk, discarding changes
                Util.Palette.GetModal<OverlayModal<SkinSettingsView>>()?.Close();
            }
        });
    }

    const float FooterButtonHeight = 30f;
    public override float FooterSize => FooterButtonHeight + CommandPalette.Margin * 2;

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        if (Util.Skin.Dirty)
        {
            SkinManager.SavePartialSkin(Util.Skin); // only saves if dirty
            SkinManager.ReloadSkin(true); // this resets/calls LoadDefaults, which is important in some cases
        }
        base.Dispose(isDisposing);
    }

    [CommandHandler]
    public void RevealInFileExplorer()
    {
        Util.RevealInFileExplorer(Skin.Source);
        SkinManager.StartHotWatcher();
    }
    [CommandHandler]
    public void OpenExternally()
    {
        Util.Host.OpenFileExternally(Skin.Source);
        SkinManager.StartHotWatcher();
    }

    protected override void RenderSettings() => SkinSettingsList.RenderSettings(this);
}
