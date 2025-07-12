using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Stores;
using osuTK;
using System;
using System.IO;
using DrumGame.Game.Stores;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using DrumGame.Game.Commands;
using DrumGame.Game.Containers;
using DrumGame.Game.Components;
using osu.Framework.Platform;
using System.Collections.Generic;
using osu.Framework.Threading;
using osu.Framework.Logging;
using osu.Framework.Configuration;
using DrumGame.Game.Notation;
using DrumGame.Game.Media;
using DrumGame.Game.API;
using DrumGame.Game.Utils;
using DrumGame.Game.Components.Overlays;
using DrumGame.Game.Midi;
using SixLabors.ImageSharp;
using DrumGame.Game.IO;
using DrumGame.Game.Modals;
using osu.Framework.Graphics.Sprites;
using DrumGame.Game.Skinning;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Input.Handlers.Keyboard;
using DrumGame.Game.Input;

namespace DrumGame.Game;

public class DrumGameGameBase : osu.Framework.Game
{
    [Resolved] public FrameworkConfigManager FrameworkConfigManager { get; private set; }
    public GameThread AudioThread => Host.AudioThread;
    public readonly CommandController command = new CommandController();
    public MapStorage MapStorage;
    public CollectionStorage CollectionStorage;
    public FileSystemResources FileSystemResources;
    private Container content;
    protected override Container<Drawable> Content => content;
    private DependencyContainer dependencies;
    public VolumeController VolumeController;
    public DbStorage DbStorage;
    public DrumInputManager InputManager;
    public Lazy<DrumsetAudioPlayer> Drumset;
    protected DrumGameGameBase()
    {
        Util.DrumGame = this;
        command.RegisterHandlers(this); // we don't bother disposing since command lives inside us
    }
    protected override bool OnKeyDown(KeyDownEvent e) => command.HandleEvent(e) || base.OnKeyDown(e);
    protected override bool OnScroll(ScrollEvent e) => command.HandleEvent(e) || base.OnScroll(e);

    [BackgroundDependencyLoader]
    private void load()
    {
        Logger.Log($"Starting Drum Game {Util.VersionString}", level: LogLevel.Important);
        // first we need to load some of the dependencies required reguardless if we have a `resources` folder
        // these can be used for displaying the warning message when `resources` is not found
        dependencies.Cache(LocalConfig);
        dependencies.Cache(command);

        base.Content.Add(new DrawSizePreservingFillContainer
        {
            TargetDrawSize = new Vector2(1280, 720),
            Strategy = DrawSizePreservationStrategy.Minimum,
            Child = new DrumPopoverContainer
            {
                Child = content = new DrumContextMenuContainer()
            }
        });

        dependencies.Cache(KeybindConfig = new KeybindConfigManager(Storage));
        if (InputManager != null) dependencies.Cache(InputManager);

        command.RegisterHandler(Command.RefreshMidi, DrumMidiHandler.RefreshMidi);
        command.RegisterHandler(Command.TriggerRandomMidiNote, DrumMidiHandler.TriggerRandomNote);
        DrumMidiHandler.AddNoteHandler(command.OnMidiNote);
        DrumMidiHandler.RefreshMidi();
        loadResources();
    }
    void loadResources()
    {
        // everything below here depends on the resources folder in some way

        // attempt to load resources folder, could probably move this to a method
        string resLocation = null;
        if (resLocation == null)
        {
            var searchPaths = new string[] {
                LocalConfig.FileSystemResources.Value,
                "resources/",
                "../resources/",
                "../../resources/",
                "../../../resources/",
                "../../../../resources/",
            };
            foreach (var path in searchPaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    resLocation = path;
                    break;
                }
            }
            if (resLocation == null)
            {
                var errorMessage = "Failed to locate resources folder.\nLocations searched:";
                foreach (var path in searchPaths)
                    if (!string.IsNullOrWhiteSpace(path))
                        errorMessage += $"\n  {Path.GetFullPath(path)}";
                Logger.Log(errorMessage, level: LogLevel.Error);
                return;
            }
        }

        resLocation = Path.GetFullPath(resLocation);
        FileSystemResources = new FileSystemResources(resLocation, new NativeStorage(resLocation, Host));

