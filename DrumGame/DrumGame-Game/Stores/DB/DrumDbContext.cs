using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using osu.Framework.Logging;

namespace DrumGame.Game.Stores.DB;

public class DrumDbContextDesignFactory : IDesignTimeDbContextFactory<DrumDbContext>
{
    // we should never write to this since the design database will be mostly ignored
    public DrumDbContext CreateDbContext(string[] args) => new DrumDbContext($"Data Source=design.db");
}
public class DrumDbContextFactory : IDbContextFactory<DrumDbContext>
{
    readonly string fullPath;
    public DrumDbContextFactory(string fullPath) { this.fullPath = fullPath; }
    public DrumDbContext CreateDbContext()
    {
        var context = new DrumDbContext($"Data Source={fullPath}");
        try
        {
            // this takes ~440ms, but if it's skipped, the first query to the database takes ~580ms instead of ~170ms
            // no matter what, SQLLite always has a slow startup
            context.Database.Migrate();
        }
        catch (Exception e)
        {
            Logger.Error(e.InnerException ?? e, "Migration failed! We'll be starting with a fresh database.", LoggingTarget.Database);
            var newPath = Path.Join(Path.GetDirectoryName(fullPath),
                $"{Path.GetFileNameWithoutExtension(fullPath)}_{DateTime.UtcNow:yyyyMMddHHmmss}.db");
            Logger.Log($"Copying old database to {newPath}", LoggingTarget.Database, level: LogLevel.Important);
            File.Copy(fullPath, newPath);
            context.Database.EnsureDeleted();
            Logger.Log("Database purged successfully.", LoggingTarget.Database, level: LogLevel.Important);
            context.Database.Migrate();
        }
        return context;
    }
}
public class DrumDbContext : DbContext
{
    public DbSet<ReplayInfo> Replays { get; set; }
    public DbSet<BeatmapInfo> Beatmaps { get; set; }

    public readonly string ConnectionString;
    public DrumDbContext(string connectionString)
    {
        ConnectionString = connectionString;
    }
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite(ConnectionString);

    public BeatmapInfo GetOrAddBeatmap(string id)
    {
        var existing = Beatmaps.FirstOrDefault(e => e.Id == id);
        if (existing == null)
            Beatmaps.Add(existing = new BeatmapInfo(id));
        return existing;
    }
    public BeatmapInfo GetOrAddBeatmap(string id, Dictionary<string, BeatmapInfo> lookup)
    {
        if (lookup.TryGetValue(id, out var o))
            return o;
        var b = new BeatmapInfo(id);
        Beatmaps.Add(b);
        return b;
    }

    public void SyncWith(DrumDbContext remoteContext, Browsers.BeatmapSelector.SyncOptions syncType)
    {
        lock (this)
        {
            lock (remoteContext)
            {
                remoteContext.Database.Migrate();
                if (syncType == Browsers.BeatmapSelector.SyncOptions.Database)
                {
                    // this might get kinda slow eventually

                    var localReplays = Replays.Select(e => new { e.CompleteTimeTicks, e.MapId }).AsEnumerable()
                        .Select(e => (e.CompleteTimeTicks, e.MapId)).ToHashSet();
                    var missing = remoteContext.Replays.Select(e => new { e.CompleteTimeTicks, e.MapId, e.Id })
                        .AsEnumerable()
                        .Where(e => !localReplays.Contains((e.CompleteTimeTicks, e.MapId)))
                        .Select(e => e.Id)
                        .ToList();
                    var replays = remoteContext.Replays.Where(e => missing.Contains(e.Id)).ToList();
                    foreach (var replay in replays.OrderBy(e => e.CompleteTimeTicks))
                    {
                        replay.Id = 0;
                        Replays.Add(replay);
                    }
                    var playTimes = remoteContext.Beatmaps.Select(e => new { e.Id, e.PlayTime }).ToDictionary(e => e.Id, e => e.PlayTime);
                    foreach (var (id, playTime) in playTimes)
                    {
                        var beatmap = GetOrAddBeatmap(id);
                        beatmap.PlayTime = Math.Max(beatmap.PlayTime, playTime);
                    }
                    SaveChanges();
                    ChangeTracker.Clear();
                    Logger.Log($"Copied summary data for {replays.Count} replays", level: LogLevel.Important);
                }
                else if (syncType == Browsers.BeatmapSelector.SyncOptions.OverwriteRatings)
                {
                    var remoteRatings = remoteContext.Beatmaps.Select(e => new { e.Rating, e.Id });
                    var localMaps = Beatmaps.ToDictionary(e => e.Id);
                    foreach (var remote in remoteRatings)
                    {
                        var beatmap = GetOrAddBeatmap(remote.Id, localMaps);
                        beatmap.Rating = remote.Rating;
                    }
                    SaveChanges();
                    ChangeTracker.Clear();
                }
            }
        }
    }
}

// Idk what this was for
// public class MemoryStreamDbContext : DrumDbContext
// {
//     public MemoryStreamDbContext() : base("Data Source=:memory:") { }
//     protected override void OnConfiguring(DbContextOptionsBuilder options)
//         => options.UseSqlite();
// }