using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osuTK.Input;


namespace DrumGame.Game.Components.Basic.Autocomplete;

// this will eventually be generic T so we can pass in Command infos for example
public interface IFilterable
{
    bool MatchesFilter(Filter filter) => filter.MatchesString(Name);
    string Name { get; }
    public string MarkupTooltip => null;
}
public class Filter
{
    public readonly string Text;
    public readonly string[] Split;
    public bool MatchesString(string s) => Split.All(e => s.Contains(e, StringComparison.OrdinalIgnoreCase));
    public Filter(string text)
    {
        MatchesAll = string.IsNullOrWhiteSpace(text);
        if (!MatchesAll)
        {
            Text = text;
            Split = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }
    }
    public readonly bool MatchesAll;
}
public class Autocomplete<T> : CompositeDrawable, IAcceptFocus where T : class, IFilterable
{
    public bool ClearOnFocus = false;
    bool _open = false;
    public bool Open
    {
        get => _open; set
        {
            if (_open == value) return;
            if (ClearOnFocus && value) Input.Current.Value = "";
            _open = value;
            if (value)
                Util.GetParent<DrumPopoverContainer>(this).Popover(Popover, ButtonContainer, true);
            else
            {
                Input.Current.Value = _committedTargeted?.Name ?? "";
                Util.GetParent<DrumPopoverContainer>(this)?.ClosePopover(Popover);
            }
            UpdateFilterAndDisplay();
            Util.ResetHover(this); // force tooltip to recalculate, since this changes our OnHover handle boolean
        }
    }
    protected const float SearchHeight = 30;
    Box Background { get; }

    T _committedTargeted;
    public T CommittedTarget
    {
        get => _committedTargeted; set
        {
            if (_committedTargeted == value) return;
            Target = _committedTargeted = value;
            if (!_open)
                Input.Current.Value = value?.Name ?? "";
        }
    }

