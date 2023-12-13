using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osuTK.Input;


namespace DrumGame.Game.Components.Basic.Autocomplete;


public class AutocompleteFreeSolo : CompositeDrawable, IAcceptFocus
{
    bool _open = false;
    public bool Open
    {
        get => _open; set
        {
            if (_open == value) return;
            _open = value;
            if (value)
                Util.GetParent<DrumPopoverContainer>(this).Popover(Popover, ButtonContainer);
            else
                Util.GetParent<DrumPopoverContainer>(this)?.ClosePopover(Popover, false);
            UpdateFilterAndDisplay();
        }
    }
    protected const float SearchHeight = 30;
    Box Background { get; }
    public string Value { get => Input.Current.Value; set => Input.Current.Value = value; }

    protected override void Dispose(bool isDisposing)
    {
        Popover.Dispose();
        base.Dispose(isDisposing);
    }

    protected string target; // target for keyboard etc.
    List<string> _options;
    public IEnumerable<string> Options
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
    protected List<string> FilteredOptions = new();

    public Action<bool> OnFocusChange;

    public AutocompleteInput Input;
    DrumScrollContainer ScrollContainer;
    Container Popover;
    Container ButtonContainer;
    const int DisplayCount = 12;
    protected void Commit(string value = null)
    {
        Open = false;
        if (value != null)
            Value = value;
        OnCommit?.Invoke(Value);
    }
    public Action<string> OnCommit;
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
                Action = () => Commit(c)
            };
            ScrollContainer.Add(b);
            if (i == _targetI) targetButton = b;
            y += AutocompleteOptionBase.Height;
        }
        if (y == 0)
        {
            ScrollContainer.Add(new AutocompleteOptionBase
            {
                Padding = new MarginPadding
                {
                    Right = Math.Max(Margin, DrumScrollContainer.ScrollbarSize)
                },
                Text = "No matching options - press enter to save"
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

            FilteredOptions.Clear();

            TargetI = null;
            foreach (var o in Options)
            {
                if (filter.MatchesAll || (o != null && filter.MatchesString(o)))
                    FilteredOptions.Add(o);
            }
        }
    }
    protected Container backgroundContainer;
    public void SetBackgroundWidth(float width)
    {
        backgroundContainer.RelativeSizeAxes = Axes.None;
        backgroundContainer.Width = width;
    }
    public AutocompleteFreeSolo()
    {
        Height = SearchHeight + Margin * 2; // fixed height so external components don't update when we open
                                            // starts hidden
        backgroundContainer = new Container();
        backgroundContainer.AutoSizeAxes = Axes.Y;
        backgroundContainer.RelativeSizeAxes = Axes.X;
        AddInternal(backgroundContainer);
        backgroundContainer.Add(ButtonContainer = new MouseBlockingContainer
        {
            Padding = new MarginPadding(Margin),
            AutoSizeAxes = Axes.Y,
            RelativeSizeAxes = Axes.X
        });
        ButtonContainer.Add(Input = new AutocompleteInput
        {
            PlaceholderText = "Type to search",
            Height = SearchHeight,
            RelativeSizeAxes = Axes.X
        });
        Input.FocusChanged += focus =>
        {
            Open = focus;
            OnFocusChange?.Invoke(focus);
        };
        Input.OnCommit += () => Commit(target);
        Input.Current.ValueChanged += v =>
        {
            UpdateFilterAndDisplay();
        };
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

    public new virtual float Margin => 0;

    public class AutocompleteOption : AutocompleteOptionBase
    {
        public AutocompleteOption(string option) { Text = option; }
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
        switch (e.Key)
        {
            case Key.Down:
                if (FilteredOptions.Count > 0)
                {
                    if (TargetI.HasValue)
                        TargetI += 1;
                    else
                        TargetI = 0;
                    UpdateOptionDisplay();
                }
                UpdateOptionDisplay();
                return true;
            case Key.Up:
                if (FilteredOptions.Count > 0)
                {
                    if (TargetI.HasValue)
                        TargetI -= 1;
                    else
                        TargetI = -1;
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
                if (e.ControlPressed)
                {
                    Commit(target);
                    return true;
                }
                break;
        }
        return base.OnKeyDown(e);
    }
    public void Focus(InputManager inputManager) => inputManager.ChangeFocus(Input);
}
