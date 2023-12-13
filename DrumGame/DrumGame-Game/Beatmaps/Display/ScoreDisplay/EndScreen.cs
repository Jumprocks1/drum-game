using System;
using DrumGame.Game.Beatmaps.Replay;
using DrumGame.Game.Browsers;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Modals;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.DB;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;

namespace DrumGame.Game.Beatmaps.Display.ScoreDisplay;

public class EndScreen : ModalBase, IModal
{
    [Resolved] CommandController Command { get; set; }
    [Resolved] MapStorage MapStorage { get; set; }
    [Resolved] FileSystemResources Resources { get; set; }
    readonly ReplayInfo Info;
    // ReplayInfo should not change after getting here
    BeatmapReplay Replay;
    Beatmap _beatmap;
    Beatmap Beatmap => _beatmap ??= Player?.Beatmap ?? MapStorage.LoadMapFromId(Info.MapId);
    readonly BeatmapPlayer Player;
    bool replaySaved = false;
    public EndScreen(ReplayInfo info, BeatmapReplay replay = null, BeatmapPlayer player = null)
    {
        Player = player;
        RelativeSizeAxes = Axes.Both;
        Info = info;
        Replay = replay;
    }

    public Action CloseAction { get; set; }
    void Close() => CloseAction?.Invoke();

    [BackgroundDependencyLoader]
    private void load()
    {
        Command.RegisterHandlers(this);
        AddInternal(new ModalBackground(Close));

        var bodyOuter = new ClickBlockingContainer
        {
            Width = 0.85f,
            Height = 0.8f,
            Alpha = 0.95f,
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre
        };
        void AddSaveText()
        {
            bodyOuter.Add(new SpriteText
            {
                Text = "Full replay data saved",
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Y = -5,
                X = 5,
                Font = FrameworkFont.Regular.With(size: 20)
            });
        }
        void SaveReplay(bool addText = true)
        {
            if (replaySaved) return;
            if (Player != null && Player.OpenMode == BeatmapOpenMode.Record)
            {
                var audioRecorder = Player.Loader?.AudioRecorder;
                if (audioRecorder != null)
                    audioRecorder.WriteToFile(Info.StartTime.UtcDateTime, Resources.GetDirectory("recordings").FullName);
            }
            Replay.Save(Resources, Info.Path);
            replaySaved = true;
            if (addText) AddSaveText();
        }

        AddInternal(bodyOuter);
        bodyOuter.Add(new Box
        {
            Colour = DrumColors.DarkBackgroundAlt,
            RelativeSizeAxes = Axes.Both
        });

        var mapMetadata = MapStorage.GetMetadataFromId(Info.MapId) ?? new BeatmapMetadata { };

        bodyOuter.Add(new AutoSizeSpriteText
        {
            Text = $"{mapMetadata.Title}",
            MaxSize = 80,
            Font = FrameworkFont.Regular,
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Padding = new MarginPadding { Horizontal = 40 }
        });

        var replayOnDisk = Resources.Exists(Info.Path);
        replaySaved = replayOnDisk;
        if (replayOnDisk)
            Util.CommandController.RegisterHandler(Commands.Command.RevealInFileExplorer, RevealInFileExplorer);

        if (Replay != null || replayOnDisk)
        {
            Replay ??= Info.LoadReplay(); // if Replay is null but the file exists, then we can load the json file
            bodyOuter.Add(new DrumButton
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Action = () =>
                {
                    if (Replay != null) SaveReplay(false);
                    (Util.DrumGame as DrumGameGame).Loader.LoadReplay(Info, Replay);
                    Close();
                },
                Text = "Watch Replay",
                Height = 30,
                Width = 120,
                X = -5,
                Y = -5
            });
            bodyOuter.Add(new AccuracyPlot(Replay, Info, Beatmap)
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = 0
            });
            bodyOuter.Add(new ErrorHistogram(Replay, Info, Beatmap)
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = 303
            });
        }

        if (Replay != null && !replayOnDisk)
        {
            if (Util.ConfigManager.Get<bool>(DrumGameSetting.SaveFullReplayData)) SaveReplay();
            if (!replaySaved)
            {
                DrumButton saveButton = null;
                saveButton = new DrumButton
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Action = () =>
                    {
                        SaveReplay();
                        bodyOuter.Remove(saveButton, true);
                        saveButton = null;
                    },
                    Text = "Save Replay",
                    Height = 30,
                    Width = 120,
                    X = -130,
                    Y = -5
                };
                bodyOuter.Add(saveButton);
            }
            else AddSaveText();
        }

        var statX = 10f;
        var statY = 80 + 10;

        void AddStat(string label, string value, float width, Colour4? color = null, string tooltip = null)
        {
            bodyOuter.Add(new Stat(label, value, color, tooltip)
            {
                X = statX,
                Y = statY,
                Width = width
            });
            statX += width;
        }

        AddStat("Accuracy", Info.AccuracyNoLeading, 150);
        AddStat("Max Combo", Info.MaxCombo.ToString(), 150, tooltip: Info.NotePercent(Info.MaxCombo));
        AddStat("Score", Info.Score.ToString(), 150);

        statX = 10;
        statY += 60;
        AddStat("Perfect", Info.Perfect.ToString(), 150, Util.HitColors.Perfect, Info.NotePercent(Info.Perfect));
        AddStat("Good", Info.Good.ToString(), 150, Util.HitColors.Good, Info.NotePercent(Info.Good));
        AddStat("Bad", Info.Bad.ToString(), 150, Util.HitColors.Bad, Info.NotePercent(Info.Bad));
        AddStat("Miss", Info.Miss.ToString(), 150, Util.HitColors.Miss, Info.NotePercent(Info.Miss));
    }
    public void RevealInFileExplorer() => Util.RevealInFileExplorer(Util.Resources.GetAbsolutePath(Info.Path));

    [CommandHandler]
    public bool GenerateThumbnail(CommandContext context)
    {
        if (Beatmap.Image == null) return false;
        void HandleImage()
        {
            var image = Beatmap.Image;
            if (image == null) return;
            AddInternal(new Sprite
            {
                Texture = Resources.LargeTextures.Get(Beatmap.FullAssetPath(image)),
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fit,
                Height = 1,
                Width = 1
            });
        }
        HandleImage();
        return false;
    }

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandler(Commands.Command.RevealInFileExplorer, RevealInFileExplorer);
        Command.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }
    public void FadeIn() => this.FadeIn(1200);


    class Stat : CompositeDrawable, IHasTooltip
    {
        readonly string tooltip;
        public LocalisableString TooltipText => tooltip;
        public Stat(string label, string value, Colour4? color = null, string tooltip = null)
        {
            Height = 50;
            this.tooltip = tooltip;
            var l = new SpriteText
            {
                Text = label,
                Font = FrameworkFont.Regular.With(size: 20, weight: "Bold"),
                Origin = Anchor.TopCentre,
                Anchor = Anchor.TopCentre
            };
            if (color.HasValue) l.Colour = color.Value;
            AddInternal(l);
            AddInternal(new SpriteText
            {
                Text = value,
                Font = FrameworkFont.Regular.With(size: 30),
                Origin = Anchor.TopCentre,
                Anchor = Anchor.TopCentre,
                Y = 20,
            });
        }
    }
}