using System.IO;
using DrumGame.Game.Browsers;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Modals;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Views;

public class MapLibraryView : RequestModal
{
    RequestModal ActiveRequest;
    public MapLibraryView() : base(new RequestConfig
    {
        Title = "Map Libraries"
    })
    {
        AddFooterButton(new DrumButton
        {
            Text = "Add New Library",
            Width = 150,
            Height = 30,
            Action = RequestNewLibrary
        });
    }

    public void RequestNewLibrary()
    {
        var req = Util.Palette.Request(new RequestConfig
        {
            Title = "Adding New Library",
            Description = "Drag + drop a folder to autofill these fields",
            Fields = new FieldBuilder()
                .Add(new StringFieldConfig("Folder Path"))
                .Add(new StringFieldConfig("Name"))
                .Add(new NumberFieldConfig
                {
                    Label = "Recursive Depth",
                    DefaultValue = 5,
                    Tooltip = "How many nested layers of folders to search for maps."
                })
                .Add(new BoolFieldConfig("Scan for DTX"))
                .Add(new BoolFieldConfig("Scan for BJson"))
                .Add(new BoolFieldConfig("Scan for song.ini"))
                .Build(),
            CommitText = "Add",
            OnCommit = e =>
            {
                var provider = new MapLibrary
                {
                    Path = e.GetValue<string>(0),
                    Name = e.GetValue<string>(1),
                    RecursiveDepth = e.GetValue<int?>(2) ?? 5,
                    ScanDtx = e.GetValue<bool>(3),
                    ScanBjson = e.GetValue<bool>(4),
                    ScanSongIni = e.GetValue<bool>(5)
                };
                Util.MapStorage.MapLibraries.Add(provider);
            }
        });
        ActiveRequest = req;
    }

    [CommandHandler]
    public bool OpenFile(CommandContext context)
    {
        if (context.TryGetParameter(out string path))
        {
            if (File.Exists(path))
                path = Path.GetDirectoryName(path);
            if (Directory.Exists(path))
            {
                if (ActiveRequest == null || !ActiveRequest.IsAlive)
                    RequestNewLibrary();
                ActiveRequest.SetValue(0, path);
                ActiveRequest.SetValue(1, Path.GetFileName(path));
                var enumerationOptions = new EnumerationOptions
                {
                    MaxRecursionDepth = 5,
                    MatchCasing = MatchCasing.CaseInsensitive,
                    RecurseSubdirectories = true
                };
                var containsDtx = Directory.GetFiles(path, "*.dtx", enumerationOptions).Length > 0;
                ActiveRequest.SetValue(3, containsDtx);
                var containsBjson = Directory.GetFiles(path, "*.bjson", enumerationOptions).Length > 0;
                ActiveRequest.SetValue(4, containsBjson);
                ActiveRequest.SetValue(5, Directory.GetFiles(path, "song.ini", enumerationOptions).Length > 0);
                return true;
            }
        }
        return false;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        LoadDisplay();
        Util.CommandController.RegisterHandlers(this);
        Util.MapStorage.MapLibraries.Changed += LoadDisplay;
    }

    public void LoadDisplay()
    {
        if (Children.Count > 0) Clear();
        var y = 0f;
        foreach (var source in Util.MapStorage.ValidLibraries)
        {
            var row = new LibraryRow(source) { Y = y };
            Add(row);
            y += row.Height + CommandPalette.Margin;
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        Util.MapStorage.MapLibraries.Changed -= LoadDisplay;
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }

    class LibraryRow : CompositeDrawable, IHasMarkupTooltip, IHasContextMenu
    {
        Box Background;
        const float NormalAlpha = 0.05f;

        readonly MapLibrary Library;

        public MenuItem[] ContextMenuItems => ContextMenuBuilder.New(Library)
            .Add("Reveal In File Explorer", e => Util.RevealInFileExplorer(e.AbsolutePath))
            .Add("Refresh", e =>
            {
                Util.MapStorage.CheckWriteTimes();
                RefreshStats();
            })
                .Tooltip($"Quickly checks for modified maps and reloads them. This runs each time the game is booted.\nYou can also trigger a refresh with the command {IHasCommand.GetMarkupTooltip(Command.Refresh)}")
            .Add("Force Reload Metadata", Util.MapStorage.ForceReloadMetadata)
                .Tooltip("This will remove cached metadata for all maps in this library.\nNormally the game reloads metadata each time a file changes, but this can be used to force a reload.\n\nTypically takes ~5s for 1000 maps.")
            .Add("Disable", Util.MapStorage.MapLibraries.Disable).Hide(Library.Disabled)
            .Add("Enable", Util.MapStorage.MapLibraries.Enable).Hide(!Library.Disabled)
            .Add("Remove", e =>
            {
                Util.Palette.Request(new RequestConfig
                {
                    Title = $"Remove {e.Name}",
                    Description = "This will only remove the library from Drum Game. You files will be unaffected.",
                    CommitText = "Remove",
                    CloseText = "Cancel",
                    OnCommit = _ => Util.MapStorage.MapLibraries.Remove(e)
                });
            }).Danger().Disabled(Library.IsMain ? "Main provider cannot be removed" : null)
            .Build();

        public string MarkupTooltip => "Right click for more options";

        void RefreshStats()
        {
            NameText.Text = Library.FriendlyName;

            var (bjson, dtx, songIni) = Library.CountMaps();
            if (bjson != null || dtx != null || songIni != null)
            {
                var text = "";
                if (bjson != null) text += $"BJson maps: {bjson}";
                if (dtx != null) text += $" DTX maps: {dtx}";
                if (songIni != null) text += $" Song.ini maps: {songIni}";
                CountText.Text = text.Trim();
            }

            if (Library.Disabled)
            {
                if (DisabledText == null)
                {
                    AddInternal(DisabledText = new()
                    {
                        Text = "(Disabled)",
                        Colour = DrumColors.FadedText,
                        Font = NameText.Font.With(size: 18),
                        Y = NameText.Y + NameText.Height / 2,
                        Origin = Anchor.CentreLeft
                    });
                }
                DisabledText.Alpha = 1;
                DisabledText.X = NameText.Width + NameText.X + 5;
            }
            else if (DisabledText != null) DisabledText.Alpha = 0;
        }

        SpriteText DisabledText;
        SpriteText NameText;
        SpriteText CountText;
        public LibraryRow(MapLibrary provider)
        {
            Library = provider;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Height = 60;
            RelativeSizeAxes = Axes.X;
            AddInternal(Background = new Box
            {
                RelativeSizeAxes = Axes.Both
            });
            Background.Colour = new Colour4(1, 1, 1, NormalAlpha);
            var y = 0f;
            AddInternal(NameText = new SpriteText
            {
                X = 3,
                Font = FrameworkFont.Regular.With(size: 28)
            });
            y += NameText.Font.Size;
            AddInternal(CountText = new SpriteText
            {
                X = 10,
                Y = y,
                Font = FrameworkFont.Regular.With(size: 20)
            });
            y += 18;
            y += 3;
            Height = y;
            RefreshStats();
        }
        protected override bool OnMouseDown(MouseDownEvent e) => true;
        protected override bool OnHover(HoverEvent e)
        {
            Background.Colour = new Colour4(1, 1, 1, 0.2f);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            Background.Colour = new Colour4(1, 1, 1, NormalAlpha);
        }
    }
}