using System;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;

namespace DrumGame.Game.Commands;

public class FileRequest : RequestModal
{
    Action<string> Callback;

    public FileRequest(string title, string description, Action<string> callback) : base(title, description)
    {
        Callback = callback;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Util.CommandController.RegisterHandlers(this);
    }
    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }

    [CommandHandler]
    public bool OpenFile(CommandContext context)
    {
        if (context.TryGetParameter(out string path))
        {
            ScheduleAfterChildren(() => Callback(path));
            return true;
        }
        return false;
    }
}
