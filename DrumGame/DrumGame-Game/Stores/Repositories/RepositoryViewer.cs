using System;
using System.IO;
using System.Linq;
using DrumGame.Game.API;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.IO;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osuTK.Input;

namespace DrumGame.Game.Stores.Repositories;

public class RepositoryViewer : CompositeDrawable, IModal, IAcceptFocus
{
    public Action CloseAction { get; set; }
    void Close() => CloseAction?.Invoke();
    Container Inner;
    NoDragScrollContainer ScrollContainer;
    SearchTextBox Search;

    public void UpdateSearch()
    {
        var search = Search.Current.Value;
        var y = 0f;
        foreach (var child in ScrollContainer.Children.OfType<RepoRow>())
        {
            child.Y = y;
            child.UpdateSearch(search);
            y += child.Height;
        }
    }

    [CommandHandler]
    public void HighlightRandom()
    {
        var options = ScrollContainer.OfType<RepoRow>().SelectMany(e => e.Definition.Cache.Maps).ToList();
        Search.Current.Value = options.Random().Artist;
    }

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        DownloadedCache.Save();
        base.Dispose(isDisposing);
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Util.CommandController.RegisterHandlers(this);

        AddInternal(new Box { Colour = DrumColors.DarkActiveBackground, Height = 22, RelativeSizeAxes = Axes.X });
        AddInternal(new SpriteText
        {
            Font = FrameworkFont.Regular.With(size: 16),
            Y = 3,
            Text = "You can drag + drop a DTXMania chart (*.zip, *.dtx, or set.def) at any point to convert and load the chart."
        });

        AddInternal(Search = new SearchTextBox { Height = 40, RelativeSizeAxes = Axes.X, Y = 22 });
        Search.AddHelpButton<JsonRepositoryBeatmap>("Repository Search");
        Search.Current.ValueChanged += e => UpdateSearch();

