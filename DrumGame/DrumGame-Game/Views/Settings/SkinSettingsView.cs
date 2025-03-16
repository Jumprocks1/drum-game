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

public class SkinSettingsView : CompositeDrawable, IHandleSettingInfo
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
    }

    readonly Skin Skin;
    DrumScrollContainer ScrollContainer;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.Both;
        var inner = new ModalForeground(Axes.None)
        {
            Width = 800,
            Height = 0.9f,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Y
        };
        var headerSize = 90;
        var footerButtonHeight = 30;
        var footerSize = footerButtonHeight + CommandPalette.Margin * 2;
        inner.Add(new SpriteText
        {
            Text = $"Skin Settings - Editing {Skin.Name}",
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Font = FrameworkFont.Regular.With(size: 40),
            Y = 5
        });
        inner.Add(new CommandIconButton(Command.RevealInFileExplorer, FontAwesome.Solid.FolderOpen, 40)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = CommandPalette.Margin,
            X = -40 - CommandPalette.Margin * 2,
        });
        inner.Add(new CommandIconButton(Command.OpenExternally, FontAwesome.Regular.Edit, 40)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = CommandPalette.Margin,
            X = -CommandPalette.Margin
        });
        inner.Add(new DrumButton
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            Width = 100,
            X = -CommandPalette.Margin,
            Text = "Save",
            Height = footerButtonHeight,
            Y = -CommandPalette.Margin,
            // close will save in the dispose method
            Action = () =>
            {
                // some fields only save on focus drop
                GetContainingFocusManager()?.ChangeFocus(null);
                Util.Palette.GetModal<OverlayModal<SkinSettingsView>>()?.Close();
            }
        });
        inner.Add(new DrumButton
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            X = -100 - CommandPalette.Margin * 2,
            Width = 150,
            Text = "Discard Changes",
            Height = footerButtonHeight,
            Y = -CommandPalette.Margin,
            Action = () =>
            {
                Util.Skin.DirtyPaths?.Clear();
                SkinManager.ReloadSkin(); // this will reload from disk, discarding changes
                Util.Palette.GetModal<OverlayModal<SkinSettingsView>>()?.Close();
            }
        });

        inner.Add(new Container
        {
            Child = ScrollContainer = new DrumScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
            },
            Padding = new MarginPadding { Top = headerSize, Bottom = footerSize },
            RelativeSizeAxes = Axes.Both,
        });


        SkinSettingsList.RenderSettings(this);

        AddInternal(inner);
        Util.CommandController.RegisterHandlers(this);
    }

    bool Even = true; // even fields have lighter background
    int NextDepth;
    float NextY;
    public void AddBlockHeader(string text)
    {
        var blockHeaderFontSize = 30;
        if (NextY != 0) NextY += CommandPalette.Margin; // add extra space between header and previous section
        var blockHeader = new SpriteText
        {
            Text = text,
            Height = blockHeaderFontSize + CommandPalette.Margin / 2,
            Font = FrameworkFont.Regular.With(size: blockHeaderFontSize),
            X = CommandPalette.Margin,
            Y = NextY,
        };
        ScrollContainer.Add(blockHeader);
        NextY += blockHeader.Height;
        Even = true;
    }
    public void AddSetting(SettingInfo setting)
    {
        var control = new SettingControl(setting, Even)
        {
            Y = NextY,
            Depth = NextDepth++
        };
        ScrollContainer.Add(control);
        NextY += control.Height;
        Even = !Even;
    }

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

    [CommandHandler] public void RevealInFileExplorer() => Util.RevealInFileExplorer(Skin.Source);
    [CommandHandler] public void OpenExternally() => Util.Host.OpenFileExternally(Skin.Source);
}
