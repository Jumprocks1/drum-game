using osu.Framework.Graphics;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Components;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Sprites;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Commands;
using osu.Framework.Input.Events;
using osuTK.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Bindables;
using DrumGame.Game.Skinning;
using DrumGame.Game.Beatmaps.Display.Mania;

namespace DrumGame.Game.Beatmaps.Practice;

public class PracticeInfoPanel : AdjustableSkinElement, IHasCommand
{
    public override ref AdjustableSkinData SkinPath
    {
        get
        {
            if (Mania) return ref Util.Skin.Mania.PracticeInfoPanel;
            return ref Util.Skin.Notation.PracticeInfoPanel;
        }
    }
    public Command Command => Command.PracticeMode;

    public PracticeMode.PracticeConfig Config => PracticeMode.Config;

    public override AdjustableSkinData DefaultData() => Mania ? new()
    {
        Origin = Anchor.BottomRight,
        Anchor = Anchor.BottomLeft,
        AnchorTarget = SkinAnchorTarget.LaneContainer
    } : new()
    {
        Anchor = Anchor.BottomLeft,
        Y = -BeatmapTimeline.Height - MusicNotationBeatmapDisplay.ModeTextHeight - SongInfoPanel.DefaultHeight
    };

    Box Hover;
    SpriteText Rate;
    SpriteText Range;
    SpriteText Streak;
    Box StreakIndicator;
    const float Column = 50;

    public readonly PracticeMode PracticeMode;
    bool Mania => PracticeMode.Display is ManiaBeatmapDisplay;

    Colour4 FontColor = Util.Skin.Notation.NotationColor;

    public PracticeInfoPanel(PracticeMode practiceMode) : base(true)
    {
        PracticeMode = practiceMode;
        InitializeSkinData();
        Height = 80;
        Width = 200;
        var y = 0;
        AddInternal(new SpriteText
        {
            Text = "Practice Mode (click to configure)",
            Font = FrameworkFont.Regular.With(size: 16),
            Colour = FontColor
        });
        y += 16;
        var rowHeight = 14;
        SpriteText AddProperty(string name)
        {
            AddInternal(new SpriteText
            {
                Text = name + ':',
                Font = FrameworkFont.Regular.With(size: rowHeight),
                Y = y,
                Colour = FontColor
            });
            var res = new SpriteText
            {
                Font = FrameworkFont.Regular.With(size: rowHeight),
                X = Column,
                Y = y,
                Colour = FontColor
            };
            AddInternal(res);
            y += rowHeight;
            return res;
        }
        AddInternal(new CommandIconButton(Command.DecreasePlaybackSpeed, FontAwesome.Solid.Minus, 14)
        {
            Y = y,
            X = Column + 30,
            Colour = Colour4.Black
        });
        AddInternal(new CommandIconButton(Command.IncreasePlaybackSpeed, FontAwesome.Solid.Plus, 14)
        {
            Y = y,
            X = Column + 30 + 18,
            Colour = Colour4.Black
        });
        Rate = AddProperty("Speed");
        Range = AddProperty("Range");
        AddInternal(StreakIndicator = new Box
        {
            Y = y,
            X = Column + 30,
            Width = rowHeight,
            Height = rowHeight
        });
        Streak = AddProperty("Streak");
        AddInternal(Hover = new Box
        {
            Alpha = 0,
            RelativeSizeAxes = Axes.Both,
            Colour = Color4.White.Opacity(.1f),
            Blending = BlendingParameters.Additive
        });
        PracticeMode.Player.Track.PlaybackSpeed.BindValueChanged(RateChanged, true);
        UpdateText();
    }

    void RateChanged(ValueChangedEvent<double> _) => Rate.Text = $"{PracticeMode.Player.Track.Rate * 100:0}%";

    public void UpdateText()
    {
        Range.Text = $"{PracticeMode.StartBeat} - {PracticeMode.EndBeat}";
        Streak.Text = $"{PracticeMode.Streak} / {Config.MinimumStreak}";
        StreakIndicator.Colour = PracticeMode.Streak switch
        {
            > 0 => Util.Skin.HitColors.Good,
            < 0 => Util.Skin.HitColors.Miss,
            _ => Colour4.Transparent,
        };
    }


    protected override void Dispose(bool isDisposing)
    {
        PracticeMode.Player.Track.PlaybackSpeed.ValueChanged -= RateChanged;
        base.Dispose(isDisposing);
    }


    protected override bool OnHover(HoverEvent e)
    {
        Hover.FadeIn();
        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        base.OnHoverLost(e);
        Hover.FadeOut();
    }
}