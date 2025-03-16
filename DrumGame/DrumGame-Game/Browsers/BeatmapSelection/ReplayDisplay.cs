using System;
using System.Collections.Generic;
using System.Globalization;
using DrumGame.Game.Beatmaps.Display.ScoreDisplay;
using DrumGame.Game.Commands.Requests;
using DrumGame.Game.Components;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Modifiers;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace DrumGame.Game.Browsers.BeatmapSelection;

public class ReplayDisplay : CompositeDrawable, IHasMarkupTooltip, IHasContextMenu, IHasCursor
{
    public SDL2.SDL.SDL_SystemCursor? Cursor => SDL2.SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND;
    FontUsage Font => FrameworkFont.Regular;
    FontUsage FontCondensed => FrameworkFont.Condensed;
    public new const float Height = 48;
    public ReplayInfo ReplayInfo;
    static DateTimeFormatInfo Formats => CultureInfo.InstalledUICulture.DateTimeFormat;
    public string MarkupTooltip
    {
        get
        {
            var time = ReplayInfo.CompleteTimeLocal;
            var res = $"{time.ToString(Formats.ShortDatePattern)} {time.ToString(Formats.LongTimePattern).Replace('\u202F', ' ')}\n" +
                $"<perfect>P</c>:{ReplayInfo.Perfect} <good>G</c>:{ReplayInfo.Good} <bad>B</c>:{ReplayInfo.Bad} <miss>M</c>:{ReplayInfo.Miss}\n" +
                $"Accuracy: {ReplayInfo.AccuracyNoLeading}\n" +
                $"Hit windows: {ReplayInfo.HitWindows ?? "Standard"}";
            if (ParsedMods != null)
                foreach (var mod in ParsedMods)
                    res += '\n' + mod.MarkupDisplay;
            return res;
        }
    }

    public MenuItem[] ContextMenuItems => ContextMenuBuilder.New(ReplayInfo)
        .Add("Delete", Delete).Danger()
        .Add("View File", replay =>
        {
            Util.Resources.Storage.PresentFileExternally(replay.Path);
        }).Disabled(!ReplayInfo.Exists)
        .Build();

    public void Delete(ReplayInfo replay)
    {
        Util.Palette.Push(new ConfirmationModal(() =>
        {
            if (Util.Resources.Exists(replay.Path))
                Util.Resources.Storage.Delete(replay.Path);
            using var context = Util.GetDbContext();
            context.Replays.Remove(replay);
            context.SaveChanges();
            Util.GetParent<BeatmapDetailLoader>(this)?.RefreshReplays();
        }, "Do you want to delete this replay?"));
    }

    public ReplayDisplay(ReplayInfo info)
    {
        RelativeSizeAxes = Axes.X;
        ReplayInfo = info;
        base.Height = Height;
    }

    List<BeatmapModifier> ParsedMods;

    Box box;

    [BackgroundDependencyLoader]
    private void load()
    {
        // please note, this loads on a background thread, so performance isn't even super important
        // still, the average replay display takes less than 0.1ms to load
        AddInternal(box = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = Colour4.White,
            Alpha = 0.05f
        });
        var accuracy = new SpriteText
        {
            Font = Font.With(size: 35),
            Text = ReplayInfo.AccuracyNoLeading,
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            X = -6
        };
        AddInternal(accuracy);
        MarkupText markupText = null;
        void AddModText(string text, Action<SpriteText> creationParameters)
        {
            if (markupText == null)
            {
                AddInternal(markupText = new MarkupText(e => e.Font = FontCondensed.With(size: 14))
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomCentre,
                    X = -6 - accuracy.Width / 2,
                });
            }
            else markupText.AddText(" ");
            markupText.AddText(text, creationParameters);
        }
        ParsedMods = ReplayInfo.ParseMods();
        if (ParsedMods != null)
        {
            foreach (var mod in ParsedMods)
                AddModText(mod.AbbreviationMarkup, null);
        }
        if (ReplayInfo.PlaybackSpeed != 1)
        {
            // clamp is to prevent even smaller speeds (0.25x) from changing hue
            // speed range should be 0.5x to 2x
            var log2 = Math.Clamp(Math.Log2(ReplayInfo.PlaybackSpeed), -1, 1);
            // log2 now has a range of 2
            // we want 0.5x (-1) => 0.66666 hue (blue), and 2x (1) => 0 hue (red)
            // simple linear transform to fit the desired range
            var hue = (log2 - 1) / -3;
            // saturation should be 0 when we are super close to 1x, don't exceed 0.5 since it's hard to read
            // for reference, abs(log2) = 0.5 at 0.71x and 1.41x
            // this basically replaces the green hue with white
            var saturation = Math.Min(0.5, Math.Abs(log2));
            var speedColor = Colour4.FromHSV((float)hue, (float)saturation, 1f);
            AddModText(ReplayInfo.PlaybackSpeed.ToString("0.00x"), e => e.Colour = speedColor);
        }
        AddInternal(new SpriteText
        {
            Text = ReplayInfo.CompleteTimeLocal.ToString(Formats.ShortDatePattern),
            Font = FontCondensed.With(size: 21),
            Y = 2,
            X = 4
        });
        AddInternal(new FillFlowContainer
        {
            Direction = FillDirection.Horizontal,
            AutoSizeAxes = Axes.Both,
            Children = new Drawable[]{
                new SpriteText
                {
                    Text = $"Score: {ReplayInfo.Score:N0} ({ReplayInfo.MaxCombo})",
                    Font = FontCondensed.With(size: 21),
                },
                new SpriteText
                {
                    Text = $"  x{ReplayInfo.Miss}",
                    Font = FontCondensed.With(size: 21),
                    Colour= Util.HitColors.Miss
                }
            },
            Y = 25,
            X = 4
        });
    }
    protected override bool OnClick(ClickEvent e)
    {
        if (e.Button == MouseButton.Left)
            Util.Palette.Push(new EndScreen(ReplayInfo));
        return true;
    }

    protected override bool OnHover(HoverEvent e)
    {
        box.FadeTo(0.1f, 200);
        return base.OnHover(e);
    }
    protected override void OnHoverLost(HoverLostEvent e)
    {
        box.FadeTo(0.05f, 200);
        base.OnHoverLost(e);
    }
}