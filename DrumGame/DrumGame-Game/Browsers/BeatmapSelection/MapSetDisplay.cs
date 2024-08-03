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
    public MapSetDisplay(IReadOnlyList<MapSetEntry> mapSet, int selectedIndex)
    {
        var y = 0f;
        var builder = new StringBuilder();

        var selected = mapSet[selectedIndex].Metadata;


        List<(BeatmapMetadata Meta, string DtxLevel)> list = mapSet.Select(e => (e.Metadata, e.Metadata.DtxLevel))
            .OrderBy(e => e.DtxLevel).ToList();

        for (var i = 0; i < list.Count; i++)
        {
            var (Meta, DtxLevel) = list[i];
            builder.Append(Meta.DifficultyString);
            if (DtxLevel != null) builder.Append(" - " + DtxLevel);
            DrumButton button = null;
            button = new DrumButton
            {
                Text = builder.ToString(),
                Y = y,
                AutoSize = true,
                Action = () =>
                {
                    if (BeatmapCarousel.Current?.JumpToMap(Meta) == false)
                        button.MarkupTooltip = "This difficulty is not currently visible due to the active filters";
                    else button.MarkupTooltip = null;
                },
                BackgroundColour = Meta == selected ? DrumColors.DarkGreen : DrumColors.DarkActiveButton
            };
            AddInternal(button);
            y += button.Height;
            builder.Clear();
        }

        AutoSizeAxes = Axes.Both;
    }
}