    public T Target
    {
        get => target; set
        {
            if (target == value) return;
            if (Open)
            {
                var index = FilteredOptions.IndexOf(value);
                if (index >= 0)
                {
                    TargetI = index;
                    UpdateOptionDisplay();
                }
            }
            else
            {
                target = value;
            }
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        Popover.Dispose();
        base.Dispose(isDisposing);
    }

    protected T target; // target for keyboard etc.
    List<T> _options;
    public IEnumerable<T> Options
    {
        get => _options; set
        {
            if (_options == value) return;
            loadedFilterString = null;
            _options = value.AsList();
            UpdateFilterAndDisplay();
        }
    }
    int? _targetI;
    protected int? TargetI
    {
        get => _targetI; set
        {
            if (FilteredOptions.Count == 0) { _targetI = null; }
            else
            {
                _targetI = value?.Mod(FilteredOptions.Count);
            }
            target = _targetI.HasValue ? FilteredOptions[_targetI.Value] : null;
        }
    }
    protected List<T> FilteredOptions = new();

    public Action<bool> OnFocusChange;

    public AutocompleteInput Input;
    DrumScrollContainer ScrollContainer;
    Container Popover;
    Container ButtonContainer;
    const int DisplayCount = 12;
    protected virtual void SelectOption(T option)
    {
        CommittedTarget = option;
        OnSelect?.Invoke(option);
        Open = false;
    }
    public Action<T> OnSelect;
    void UpdateOptionDisplay()
    {
        if (!Open) return;
        // This is expensive but who cares. Can fix this when we add virtualization
        ScrollContainer.Clear();
        AutocompleteOption targetButton = null;
        var y = 0f;
        for (var i = 0; i < FilteredOptions.Count; i++)
        {
            var c = FilteredOptions[i];
            var b = new AutocompleteOption(c)
            {
                Y = y,
                Padding = new MarginPadding { Right = Math.Max(Margin, DrumScrollContainer.ScrollbarSize) },
                Action = () => { SelectOption(c); }
            };
            ScrollContainer.Add(b);
            if (target == c) targetButton = b;
            y += AutocompleteOptionBase.Height;
        }
        if (y == 0)
        {
            ScrollContainer.Add(new AutocompleteOptionBase
            {
                Padding = new MarginPadding { Right = Math.Max(Margin, DrumScrollContainer.ScrollbarSize) },
                Text = "No matching options"
            });
            y += AutocompleteOptionBase.Height;
        }
        Popover.Height = Math.Min(AutocompleteOption.Height * DisplayCount, y); // make sure to set height before trying to scroll
        if (targetButton != null)
        {
            targetButton.BackgroundColour = DrumColors.ActiveButton;
            ScrollContainer.ScrollIntoView(targetButton);
        }
    }

    protected string loadedFilterString;

    void UpdateFilterAndDisplay()
    {
        UpdateFilter();
        UpdateOptionDisplay();
    }

    protected virtual void UpdateFilter()
    {
        // see performance notes in CommandPalette.cs
        if (!Open) return;
        if (loadedFilterString != Input.Current.Value)
        {
            loadedFilterString = Input.Current.Value;
            var filter = new Filter(loadedFilterString);

            // if (!targetFound) TargetI = 0;
            // UpdateSearch();

            FilteredOptions.Clear();
            var targetFound = false;
            var i = 0;
            foreach (var o in Options)
            {
                if (filter.MatchesAll || (o != null && o.MatchesFilter(filter)))
                {
                    if (target == null) target = o;
                    if (o == target)
                    {
                        targetFound = true;
                        _targetI = i;
                    }
                    FilteredOptions.Add(o);
                    i += 1;
                }
            }
            if (!targetFound) TargetI = 0; // this updates `Target`
        }
    }
    protected Container backgroundContainer;
    public void SetBackgroundWidth(float width)
    {
        backgroundContainer.RelativeSizeAxes = Axes.None;
        backgroundContainer.Width = width;
    }
    public Autocomplete()
    {
        Height = SearchHeight + Margin * 2; // fixed height so external components don't update when we open
        // starts hidden
        backgroundContainer = new Container();
        backgroundContainer.AutoSizeAxes = Axes.Y;
        backgroundContainer.RelativeSizeAxes = Axes.X;
        AddInternal(backgroundContainer);
        backgroundContainer.Add(ButtonContainer = new Container
        {
            Padding = new MarginPadding(Margin),
            AutoSizeAxes = Axes.Y,
            RelativeSizeAxes = Axes.X
        });
        ButtonContainer.Add(Input = new AutocompleteInput
        {
            PlaceholderText = "Type to search",
            Height = SearchHeight,
            RelativeSizeAxes = Axes.X,
            // CommitOnFocusLost = true // we probably shouldn't commit when we press escape to lose focus, oh well
        });
        Input.FocusChanged += focus =>
        {
            Open = focus;
            OnFocusChange?.Invoke(focus);
        };
        Input.OnCommit += () =>
        {
            if (FilteredOptions.Count > 0) SelectOption(target);
        };
        Input.Current.ValueChanged += v => UpdateFilterAndDisplay();
        Popover = new MouseBlockingContainer
        {
            RelativeSizeAxes = Axes.X,
            Y = SearchHeight + Margin,
            // this lets us place the scrollbar into the side margin
            // we need this separate container since BasicScrollContainer has to have masking enabled, negative padding would be masked
            Padding = new MarginPadding { Right = -Margin },
        };
        Popover.Add(Background = new Box
        {
            Colour = DrumColors.DarkBackground,
            RelativeSizeAxes = Axes.Both
        });
        Popover.Add(ScrollContainer = new DrumScrollContainer
        {
            RelativeSizeAxes = Axes.Both
        });
    }
    protected override bool Handle(UIEvent e)
    {
        if (base.Handle(e)) return true;
        return e switch
        {
            // block all mouse events from going behind us if we're open
            // mainly to prevent tooltips showing for background elements
            MouseEvent => Open,
            _ => false,
        };
    }

    protected virtual void HardCommit()
    {
        if (FilteredOptions.Count > 0) SelectOption(target);
    }

    public new virtual float Margin => 0;

    public class AutocompleteOption : AutocompleteOptionBase, IHasMarkupTooltip
    {
        T Option;
        public AutocompleteOption(T option)
        {
            Option = option;
            Text = option?.Name ?? "Unset";
        }
        public string MarkupTooltip => Option?.MarkupTooltip;
    }

    public class AutocompleteInput : DrumTextBox
    {
        public event Action<bool> FocusChanged;
        protected override void OnFocus(FocusEvent e)
        {
            FocusChanged?.Invoke(true);
            base.OnFocus(e);
        }
        public new Action OnCommit;
        protected override void Commit()
        {
            OnCommit?.Invoke();
            base.Commit();
        }
        protected override void OnFocusLost(FocusLostEvent e)
        {
            FocusChanged?.Invoke(false);
            base.OnFocusLost(e);
        }
    }


    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (!Open) return false;
        switch (e.Key)
        {
            case Key.Down:
                if (TargetI.HasValue)
                {
                    TargetI += 1;
                    UpdateOptionDisplay();
                }
                return true;
            case Key.Up:
                if (TargetI.HasValue)
                {
                    TargetI -= 1;
                    UpdateOptionDisplay();
                }
                return true;
            case Key.PageDown:
                if (TargetI.HasValue)
                {
                    TargetI = Math.Min(FilteredOptions.Count - 1, TargetI.Value + (DisplayCount - 1));
                    UpdateOptionDisplay();
                }
                return true;
            case Key.PageUp:
                if (TargetI.HasValue)
                {
                    TargetI = Math.Max(0, TargetI.Value - (DisplayCount - 1));
                    UpdateOptionDisplay();
                }
                return true;
            case Key.Enter:
            case Key.KeypadEnter:
                if (e.ControlPressed) { HardCommit(); return true; }
                break;
        }
        return base.OnKeyDown(e);
    }
    public void Focus(IFocusManager focusManager) => focusManager.ChangeFocus(Input);
}

public class Autocomplete : Autocomplete<BasicAutocompleteOption>
{
    public IEnumerable<string> StringOptions
    {
        set => Options = value.Select(e => new BasicAutocompleteOption(e));
    }
}

public class BasicAutocompleteOption : IFilterable
{
    public string Name { get; set; }
    public BasicAutocompleteOption(string name) { Name = name; }
}

public class AutocompleteKeyName : IFilterable
{
    public string Name { get; set; }
    public string Key { get; set; }
    public AutocompleteKeyName(string key) { Key = key; Name = key; }
    public AutocompleteKeyName(string key, string name) { Key = key; Name = name; }
}