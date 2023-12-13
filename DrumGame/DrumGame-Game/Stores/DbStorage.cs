using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DrumGame.Game.Stores.DB;

namespace DrumGame.Game.Stores;

public class DbStorage
{

    static List<Action<DrumDbContext>> queue = new();
    public static void Queue(Action<DrumDbContext> action)
    {
        bool run = false;
        lock (queue)
        {
            if (queue.Count == 0) run = true;
            queue.Add(action);
        }
        if (run)
        {
            Task.Run(() =>
            {
                var i = 0;
                while (true)
                {
                    lock (queue)
                    {
                        if (i >= queue.Count)
                        {
                            queue.Clear();
                            break;
                        }
                    }
                    using (var db = Utils.Util.GetDbContext())
                    {
                        queue[i](db);
                    }
                    i += 1;
                }
            });
        }
    }

    readonly string path;
    object _creationLock = new();

    bool migrated = false;

    // this is basically instant except on migration, which should be rare
    // EF recommends creating a context for each unit of work
    // Make sure to dispose when done
    public DrumDbContext GetContext()
    {
        lock (_creationLock)
        {
            if (migrated)
                return new DrumDbContext($"Data Source={path}");
            migrated = true;
            return new DrumDbContextFactory(path).CreateDbContext();
        }
    }
    public DbStorage(FileSystemResources resources, string name)
    {
        path = resources.GetAbsolutePath(name);
    }
}
