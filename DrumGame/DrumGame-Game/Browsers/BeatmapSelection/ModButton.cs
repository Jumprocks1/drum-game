using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Modifiers;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Browsers.BeatmapSelection;

public class ModButton : CompositeDrawable, IHasCommandInfo, IHasContextMenu
{
    public CommandInfo CommandInfo { get; }
    string IHasMarkupTooltip.MarkupTooltip
    {
        get
        {
            var o = IHasCommandInfo.GetMarkupTooltip(CommandInfo) + "\n" + Modifier.MarkupDescription;
            if (Modifier.CanConfigure)
                o += $"\n\n<brightGreen>This mod can be configured, right click for options</>";
            return o;
        }
    }
    public MenuItem[] ContextMenuItems => ContextMenuBuilder.New(Modifier)
        .Add("Set Hotkey", e => Util.Palette.EditKeybind(CommandInfo))
            .Color(DrumColors.BrightGreen)
        .Add("Configure", e => e.Configure())
            .Hide(!Modifier.CanConfigure)
        .Add("Reset to default configuration", e => e.Reset())
            .Color(DrumColors.WarningText)
            .Hide(!Modifier.CanConfigure)
        .Build();


    public Box Background;
    readonly BeatmapSelectorState State;
    readonly BeatmapModifier Modifier;

    IconButton ConfigureButton;

    public new const float Size = 100;

    public ModButton(BeatmapSelectorState state, BeatmapModifier modifier)
    {
        State = state;
        Modifier = modifier;
        var c = Util.CommandController;
        CommandInfo = c.GetParameterCommand(Command.ToggleMod, modifier);
        Width = Size;
        Height = Size;
        AddInternal(Background = new Box
        {
            RelativeSizeAxes = Axes.Both
        });
        AddInternal(new SpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Font = FrameworkFont.Regular.With(size: 70),
            Text = modifier.Abbreviation
        });
        if (modifier.CanConfigure)
        {
            AddInternal(ConfigureButton = new IconButton(modifier.Configure, FontAwesome.Solid.Cog, 20)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
            });
        }
        State.OnModifiersChange += UpdateDisplay;
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        var has = State.HasModifier(Modifier);
        Background.Colour = has ? DrumColors.Green.MultiplyAlpha(0.4f) : DrumColors.AnsiWhite.MultiplyAlpha(0.1f);

        if (ConfigureButton != null)
        {
            var isDefault = Modifier.IsDefault;
            var baseTooltip = $"<brightGreen>Configure {Modifier.FullName} Mod</>";
            ConfigureButton.MarkupTooltip = isDefault ? baseTooltip :
                        $"{baseTooltip}\n<brightBlue>This mod has some settings changed.</>";
            ConfigureButton.ButtonColor = isDefault ? Colour4.White : DrumColors.BrightBlue;
        };
    }


    protected override bool OnMouseDown(MouseDownEvent e) => true;
    protected override bool OnHover(HoverEvent e)
    {
        this.FadeColour(DrumColors.AnsiWhite.DarkenOrLighten(0.3f), 200);
        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.FadeColour(DrumColors.AnsiWhite, 200);
        base.OnHoverLost(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        State.OnModifiersChange -= UpdateDisplay;
        base.Dispose(isDisposing);
    }
}