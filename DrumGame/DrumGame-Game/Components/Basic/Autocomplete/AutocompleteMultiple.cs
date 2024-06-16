using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Components.Basic.Autocomplete;

public class AutocompleteMultiple<T> : Autocomplete<T> where T : class, IFilterable
{
    public new Action<List<T>> OnSelect;
    List<T> Selected = new();
    List<Drawable> SelectedOptions = new();

    static float DisplayHeight = 25;

    void UpdateDisplay()
    {
        var listHeight = Selected.Count * DisplayHeight;
        Height = SearchHeight + Margin * 2 + listHeight;
        backgroundContainer.Y = listHeight;
    }

    protected override void UpdateFilter()
    {
        base.UpdateFilter();
        FilteredOptions.RemoveAll(e => Selected.Contains(e));
    }

    protected override void HardCommit() => OnSelect?.Invoke(Selected);

    protected override void SelectOption(T option)
    {
        target = option;
        Input.Current.Value = "";
        if (!Selected.Contains(option))
        {
            Selected.Add(option);
            var newDisplay = new SelectedOptionDisplay(option, this)
            {
                Y = SelectedOptions.Count * DisplayHeight,
            };
            SelectedOptions.Add(newDisplay);
            AddInternal(newDisplay);
            UpdateDisplay();
            UpdateFilter();
            TargetI = 0;
        }
        // pull focus back
        Schedule(() => GetContainingFocusManager().ChangeFocus(Input));
    }

    public void Remove(T option)
    {
        var index = Selected.IndexOf(option);
        if (index != -1)
        {
            loadedFilterString = null;
            Selected.RemoveAt(index);
            var o = SelectedOptions[index];
            RemoveInternal(o, true);
            SelectedOptions.RemoveAt(index);
            for (var i = index; i < SelectedOptions.Count; i++)
                SelectedOptions[i].Y -= DisplayHeight;
            UpdateDisplay();
        }
    }

    class SelectedOptionDisplay : CompositeDrawable
    {
        public SelectedOptionDisplay(T option, AutocompleteMultiple<T> autocomplete)
        {
            Height = DisplayHeight;
            RelativeSizeAxes = Axes.X;
            AddInternal(new SpriteText
            {
                Padding = new MarginPadding { Left = 8 },
                Font = FrameworkFont.Regular,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = option.Name
            });
            AddInternal(new IconButton(() => autocomplete.Remove(option), FontAwesome.Solid.Times, DisplayHeight * 0.75f)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
            });
        }
    }
}