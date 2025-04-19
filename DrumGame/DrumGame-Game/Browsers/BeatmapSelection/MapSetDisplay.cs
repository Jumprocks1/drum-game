using System.Collections.Generic;
using System.Linq;
using System.Text;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Components;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Browsers.BeatmapSelection;

public class MapSetDisplay : CompositeDrawable
{
    public const string MultipleDifficultiesTag = "multiple-difficulties";
    public MapSetDisplay(IReadOnlyList<MapSetEntry> mapSet, int selectedIndex)
    {
        var y = 0f;
        var builder = new StringBuilder();

        var selected = mapSet[selectedIndex].Metadata;


        var list = mapSet.Select(e => e.Metadata)
            .OrderByDescending(e => e.DtxLevel)
            .ThenByDescending(e => e.Difficulty).ToList();

        for (var i = 0; i < list.Count; i++)
        {
            var meta = list[i];
            var dtxLevel = meta.DtxLevel;
            builder.Append(meta.DifficultyString);
            if (!string.IsNullOrWhiteSpace(dtxLevel)) builder.Append(" - " + dtxLevel);
            if (meta.Tags != null && meta.Tags.Contains(MultipleDifficultiesTag)) builder.Append(" (Multiple)");
            DrumButton button = null;
            button = new DrumButton
            {
                Text = builder.ToString(),
                Y = y,
                AutoSize = true,
                Action = () =>
                {
                    if (BeatmapCarousel.Current?.JumpToMap(meta) == false)
                        button.MarkupTooltip = "This difficulty is not currently visible due to the active filters";
                    else button.MarkupTooltip = null;
                },
                BackgroundColour = meta == selected ? DrumColors.DarkGreen : DrumColors.DarkActiveButton
            };
            AddInternal(button);
            y += button.Height;
            builder.Clear();
        }

        AutoSizeAxes = Axes.Both;
    }
}