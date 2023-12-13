using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Modifiers;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Browsers.BeatmapSelection;

public class ModButton : CompositeDrawable, IHasCommandInfo
{
    public bool AllowClick => true;
    public CommandInfo CommandInfo { get; }
    string IHasMarkupTooltip.MarkupTooltip => IHasCommandInfo.GetMarkupTooltip(CommandInfo) + "\n" + Modifier.MarkupDescription;

    public Box Background;
    readonly BeatmapSelectorState State;
    readonly BeatmapModifier Modifier;

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
        State.OnModifiersChange += UpdateColor;
        UpdateColor();
    }

    public void UpdateColor()
    {
        var has = State.HasModifier(Modifier);
        Background.Colour = has ? DrumColors.Green.MultiplyAlpha(0.4f) : DrumColors.AnsiWhite.MultiplyAlpha(0.1f);
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
        State.OnModifiersChange -= UpdateColor;
        base.Dispose(isDisposing);
    }
}