        AddInternal(new CommandIconButton(Command.HighlightRandom, FontAwesome.Solid.Random, 32)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            X = -32,
            Y = Search.Y + 4
        });

        AddInternal(Inner = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Padding = new MarginPadding { Top = Search.Height + Search.Y }
        });
        Inner.Add(new ModalBackground(Close));
        ScrollContainer = new NoDragScrollContainer()
        {
            RelativeSizeAxes = Axes.Both
        };
        Inner.Add(ScrollContainer);

        var repoDir = Util.Resources.GetDirectory("repositories");
        var list = repoDir.GetFiles("*.json").Where(e => e.Name != "repository-schema.json" && e.Name != "downloaded.json").Select(e =>
        {
            try
            {
                var def = JsonConvert.DeserializeObject<RepositoryDefinition>(File.ReadAllText(e.FullName));
                def.Source = e.FullName;
                def.TryLoadCache();
                return def;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load repository: {e.FullName}");
                return new RepositoryDefinition { Title = $"{e.FullName} (failed)" };
            }
        }).OrderBy(e => e.Order);

        var hasRepo = false;

        var y = 0f;
        foreach (var repo in list)
        {
            var row = new RepoRow(repo) { Y = y };
            ScrollContainer.Add(row);
            y += row.Height;
            hasRepo = true;
        }
        if (!hasRepo)
        {
            ScrollContainer.Add(new SpriteText
            {
                Text = "No repositories found.",
                Y = 10,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Font = FrameworkFont.Regular.With(size: 50)
            });
        }
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        return base.OnMouseDown(e) || true; // prevent closing when we click somewhere inside
    }
    public void Focus(IFocusManager _) => Search.TakeFocus();

    class MapRow : DrumButton, IHasMarkupTooltip, IHasContextMenu
    {
        public void MarkDownloaded()
        {
            DownloadedCache.Add(Map);
            UpdateDownloaded();
        }
        public void UnmarkDownloaded()
        {
            DownloadedCache.Remove(Map);
            UpdateDownloaded();
        }
        protected override bool OnClick(ClickEvent e)
        {
            if (e.Button == MouseButton.Right) return false;
            return base.OnClick(e);
        }
        JsonRepositoryBeatmap Map;
        void DownloadOrOpen(bool tryDownload, bool allowClipboard)
        {
            var mapper = Util.GetParent<RepoRow>(this).Definition.GetMapper(Map);
            if (Map.DownloadUrl == null && Map.Url == null)
            {
                if (tryDownload || !allowClipboard) Util.Google(Map.FullName);
                else Util.SetClipboard(Map.FullName);
                return;
            }
            var url = tryDownload ? Map.DownloadUrl ?? Map.Url : Map.Url;
            if (tryDownload && Map.CanDirectDownload)
            {
                MarkDownloaded();
                using var _ = new MapImportContext { Author = mapper, Url = url };
                FileImporters.DownloadAndOpenFile(url);
            }
            else
            {
                if (tryDownload)
                {
                    MarkDownloaded();
                    FileImporters.NextContext = new MapImportContext(false) { Author = mapper, Url = url };
                    url = FileImporters.DirectDownloadUrl(url);
                }
                Util.Host.OpenUrlExternally(url);
            }
        }
        public void UpdateDownloaded()
        {
            if (DownloadedCache.Contains(Map))
            {
                if (this.Children.Any(e => e is SpriteIcon)) return;
                AddInternal(new SpriteIcon
                {
                    Icon = FontAwesome.Solid.CheckSquare,
                    Depth = -5,
                    X = -12,
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Height = 20,
                    Width = 20
                });
            }
            else
            {
                var icon = InternalChildren.OfType<SpriteIcon>().FirstOrDefault();
                if (icon != null) RemoveInternal(icon, true);
            }
        }
        public MapRow(JsonRepositoryBeatmap map)
        {
            Map = map;
            Height = 20;
            RelativeSizeAxes = Axes.X;
            Action = () => DownloadOrOpen(Ctrl, true);
            UpdateDownloaded();
        }
        public bool Ctrl => Util.InputManager.CurrentState.Keyboard.ControlPressed;
        string IHasMarkupTooltip.MarkupTooltip
        {
            get
            {
                var url = Ctrl ? Map.DownloadUrl : Map.Url;
                string text;
                if (Map.DownloadUrl == null && Map.Url == null)
                {
                    if (Ctrl) text = "<brightCyan>Search name on Google</>";
                    else text = "<brightCyan>Copy name to clipboard</>";
                }
                else if (Ctrl) text = $"<brightGreen>Download {url}</>";
                else text = $"<brightOrange>Open {url}</> (Hold Ctrl to download)";
                if (!string.IsNullOrWhiteSpace(Map.Comments))
                    text += "\n" + MarkupText.Escape(Map.Comments); // comments frequently contains "\" character
                if (Map.Difficulties != null)
                    text += "\nDifficulty: " + MarkupText.Escape(string.Join(" / ", Map.Difficulties));
                return text;
            }
        }

        public MenuItem[] ContextMenuItems
        {
            get
            {
                var downloaded = DownloadedCache.Contains(Map);
                return ContextMenuBuilder.New(Map)
                    .Add("Download", e => DownloadOrOpen(true, false)).Color(DrumColors.BrightGreen).Disabled(Map.DownloadUrl == null)
                    .Add("View online", e => DownloadOrOpen(false, false)).Disabled(Map.Url == null).Color(DrumColors.BrightOrange)
                    .Add("Copy name to clipboard", e => Util.SetClipboard(e.FullName))
                    .Add("Search name on Google", e => Util.Google(e.FullName))
                    .Add("Search name on YouTube", e => Util.YouTube(e.FullName))
                    .Add($"{(downloaded ? "Unmark" : "Mark")} as downloaded",
                        e => { if (downloaded) UnmarkDownloaded(); else MarkDownloaded(); })
                    .Build();
            }
        }

        protected override SpriteText CreateText()
        {
            var text = $"{Map.Artist} - {Map.Title}";
            if (Map.Mapper != null) text += " mapped by " + Map.Mapper;
            return new SpriteText
            {
                Font = FrameworkFont.Regular.With(size: 18),
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = text,
                X = 30
            };
        }
    }
    class RepoRow : DrumButton, IHasContextMenu, IHasMarkupTooltip // should consider just making this a container then putting buttons inside it
    {
        public readonly RepositoryDefinition Definition;
        public RepoRow(RepositoryDefinition definition)
        {
            Definition = definition;
            RelativeSizeAxes = Axes.X;
        }
        SpriteText CountText;

        Container<MapRow> List;
        protected override SpriteText CreateText() => new()
        {
            Font = FrameworkFont.Regular.With(size: 45),
            Text = Definition.Title,
            Y = 2.5f,
            X = 10
        };

        bool showAll = false; // only valid for a single UpdateSearch call

        public MenuItem[] ContextMenuItems => ContextMenuBuilder.New(Definition)
            .Add("Refresh", e => e.Refresh()).Disabled(!Definition.CanRefresh)
            .Add("View online", e => Util.Host.OpenUrlExternally(Definition.HomeUrl ?? Definition.Url))
                .Disabled(Definition.HomeUrl == null && Definition.Url == null)
                .Color(DrumColors.BrightGreen)
            .Add("Refresh Specifc Page", e =>
            {
                Util.Palette.RequestNumber("Refreshing Page", "Page", 0, page => e.RefreshPage((int)page));
            })
            .Build();

        const int maxRowsDisplayed = 1000;

        string IHasMarkupTooltip.MarkupTooltip
        {
            get
            {
                var foot = "";
                if (Definition.Url != null)
                    foot += $"\nRepo source: {Definition.HomeUrl ?? Definition.Url}";
                foot += "\n<brightRed>Right click</c> to display more options";
                if (filteredCount == 0)
                    return $"<brightYellow>No maps match filter</c>{foot}";
                if (filteredCount <= maxRowsDisplayed)
                    return $"<brightGreen>Left click</c> to display {filteredCount} maps{foot}";
                return $"<brightYellow>More than {maxRowsDisplayed} maps</c>, please filter before viewing{foot}";
            }
        }

        int filteredCount;

        public void UpdateSearch(string search)
        {
            if (CountText == null) return;
            var emptySearch = search == null || search.Length == 0;
            var filtered = emptySearch ? Definition.Cache.Maps : Definition.Filtered(search).AsList();
            filteredCount = filtered.Count;
            CountText.Text = $"({filtered.Count} of {Definition.Count} maps)";
            Enabled.Value = filtered.Count > 0;
            var oldCount = List.Count;
            List.Clear();
            if (filtered.Count > maxRowsDisplayed) showAll = false;
            if (filtered.Count > oldCount && oldCount > 0) showAll = false; // if we increase the filtered count, don't show all
            if (filtered.Count <= 10 || showAll)
            {
                var y = 0f;
                foreach (var map in filtered)
                {
                    var row = new MapRow(map) { Y = y };
                    List.Add(row);
                    y += row.Height;
                }
                List.Height = y;
            }
            else List.Height = 0;
            Height = 50 + List.Height;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Action = () =>
            {
                showAll = !showAll;
                Util.GetParent<RepositoryViewer>(this).UpdateSearch();
            };
            var x = SpriteText.X + SpriteText.Width + 5;
            if (Definition.Count is int c)
            {
                AddInternal(CountText = new SpriteText
                {
                    Font = FrameworkFont.Regular.With(size: 30),
                    Y = 10,
                    X = x
                });
                AddInternal(List = new Container<MapRow>
                {
                    RelativeSizeAxes = Axes.X,
                    Y = 50
                });
                UpdateSearch(null);
            }
            else Height = 50;
            if (Definition.CanRefresh)
                AddInternal(new IconButton(Definition.Refresh, FontAwesome.Solid.Sync, 35)
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Y = 7.5f,
                    X = -5
                });
        }
    }
}