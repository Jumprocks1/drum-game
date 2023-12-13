using System;
using System.Collections.Generic;
using DrumGame.Game.Commands;
using DrumGame.Game.Components.Abstract;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;

namespace DrumGame.Game.Containers;

public class EventContainer : FadeContainer
{
    public override Colour4 BackgroundColour => Colour4.Transparent;
    public static readonly FontUsage Font = FrameworkFont.Regular.With(size: 14);
    public const int EventBuffer = 8;
    // if we need more detailed storage we can make custom class over SpriteText
    Queue<SpriteText> events = new Queue<SpriteText>(EventBuffer);
    public const int EventHeight = 16;
    public EventContainer()
    {
        AutoSizeAxes = Axes.Both;
        Padding = new MarginPadding { Left = 2 };
    }
    public void Add(EventLog log)
    {
        SpriteText added;
        if (events.Count == EventBuffer)
        {
            added = events.Dequeue();
        }
        else
        {
            added = new SpriteText
            {
                Colour = Util.Skin.Notation.NotationColor,
                Anchor = Anchor.BottomLeft,
                Height = EventHeight,
                Origin = Anchor.BottomLeft,
                Font = Font
            };
        }
        var message = $"{log.Time:HH:mm:ss}  {log.Description}";
        Logger.Log(log.Description, level: log.Level); // don't need to log time since it's included in Logger.Log
        added.Text = message;
        foreach (var d in events)
        {
            d.Y -= EventHeight;
        }
        if (added.Parent == null) AddInternal(added);
        added.Y = 0;
        events.Enqueue(added);
        Touch();
    }

    [Resolved] CommandController CommandController { get; set; }
    [CommandHandler(Command.ShowEventLog)]
    public override void Touch() => base.Touch();

    [BackgroundDependencyLoader]
    private void load()
    {
        CommandController.RegisterHandlers(this);
    }
    protected override void Dispose(bool isDisposing)
    {
        CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }
}

public class EventLog
{
    public string Description;
    public DateTime Time;
    public LogLevel Level;
    public EventLog(string description, DateTime? time = null, LogLevel level = LogLevel.Verbose)
    {
        Description = description;
        Time = time ?? DateTime.Now;
        Level = level;
    }
    public static implicit operator EventLog(string description) => new EventLog(description);
}
