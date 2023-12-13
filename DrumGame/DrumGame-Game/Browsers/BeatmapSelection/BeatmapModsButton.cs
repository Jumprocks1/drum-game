using System.Linq;
using DrumGame.Game.Components;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Browsers.BeatmapSelection;

public class BeatmapModsButton : CommandButton, IHasCustomTooltip
{
    readonly BeatmapSelector Selector;
    BeatmapSelectorState State => Selector.State;
    public BeatmapModsButton(BeatmapSelector selector) : base(Commands.Command.SelectMods)
    {
        Selector = selector;

        Anchor = Anchor.BottomLeft;
        Origin = Anchor.BottomLeft;
        X = 250;
        Y = -45;
        Text = "Mods";
        Height = 35;
        Width = 150;
        Action = () => { };

        AddInternal(new SpriteIcon
        {
            Height = 25,
            Width = 25,
            Icon = FontAwesome.Solid.Wrench,
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            Alpha = 1,
            Depth = -1,
            X = -5
        });
    }


    [BackgroundDependencyLoader]
    private void load()
    {
        UpdateColor();
        State.OnModifiersChange += ModifiersChanged;
    }

    protected override void Dispose(bool isDisposing)
    {
        State.OnModifiersChange -= ModifiersChanged;
        base.Dispose(isDisposing);
    }

    MarkupText markupText;
    void ModifiersChanged()
    {
        UpdateColor();
    }

    void UpdateColor()
    {
        var hasMod = State.HasModifiers;
        BackgroundColour = hasMod ? DrumColors.DarkGreen : DrumColors.DarkBackground;
        SpriteText.Y = hasMod ? -5 : 0;


        if (hasMod)
        {
            if (markupText == null)
            {
                AddInternal(markupText = new MarkupText(e => e.Font = FrameworkFont.Condensed.With(size: 14))
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -1
                });
            }
            markupText.Clear();
            var first = true;
            foreach (var mod in State.Modifiers)
            {
                if (!first) markupText.AddText(" ");
                markupText.AddText(mod.AbbreviationMarkup);
                first = false;
            }
            markupText.Alpha = 1;
        }
        else
        {
            if (markupText != null)
                markupText.Alpha = 0;
        }
    }

    string CommandTooltip => IHasCommand.GetMarkupTooltip(CommandInfo);

    public string MarkupTooltip
    {
        get
        {
            if (State.HasModifiers)
            {
                return CommandTooltip + "\n" + string.Join('\n', State.Modifiers.Select(e => e.MarkupDisplay));
            }
            else
            {
                return CommandTooltip + "\n" + "No modifiers currently selected.";
            }
        }
    }

    public object TooltipContent => new MarkupTooltipData(MarkupTooltip);

    public ITooltip GetCustomTooltip() => null;
}