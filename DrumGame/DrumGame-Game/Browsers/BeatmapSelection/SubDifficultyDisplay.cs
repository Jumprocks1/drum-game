using System.Collections.Generic;
using System.Linq;
using System.Text;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Components;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace DrumGame.Game.Browsers.BeatmapSelection;

// copy pasted from MapSetDisplay
public class SubDifficultyDisplay : CompositeDrawable
{
    List<BeatmapDifficultyDefinition> Difficulties;
    [Resolved] BeatmapSelector Selector { get; set; }
    BeatmapSelectorState State => Selector.State;
    public SubDifficultyDisplay(List<BeatmapDifficultyDefinition> difficulties)
    {
        Difficulties = difficulties;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        var y = 0f;
        for (var i = 0; i < Difficulties.Count; i++)
        {
            var diff = Difficulties[i];
            DrumButton button = null;
            button = new DrumButton
            {
                Text = diff.Name,
                Y = y,
                AutoSize = true,
                Action = () =>
                {
                    State.PreferredSubDifficulty = diff.Name;
                },
            };
            AddInternal(button);
            y += button.Height;
        }

        var preferred = State.PreferredSubDifficulty;
        if (!Difficulties.Any(e => e.Name == preferred))
        {
            var def = Difficulties.FirstOrDefault(e => e.Default);
            if (def != null)
                State.PreferredSubDifficulty = def.Name;
        }

        State.OnDiffChange += UpdateColors;
        UpdateColors();

        AutoSizeAxes = Axes.Both;
    }

    void UpdateColors()
    {
        var preferred = State.PreferredSubDifficulty;
        var found = Difficulties.Find(e => e.Name == preferred);
        var selected = found?.Name;
        foreach (var child in InternalChildren.OfType<DrumButton>())
        {
            child.BackgroundColour = child.Text == selected ? DrumColors.DarkGreen : DrumColors.DarkActiveButton;
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        State.OnDiffChange -= UpdateColors;
        base.Dispose(isDisposing);
    }
}