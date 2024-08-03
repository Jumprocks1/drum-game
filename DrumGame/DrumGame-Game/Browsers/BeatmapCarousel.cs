using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DrumGame.Game.API;
using DrumGame.Game.Browsers.BeatmapSelection;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Containers;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace DrumGame.Game.Browsers;

public class BeatmapCarousel : CompositeDrawable
{
    public static BeatmapCarousel Current => Util.Find<BeatmapCarousel>(Util.DrumGame);
    public void JumpToNewMap(string newMap)
    {
        Selector.Refresh(newMap);
        JumpToMap(newMap);
        Util.Palette.ShowMessage($"Imported {newMap}");
    }

    // this should be used for padding to avoid overlapping the carousel
    public new const float Width = BeatmapCard.Margin * 2 + BeatmapCard.Width;
    public BeatmapSelector Selector;
    BeatmapSelectorState State => Selector.State;
    public List<BeatmapSelectorMap> FilteredMaps => Selector.FilteredMaps;

    NoMaps NoMapsText;

    const float ItemSize = BeatmapCard.Margin * 2 + BeatmapCard.Height;
    double CarouselPosition = 0;
    // this is a double instead of some sort of int or reference to SelectedIndex because
    // the target can get set to halfway between a beatmap by using the mouse
    double _carouselTarget = 0;
    double CarouselTarget
    {
        get => _carouselTarget; set
        {
            if (_carouselTarget == value) return;
            _carouselTarget = value;
            if (FilteredMaps.Count > 0)
            {
                State.SelectedIndex = Math.Clamp((int)Math.Round(CarouselTarget / ItemSize), 0, FilteredMaps.Count - 1);
            }
        }
    }
    public void PullTarget(int i) => CarouselTarget = i * ItemSize;
    public void HardPullTarget(int i) => CarouselPosition = CarouselTarget = i * ItemSize;
    List<BeatmapCard> VisibleCards = new();
    List<BeatmapCard> FreeCards = new();
    void FreeRange(int freeStart, int freeEnd)
    {
        for (int i = freeStart; i < freeEnd; i++)
        {
            VisibleCards[i].Alpha = 0;
            FreeCards.Add(VisibleCards[i]);
        }
        VisibleCards.RemoveRange(freeStart, freeEnd - freeStart);
    }
    BeatmapCard GetCard(BeatmapSelectorMap map)
    {
        BeatmapCard res = null;
        if (FreeCards.Count > 0)
        {
            for (var i = 0; i < FreeCards.Count; i++)
            {
                // unfortunately this won't always work if they maps don't come in the right order
                // the fix would be to change GetCard to GetCards so that this method could resolve all cards
                // it would return an array and it would only initially fill in slots that match
                if (FreeCards[i].Map == map)
                {
                    res = FreeCards[i];
                    FreeCards.RemoveAt(i);
                }
            }
            if (res == null) // if we didn't find any matches
                res = FreeCards.Pop();
        }
        else
        {
            res = new BeatmapCard(this)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
            };
            AddInternal(res);
        }
        res.LoadMap(map);
        return res;
    }
    public (int StartIndex, int EndIndex) GetVisibleRange() // exclusive on right side
    {
        // Cards are center on CarouselPosition with a range of DrawHeiht, each one takes up ItemSize space
        // we add/subtract 0.5 since the cards are centered inside ItemSize
        var topCard = (int)Math.Floor((CarouselPosition - DrawHeight / 2) / ItemSize + 0.5);
        var bottomCard = (int)Math.Ceiling((CarouselPosition + DrawHeight / 2) / ItemSize - 0.5) + 1;
        return (Math.Clamp(topCard, 0, FilteredMaps.Count),
            Math.Clamp(bottomCard, 0, FilteredMaps.Count));
    }

    bool dragging;
    protected override bool OnDragStart(DragStartEvent e)
    {
        if (e.Button == MouseButton.Left || e.Button == MouseButton.Middle)
        {
            return dragging = true;
        }
        return base.OnDragStart(e);
    }
    protected override void OnDragEnd(DragEndEvent e)
    {
        dragging = false;
        base.OnDragEnd(e);
    }
    protected override void OnDrag(DragEvent e) => CarouselTarget -= e.Delta.Y;
    public void FilterChanged()
    {
        FreeRange(0, VisibleCards.Count);
        LoadedRange = (0, 0);
        NoMapsText.Show(FilteredMaps.Count == 0);
        Update();
    }

    public void InvalidateAllCards() // sets all cards so that they will require a refresh
    {
        for (var i = 0; i < VisibleCards.Count; i++)
            VisibleCards[i].Invalidate();
        for (var i = 0; i < FreeCards.Count; i++)
            FreeCards[i].Invalidate();
    }

    (int StartIndex, int EndIndex) LoadedRange;
    protected override void Update()
    {
        var dt = Clock.TimeInfo.Elapsed;
        var selected = State.SelectedIndex;
        if (!dragging)
        {
            // pulls the target to the nearest card
            // this doesn't just set to selected * ItemSize since it's fun to hit to scroll past the end of the maps with keyboard
            _carouselTarget = Util.ExpLerp(CarouselTarget, selected * ItemSize, 0.99, dt, 0.01);
        }
        CarouselPosition = Util.ExpLerp(CarouselPosition, CarouselTarget, 0.99, dt, 0.02);

        // first we free up no longer visible boxes
        var newRange = GetVisibleRange();
        if (LoadedRange.EndIndex > newRange.EndIndex)
        {
            var startRemove = Math.Max(LoadedRange.StartIndex, newRange.EndIndex) - LoadedRange.StartIndex;
            var endRemove = LoadedRange.EndIndex - LoadedRange.StartIndex;
            FreeRange(startRemove, endRemove);
        }
        var startDiff = newRange.StartIndex - LoadedRange.StartIndex;
        if (startDiff > 0)
        {
            FreeRange(0, Math.Min(startDiff, VisibleCards.Count));
        }

        // now we add new boxes
        if (startDiff < 0) // need to move free boxes into start of loaded boxes
        {
            for (int i = newRange.StartIndex; i < Math.Min(newRange.EndIndex, LoadedRange.StartIndex); i++)
            {
                var box = GetCard(FilteredMaps[i]);
                VisibleCards.Insert(i - newRange.StartIndex, box);
            }
        }
        if (newRange.EndIndex > LoadedRange.EndIndex) // need to move free boxes into end of loaded boxes
        {
            for (int i = Math.Max(newRange.StartIndex, LoadedRange.EndIndex); i < newRange.EndIndex; i++)
            {
                VisibleCards.Add(GetCard(FilteredMaps[i]));
            }
        }
        LoadedRange = newRange;

        for (int bi = 0; bi < VisibleCards.Count; bi++)
        {
            var box = VisibleCards[bi];
            var i = bi + newRange.StartIndex;
            box.Selected = selected == i;
            var y = i * ItemSize - CarouselPosition;
            var circleY = 700; // need to test this with different aspect ratios
            var circleX = 350;
            box.Y = (float)y;
            var clampedY = Math.Clamp(y / circleY, -1, 1);
            box.SetX(-BeatmapCard.Margin + (float)((1 - Math.Cos(clampedY * Math.PI / 2)) * circleX));
        }
        base.Update();
    }

    public void ReloadCard(BeatmapSelectorMap map)
    {
        foreach (var card in VisibleCards)
            if (card.Map == map)
                card.LoadMap(map, true);
    }

    public BeatmapCarousel(BeatmapSelector selector)
    {
        Selector = selector;
        RelativeSizeAxes = Axes.Both;
        CarouselPosition = _carouselTarget = State.SelectedIndex * ItemSize;
        AddInternal(NoMapsText = new NoMaps
        {
            Alpha = 0 // hidden by default
        });
        Util.CommandController.RegisterHandlers(this);
    }

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }

    // masking handled via virtualization
    public override bool UpdateSubTreeMasking() => true;
    [CommandHandler] public void Select() => Selector.SelectMap(false);
    [CommandHandler] public void SelectAutostart() => Selector.SelectMap(true);
    [CommandHandler] public void SeekToStart() => CarouselTarget = 0;
    [CommandHandler] public void SeekToEnd() => CarouselTarget = ItemSize * (FilteredMaps.Count - 1);
    [CommandHandler] public void Up() => CarouselTarget -= ItemSize;
    [CommandHandler] public void Down() => CarouselTarget += ItemSize;
    [CommandHandler] public void PageUp() => CarouselTarget -= ItemSize * 4;
    [CommandHandler] public void PageDown() => CarouselTarget += ItemSize * 4;
    [CommandHandler]
    public bool JumpToMap(CommandContext context)
    {
        context.GetString(e =>
        {
            var withExtension = e + ".bjson";
            int find()
            {
                var target = -1;
                for (var i = 0; i < FilteredMaps.Count; i++)
                {
                    var map = FilteredMaps[i];
                    if (map.MapStoragePath == e || map.MapStoragePath == withExtension) return i;
                    if (map.MapStoragePath.StartsWith(e, StringComparison.OrdinalIgnoreCase)) target = i;
                }
                if (target != -1) return target;
                for (var i = 0; i < FilteredMaps.Count; i++)
                    if (FilteredMaps[i].LoadedMetadata.Id.Equals(e, StringComparison.OrdinalIgnoreCase))
                        return i;
                return target;
            }
            var target = find();
            if (target != -1) CarouselTarget = target * ItemSize;
        }, "Jumping to Map", "Filename", Util.ShortClipboard);
        return true;
    }
    public bool JumpToMap(BeatmapMetadata metadata)
    {
        for (var i = 0; i < FilteredMaps.Count; i++)
        {
            if (FilteredMaps[i].LoadedMetadata == metadata)
            {
                CarouselTarget = i * ItemSize;
                return true;
            }
        }
        return false;
    }
    public void JumpToMap(string mapStoragePath)
    {
        for (var i = 0; i < FilteredMaps.Count; i++)
        {
            if (FilteredMaps[i].MapStoragePath == mapStoragePath)
            {
                CarouselTarget = i * ItemSize;
                return;
            }
        }
    }
    RandomSelector<BeatmapSelectorMap> selector;
    const int SelectorMemory = 30;
    [CommandHandler]
    public void HighlightRandom() => CarouselTarget = (selector ??= new(SelectorMemory))
        .Next(FilteredMaps, State.SelectedIndex) * ItemSize;
    [CommandHandler]
    public void HighlightRandomPrevious() => CarouselTarget = (selector ??= new(SelectorMemory))
        .Previous(FilteredMaps, State.SelectedIndex) * ItemSize;


    class NoMaps : CompositeDrawable
    {
        public void Show(bool show)
        {
            if (show)
            {
                var carousel = Util.GetParent<BeatmapCarousel>(this);
                var collection = carousel.State.Collection;
                if (collection != null)
                {
                    var collectionName = carousel.Selector.CollectionStorage.GetName(collection);
                    CollectionText.Text = $"Currently viewing collection: {collectionName} - click here to view Default";
                    CollectionText.Alpha = 1;
                    NewButton.Y = 45;
                }
                else
                {
                    CollectionText.Alpha = 0;
                    NewButton.Y = 25;
                }
                LibraryButton.Y = NewButton.Y + NewButton.Height + 5;
                RepoText.Y = LibraryButton.Y + LibraryButton.Height + 5;
                Alpha = 1;
            }
            else
            {
                Alpha = 0;
            }
        }
        ClickableText CollectionText;
        CommandTextIconButton NewButton;
        CommandTextIconButton LibraryButton;
        CommandTextIconButton RepoText;
        public NoMaps()
        {
            Width = BeatmapCard.Width + BeatmapCard.Margin * 2;
            RelativeSizeAxes = Axes.Y;
            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;
            AddInternal(new SpriteText
            {
                Text = "No maps found",
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Font = FrameworkFont.Regular.With(size: 40)
            });
            AddInternal(CollectionText = new ClickableText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.TopCentre,
                Font = FrameworkFont.Regular.With(size: 20),
                Y = 20,
                Action = () => Util.CommandController.ActivateCommand(Command.SelectCollection, "Default")
            });
            AddInternal(NewButton = new CommandTextIconButton(Command.CreateNewBeatmap, FontAwesome.Solid.PlusCircle, 25)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.TopCentre,
                Text = "Create New Map",
                Colour = DrumColors.BrightGreen
            });
            AddInternal(LibraryButton = new CommandTextIconButton(Command.ConfigureMapLibraries, FontAwesome.Solid.FolderPlus, 25)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.TopCentre,
                Text = "Add Local File Library",
                Colour = DrumColors.BrightBlue
            });
            AddInternal(RepoText = new CommandTextIconButton(Command.ViewRepositories, FontAwesome.Solid.Server, 25)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.TopCentre,
                Text = "Browse Online Repositories",
                Colour = DrumColors.BrightCyan
            });
        }
    }
}

