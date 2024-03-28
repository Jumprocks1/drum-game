using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Abstract;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;

namespace DrumGame.Game.Containers;

public class EventContainer : AdjustableSkinElement
{
    public override ref AdjustableSkinData SkinPath => ref Util.Skin.Notation.EventContainer;
    public override AdjustableSkinData DefaultData() => new()
    {
        Origin = Anchor.BottomLeft,
        Anchor = Anchor.TopLeft,
        AnchorTarget = SkinAnchorTarget.SongInfoPanel
    };
    public static readonly FontUsage Font = FrameworkFont.Regular.With(size: 14);
    public const int EventBuffer = 8;
    // if we need more detailed storage we can make custom class over SpriteText
    Queue<SpriteText> events = new(EventBuffer);
    public const int EventHeight = 16;
    FadeContainer Container;
    public EventContainer()
    {
        AddInternal(Container = new()
        {
            AutoSizeAxes = Axes.Both,
            Padding = new MarginPadding { Left = 2 },
            BackgroundColour = Colour4.Transparent
        });
        AutoSizeAxes = Axes.Both;
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
        if (added.Parent == null) Container.Add(added);
        added.Y = 0;
        events.Enqueue(added);
        Touch();
    }

    [CommandHandler(Command.ShowEventLog)]
    public void Touch() => Container.Touch();

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
    public static implicit operator EventLog(string description) => new(description);
}
