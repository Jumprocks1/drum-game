using System.IO;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Browsers;
using DrumGame.Game.Commands;
using DrumGame.Game.IO;
using DrumGame.Game.Utils;
using DrumGame.Game.Views;
using osu.Framework.Allocation;

namespace DrumGame.Game;

public class DrumGameGame : DrumGameGameBase
{
    public BeatmapSelectorLoader Loader;
    [BackgroundDependencyLoader]
    private void load()
    {
        command.RegisterHandlers(this);
        Add(new CommandPaletteContainer());

        // If MapStorage is null, then we failed to load resources
        // If that is the case, we should show an error splash screen here
        if (MapStorage != null)
        {
            Add(Loader = new BeatmapSelectorLoader());
        }
        else
        {
            Add(new SplashScreen("Failed to locate folder: `resources`. Please repair installation."));
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        command.RemoveHandlers(this);
        Importer?.Dispose();
        Importer = null;
        base.Dispose(isDisposing);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        Util.CheckStartDuration();

        // using var db = Util.GetDbContext();
        // var replays = db.Replays.ToList();
        // var totalTime = 0d;
        // var maps = Util.MapStorage.GetAllMetadata().ToDictionary(e => e.Value.Id, e => e.Value);
        // var earliest = DateTimeOffset.MaxValue;

        // var cutoff = new DateTime(2023, 1, 1);

        // foreach (var replay in replays)
        // {
        //     if (replay.StartTime == DateTimeOffset.MinValue) continue;
        //     if (replay.CompleteTime < cutoff) continue;
        //     var mapId = replay.MapId;
        //     if (maps.TryGetValue(mapId, out var map))
        //     {
        //         if (replay.CompleteTime < earliest)
        //             earliest = replay.CompleteTime;
        //         var mapDuration = TimeSpan.FromSeconds(map.Duration);
        //         if (replay.StartTime != DateTimeOffset.MinValue)
        //         {
        //             var replayTime = replay.CompleteTime - replay.StartTime;
        //             if (replayTime > TimeSpan.Zero && replayTime < mapDuration * 2)
        //                 totalTime += replayTime.TotalMilliseconds;
        //             else
        //                 totalTime += map.Duration;
        //             if (replayTime > mapDuration)
        //             {
        //                 Console.WriteLine($"{replay.MapId} {replayTime} {map.Title} {replay.StartTime}");
        //             }
        //         }
        //         else
        //         {
        //             totalTime += map.Duration;
        //         }
        //     }
        // }

        // var hours = totalTime / 1000 / 60 / 60;
        // Console.WriteLine($"Total hours: {hours:0.00} - started tracking: {earliest.LocalDateTime}");
        // Console.WriteLine($"Per day: {hours / (DateTimeOffset.Now - earliest).TotalDays * 60:0.00}min");


        // this adds about 3ms to our startup time if enabled
        // if we don't want that, we can consider only starting it up after we download something
        SetupImportWatcher();
    }

    ImportWatcher Importer;
    public void SetupImportWatcher()
    {
        var watch = Util.ConfigManager.Get<bool>(Stores.DrumGameSetting.WatchImportFolder);
        if (!watch || Util.Resources == null) return;
        Importer ??= new ImportWatcher();
        Importer.Init();
    }

    [CommandHandler]
    public bool CreateNewBeatmap(CommandContext context) => context.GetString(name =>
        {
            var o = Beatmap.Create();
            o.Source = new BJsonSource(MapStorage.GetFullPath(name + ".bjson"));
            if (File.Exists(o.Source.AbsolutePath))
            {
                context.ShowMessage("A map with that name already exists");
                return;
            }
            o.SaveToDisk(MapStorage);
            context.ActivateCommand(Command.Refresh);
            context.ActivateCommand(Command.JumpToMap, name);
        }, "Creating New Beatmap", "File name", "map");
}

