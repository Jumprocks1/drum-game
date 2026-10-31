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

        var includeTitle = false;
        var titleRoot = 0;
        if (list.Count > 1)
        {
            if (list[0].DtxLevel == "" && list.All(e => e.Difficulty == list[0].Difficulty))
            {
                includeTitle = true;
                var first = list[0].Title;
                titleRoot = first.Length;
                for (var i = 1; i < list.Count; i++)
                {
                    var current = list[i].Title;
                    if (current.Length < titleRoot) titleRoot = current.Length;
                    for (var j = 0; j < titleRoot; j++)
                    {
                        if (first[j] != current[j])
                        {
                            titleRoot = j;
                            break;
                        }
                    }
                    if (titleRoot == 0) break;
                }
            }
        }

        for (var i = 0; i < list.Count; i++)
        {
            var meta = list[i];
            if (includeTitle)
            {
                builder.Append(meta.Title[titleRoot..]);
            }
            else
            {
                var dtxLevel = meta.DtxLevel;
                builder.Append(meta.DifficultyString);
                if (!string.IsNullOrWhiteSpace(dtxLevel)) builder.Append(" - " + dtxLevel);
                if (meta.Tags != null && meta.Tags.Contains(MultipleDifficultiesTag)) builder.Append(" (Multiple)");
            }
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