        var musicFontStore = new FontStore(Host.Renderer, null, 800); // TODO why this hardcoded?
        Fonts.AddStore(musicFontStore);
        AddFont(FileSystemResources.ResourceStore, "fonts/Bravura", musicFontStore);

        // these all seem to load very fast, so I think it's safe to add them
        AddFont(FileSystemResources.ResourceStore, "fonts/Noto/Noto-Basic");
        AddFont(FileSystemResources.ResourceStore, "fonts/Noto/Noto-Hangul");
        AddFont(FileSystemResources.ResourceStore, "fonts/Noto/Noto-CJK-Basic");
        AddFont(FileSystemResources.ResourceStore, "fonts/Noto/Noto-CJK-Compatibility");
        AddFont(FileSystemResources.ResourceStore, "fonts/Noto/Noto-Thai");

        dependencies.Cache(FileSystemResources);
        LocalConfig.MapLibraries.Value.InitAfterResources();
        dependencies.Cache(DbStorage = new DbStorage(FileSystemResources, "database.db"));
        dependencies.Cache(MapStorage = new MapStorage(Path.Join(resLocation, "maps"), Host));
        command.RegisterHandler(Command.CleanStorage, MapStorage.Clean);


        dependencies.Cache(CollectionStorage = new CollectionStorage(Path.Join(resLocation, "collections"), MapStorage, Host));

        var shaderStore = new ResourceStore<byte[]>();
        shaderStore.AddStore(new NamespacedResourceStore<byte[]>(Resources, @"Shaders"));
        shaderStore.AddStore(FileSystemResources);
        dependencies.Cache(new DrumShaderManager(shaderStore));
        dependencies.CacheAs(this);

        dependencies.Cache(Drumset = new Lazy<DrumsetAudioPlayer>(() => new DrumsetAudioPlayer(
            Audio.GetSampleStore(FileSystemResources.GlobalStorageStore), Host, command, VolumeController)));

        // loading MusicFont takes between 20-60ms, so it helps a ton to save it!
        // we lazy load it so that the initial boot can be faster
        dependencies.Cache(new Lazy<MusicFont>(() => new MusicFont(
            FileSystemResources,
            "Bravura", "fonts/bravura_metadata.json.gz")));

        dependencies.Cache(VolumeController = new VolumeController(Audio, command, LocalConfig));

        var volumeOverlay = new VolumeOverlay { Depth = -11 }; // command palette has -10 currently
        Add(volumeOverlay);
        VolumeController.MasterVolume.Aggregate.ValueChanged += volumeOverlay.VolumeUpdated;
        SkinManager.Initialize();
    }
    protected Storage Storage { get; set; }
    public DrumGameConfigManager LocalConfig { get; private set; }
    protected KeybindConfigManager KeybindConfig { get; private set; }
    public override void SetHost(GameHost host)
    {
        Util.Host ??= host;
        base.SetHost(host);

        Storage = host.Storage;
        LocalConfig = new DrumGameConfigManager(Host.Storage);

        if (host.Window != null)
        {
            host.Window.Title = "Drum Game";
            host.Window.DragDrop += f => fileDrop(new[] { f });
        }
    }

    private readonly List<string> droppedFiles = new List<string>();
    private ScheduledDelegate importSchedule;

    private void fileDrop(string[] filePaths)
    {
        lock (droppedFiles)
        {
            droppedFiles.AddRange(filePaths);
            Logger.Log($"Adding {filePaths.Length} files for import");
            // File drag drop operations can potentially trigger hundreds or thousands of these calls on some platforms.
            // In order to avoid spawning multiple import tasks for a single drop operation, debounce a touch.
            importSchedule?.Cancel();
            importSchedule = Scheduler.AddDelayed(handlePendingDrop, 100);
        }
    }
    private void handlePendingDrop()
    {
        lock (droppedFiles)
        {
            Logger.Log($"Handling import of {droppedFiles.Count} files");
            var paths = droppedFiles.ToArray();
            droppedFiles.Clear();
            Schedule(() =>
            {
                var alreadyHandled = new HashSet<string>();
                foreach (var path in paths)
                {
                    if (alreadyHandled.Add(path))
                    {
                        Logger.Log($"Openning {path}");
                        command.ActivateCommand(Command.OpenFile, path);
                    }
                }
            });
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);
        Util.GameDisposed();
        SkinManager.Cleanup(); // saves dirty skins
        MapStorage?.Dispose(); // saves map cache
        CollectionStorage?.Dispose(); // saves dirty collections
        LocalConfig?.Dispose(); // save settings
        KeybindConfig?.Dispose(); // save keybinds
        VolumeController?.Dispose();
        if (Drumset?.IsValueCreated == true) Drumset.Value.Dispose();
        DrumMidiHandler.RemoveNoteHandler(command.OnMidiNote);
    }

    bool allowQuit = false;
    protected override bool OnExiting()
    {
        // Exit() does not trigger this method so we don't have to worry about recursion
        if (allowQuit)
        {
            return base.OnExiting();
        }
        else
        {
            // we basically never exit except through ForceQuitGame, which can be triggered through QuitGame
            command.ActivateCommand(Command.QuitGame);
            return true; // aborts this exit
        }
    }

    public override bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
    {
        // alt-f4 doesn't trigger OnExiting, so we have to intercept it here
        // as far as I can tell, alt+f4 is the only thing that triggers this PlatformAction
        if (e.Action == PlatformAction.Exit)
        {
            // this causes the action to be routed through the user's keybinds instead
            // for example, they could unbind alt+f4 in the keybind editor
            // they could also decide to bind alt+f4 to force quit instead
            return false;
        }
        return base.OnPressed(e);
    }

    [CommandHandler]
    public bool DownloadFile(CommandContext context) => context.GetString(FileImporters.DownloadAndOpenFile,
        "Download and Open File", "URL", Util.ShortClipboard);

    [CommandHandler]
    public bool OpenFile(CommandContext context)
    {
        if (context.TryGetParameter(out string path))
        {
            var task = FileImporters.OpenFile(path);
            // if task is completed, we can use the return value. If not, that means we're working async, which is good, so we return true
            return !task.IsCompleted || task.Result;
        }
        return false;
    }

    [CommandHandler]
    public void Screenshot()
    {
        Host.TakeScreenshotAsync().ContinueWith(e =>
        {
            // this runs on a background thread (there's a Task.Run in TakeScreenshotAsync)
            var im = e.Result;

            var directory = FileSystemResources.GetDirectory("screenshots");
            var outputLocation = Path.Join(directory.FullName, $"{DateTime.Now:yyyyMMdd_HH-mm-ss}.png");
            using var output = File.OpenWrite(outputLocation);
            im.SaveAsPng(output);
            Util.CommandController.NewContext().ShowMessage($"Saved to {outputLocation}");

            im.Dispose();
        });
    }

    [CommandHandler]
    public void About()
    {
        var req = Util.Palette.Request(new RequestConfig
        {
            CommitText = null,
            Title = "Drum Game"
        });
        var list = new List<(string, string)>();
        void safeAdd(string name, Func<string> get)
        {
            try
            {
                list.Add((name, get()));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Error getting {name}");
            }
        }
        safeAdd("Version", () => Util.VersionString);
        safeAdd("OS", () => System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        var y = 0;
        foreach (var kv in list)
        {
            req.Add(new SpriteText { Font = FrameworkFont.Regular, Y = y, Text = $"{kv.Item1}: {kv.Item2}" });
            y += 18;
        }
    }
    [CommandHandler]
    public bool ToggleFullscreen(CommandContext context)
    {
        // we ignore repeat keys since toggling fullscreen can temporarily stick keys
        if (Window == null || context.Repeat) return false;
        Window.WindowMode.Value = Window.WindowMode.Value != WindowMode.Fullscreen ?
            WindowMode.Fullscreen : WindowMode.Windowed;
        return true;
    }
    [CommandHandler]
    public bool CycleFrameSync(CommandContext context)
    {
        Trigger(FrameworkAction.CycleFrameSync);
        var sync = FrameworkConfigManager.Get<FrameSync>(FrameworkSetting.FrameSync);
        var syncString = sync == FrameSync.VSync ? "VSync" : sync.ToString().FromPascalCase();
        context.ShowMessage($"Frame sync set to {syncString}");
        return true;
    }
    [CommandHandler] public new void CycleFrameStatistics() => base.CycleFrameStatistics();
    [CommandHandler] public void ToggleLogOverlay() => Trigger(FrameworkAction.ToggleLogOverlay);
    [CommandHandler]
    public bool ToggleExecutionMode(CommandContext context)
    {
        Trigger(FrameworkAction.CycleExecutionMode);
        var mode = FrameworkConfigManager.Get<ExecutionMode>(FrameworkSetting.ExecutionMode);
        context.ShowMessage($"Execution mode set to {mode}");
        return true;
    }
    [CommandHandler]
    public bool MaximizeWindow(CommandContext _)
    {
        if (Window != null && Window.WindowState == osu.Framework.Platform.WindowState.Normal)
        {
            Window.WindowState = osu.Framework.Platform.WindowState.Maximised;
            return true;
        }
        return false;
    }
    [CommandHandler] public void QuitGame() => ForceQuitGame();
    [CommandHandler]
    public void ForceQuitGame()
    {
        allowQuit = true;
        Exit();
    }
    [CommandHandler] public void OpenResourcesFolder() => Host.OpenFileExternally(FileSystemResources.AbsolutePath);
    [CommandHandler] public void OpenLogFolder() => Host.OpenFileExternally(Host.Storage.GetFullPath("logs"));
    [CommandHandler] public void OpenDrumGameDiscord() => Host.OpenUrlExternally("https://discord.gg/RTc3xDKabU");
    [CommandHandler] public void ForceGarbageCollection() => GC.Collect();
    [CommandHandler] public void InputOffsetWizard() => command.Palette.Push<InputOffsetWizard>();
    [CommandHandler] public void EnableDebugLogging() => Logger.Level = LogLevel.Debug;
    [CommandHandler]
    public void EnableDebugAndPerformanceLogging()
    {
        EnableDebugLogging();
        Host.PerformanceLogging.Value = true;
    }
    protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        => dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
    protected override UserInputManager CreateUserInputManager() => Util.InputManager = InputManager = new DrumInputManager();

    public void Trigger(FrameworkAction action) => OnPressed(new KeyBindingPressEvent<FrameworkAction>(InputManager.CurrentState, action));

    [CommandHandler]
    public void ReloadKeybindsFromFile()
    {
        // this method is sketchy, but it definitely should work
        // in testing it took ~1ms the first reload, but then takes 0.25ms after the first reload
        command.ReRegister();
        KeybindConfig.Reload();
    }

    [CommandHandler] public void SubmitFeedback() => Host.OpenUrlExternally("https://github.com/Jumprocks1/drum-game/issues");
    [CommandHandler] public void Help() => Util.Palette.Palette.ShowAvailableCommands();
    [CommandHandler]
    public void ToggleScreencastMode()
    {
        if (RemoveAll(e => e is KeyPressOverlay, true) == 0)
            Add(new KeyPressOverlay());
    }

    [CommandHandler]
    public bool SetWindowSize(CommandContext context)
    {
        var currentSize = Window.ClientSize;
        context.Palette.RequestString("Window Size", "Resolution", $"{currentSize.Width}x{currentSize.Height}", e =>
        {
            var spl = e.Split('x');
            var x = 0;
            var y = 0;
            if (spl.Length == 1)
            {
                if (!int.TryParse(spl[0], out x)) return;
                y = x * 9 / 16;
            }
            else if (spl.Length == 2)
            {
                if (!int.TryParse(spl[0], out x) || !int.TryParse(spl[1], out y))
                    return;
            }
            if (x > 0 && y > 0)
            {
                Window.WindowMode.Value = WindowMode.Windowed;
                Window.WindowState = osu.Framework.Platform.WindowState.Normal;
                var bind = FrameworkConfigManager.GetBindable<System.Drawing.Size>(FrameworkSetting.WindowedSize);
                bind.Value = new System.Drawing.Size(x, y);
            }
        });
        return true;
    }

    protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults()
    {
        // Disabling these here instead of in SetHost saves us at least 100ms
        // It's sketchy because we need Util.Host to be assigned, but this really is the best way
        //    to make sure these are disabled before they are initialized
        foreach (var input in Util.Host.AvailableInputHandlers)
        {
            if (input is not MouseHandler and not KeyboardHandler)
                input.Enabled.Value = false;
        }

        return new Dictionary<FrameworkSetting, object> {
            { FrameworkSetting.FrameSync, FrameSync.VSync }
        };
    }
}

