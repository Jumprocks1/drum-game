using System;
using System.IO;
using DrumGame.Game.Browsers.BeatmapSelection;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Containers;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace DrumGame.Game.Browsers;

public class BeatmapCard : CompositeDrawable, IHasContextMenu
{
    public MenuItem[] ContextMenuItems => ContextMenuBuilder.New(Map)
        .Add("Play", e => carousel.Selector.SelectMap(e, true)).Color(DrumColors.BrightGreen)
        .Add("Edit", e => carousel.Selector.EditMap(e)).Color(DrumColors.BrightYellow)
        .Add(Commands.Command.EditBeatmapMetadata).Color(DrumColors.BrightYellow)
        .Add(Commands.Command.AddToCollection)
        .Add(Util.CommandController.GetParameterCommand(Commands.Command.AddToCollection, "Favorites"))
        .Add(Commands.Command.UpvoteMap).Color(DrumColors.Upvote)
        .Add(Commands.Command.DownvoteMap).Color(DrumColors.Downvote)
        .Add(Commands.Command.RevealInFileExplorer)
        .Add("Reveal Audio In File Explorer", e =>
        {
            var audio = e.LoadedMetadata.Audio;
            Util.RevealInFileExplorer(Util.DrumGame.MapStorage.GetFullPath(audio));
        })
        .Add(Commands.Command.RemoveFromCollection).Disabled(carousel.Selector.State.Collection == null)
        .Add(Commands.Command.DeleteMap).Danger()
        .Build();
    public Colour4 DifficultyColor(BeatmapDifficulty difficulty) => difficulty switch
    {
        BeatmapDifficulty.ExpertPlus => DrumColors.ExpertPlus,
        BeatmapDifficulty.Expert => DrumColors.Expert,
        BeatmapDifficulty.Insane => DrumColors.Insane,
        BeatmapDifficulty.Hard => DrumColors.Hard,
        BeatmapDifficulty.Normal => DrumColors.Normal,
        BeatmapDifficulty.Easy => DrumColors.Easy,
        _ => Colour4.White
    };
    public new const float Height = 80;
    public new const float Width = 520;
    public new const float Margin = 5f;
    const float Spacing = 4;
    const float Indent = 8;
    SpriteText titleText;
    SpriteText artistText;
    TooltipSpriteText durationText;
    DelayedImage image;
    VoteDisplay ratingText;
    FillFlowContainer mappingText;
    Box background;
    BeatmapCarousel carousel;

    public void SetX(float x)
    {
        X = x;
        var textX = Math.Min(0, -x);
        ratingText.X = textX;
        durationText.X = textX - 4;
    }

    public BeatmapCard(BeatmapCarousel carousel)
    {
        this.carousel = carousel;
        base.Height = Height;
        base.Width = Width;
        background = new Box
        {
            RelativeSizeAxes = Axes.Both,
        };
        UpdateBackground();
        AddInternal(background.WithEffect(new EdgeEffect
        {
            CornerRadius = 2,
            Parameters = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Colour = DrumColors.LightBorder,
                Radius = 8f
            }
        }));
        var leftTextMargin = Spacing + Height; // use to indent text next to image
        AddInternal(titleText = new SpriteText
        {
            X = leftTextMargin,
            Y = Spacing,
            Font = FrameworkFont.Regular.With(size: 25) // should try to auto fit this size (with a max)
        });
        AddInternal(artistText = new SpriteText
        {
            X = Indent + leftTextMargin,
            Y = Spacing * 1.5f + 25,
            Font = FrameworkFont.Regular.With(size: 18),
            Truncate = true,
        });
        AddInternal(durationText = new TooltipSpriteText
        {
            Y = Spacing * 1.5f + 25,
            Font = FrameworkFont.Regular.With(size: 18),
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight
        });
        AddInternal(mappingText = new FillFlowContainer
        {
            X = Indent + leftTextMargin,
            Y = Spacing * 2f + 25 + 18,
            Direction = FillDirection.Horizontal
        });
        AddInternal(ratingText = new VoteDisplay
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight
        });
        AddInternal(image = new DelayedImage
        {
            Width = Height,
            Height = Height
        });
    }
    bool hover = false;
    protected override bool OnHover(HoverEvent e)
    {
        hover = true;
        UpdateBackground();
        return base.OnHover(e);
    }
    protected override void OnHoverLost(HoverLostEvent e)
    {
        hover = false;
        UpdateBackground();
        base.OnHoverLost(e);
    }
    bool _selected = false;
    public bool Selected
    {
        get => _selected; set
        {
            if (_selected == value) return;
            _selected = value;
            UpdateBackground();
        }
    }

    public void UpdateBackground()
    {
        if (_selected)
        {
            background.Colour = DrumColors.DarkActiveBackground;
        }
        else if (hover)
        {
            background.Colour = DrumColors.DarkBackground * 0.5f + DrumColors.DarkActiveBackground * 0.5f;
        }
        else
        {
            background.Colour = DrumColors.DarkBackground;
        }
    }
    protected override bool OnClick(ClickEvent e)
    {
        if (e.Button == MouseButton.Left)
        {
            var i = carousel.FilteredMaps.IndexOf(Map);
            if (i >= 0)
            {
                if (carousel.Selector.TargetMap == Map)
                    carousel.Selector.SelectMap(e.ControlPressed);
                else
                    carousel.PullTarget(i);
                return true;
            }
        }
        return base.OnClick(e);
    }

    public BeatmapSelectorMap Map { get; private set; }

    bool invalid = false;
    public void Invalidate()
    {
        invalid = true; // forces a metadata reload on next load
    }

    public void LoadMap(BeatmapSelectorMap map, bool force = false) // update/load thread only
    {
        Alpha = 1;
        if (Map == map && !force && !invalid) return;
        invalid = false;
        Map = map;
        var metadata = Util.MapStorage.GetMetadata(map);
        titleText.Text = metadata.Title;

        if (metadata.Duration == 0)
        {
            durationText.Text = "";
            durationText.Tooltip = null;
        }
        else
        {
            var duration = Util.FormatTime(metadata.Duration);
            var bpm = metadata.BPM != 0 ? $"{metadata.BPM:0.##} BPM" : null;
            durationText.Text = $"{duration} - {bpm}";
            durationText.Tooltip = metadata.BpmRange == null ? null : metadata.BpmRange + " BPM";
        }

        artistText.MaxWidth = Width - artistText.X - durationText.Width;
        artistText.Text = metadata.Artist;

        image.Target = (map.FullAssetPath(metadata.Image), metadata.ImageUrl);

        mappingText.Clear();
        var hasDiff = false;
        if (!string.IsNullOrWhiteSpace(metadata.DifficultyString))
        {
            Drawable add = new SpriteText
            {
                Text = metadata.DifficultyString,
                Font = FrameworkFont.Regular.With(size: 18, weight: "Bold"),
                Colour = DifficultyColor(metadata.Difficulty)
            };
            if (metadata.Difficulty == BeatmapDifficulty.ExpertPlus)
            {
                var sigma = 3;
                var container = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Padding = new MarginPadding(-sigma * 2)
                };
                add.X = sigma * 2;
                add.Y = sigma * 2;
                var effect = new SpriteText
                {
                    Text = metadata.DifficultyString,
                    Font = FrameworkFont.Regular.With(size: 18, weight: "Bold"),
                }.WithEffect(new BlurEffect
                {
                    // this used to use CacheDrawnEffect, not sure if needed
                    // the new way to cache is pass useCachedFrameBuffer to BufferedContainer constructor, which is a little difficult here
                    // we would need to make a custom blur effect (which would overall be pretty easy)
                    Colour = new Colour4(1.5f, 1.5f, 1.5f, 40f), // Idk how this really works
                    PadExtent = true,
                    Sigma = new Vector2(sigma),
                    DrawOriginal = false,
                    Blending = BlendingParameters.Additive
                });
                container.Add(effect);
                container.Add(add);
                add = container;
            }
            mappingText.Add(add);
            hasDiff = true;
        }
        if (!string.IsNullOrWhiteSpace(metadata.Mapper))
        {
            var mt = hasDiff ? " - " : "";
            mt += "mapped by " + metadata.Mapper;
            mappingText.Add(new SpriteText
            {
                Text = mt,
                Font = FrameworkFont.Regular.With(size: 18)
            });
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(metadata.Folder))
            {
                var mt = hasDiff ? " - " : "";
                mt += metadata.Folder;
                mappingText.Add(new SpriteText
                {
                    Text = mt,
                    Font = FrameworkFont.Regular.With(size: 18)
                });
            }
        }
        if (!metadata.RatingLoaded)
        {
            waitingForRating = metadata;
            Util.MapStorage.LoadRatings();
        }
        else ratingText.SetCurrent(metadata.Rating);
    }

    BeatmapMetadata waitingForRating;

    protected override void Update()
    {
        if (waitingForRating != null && waitingForRating.RatingLoaded)
        {
            // make sure we're still waiting for the right map
            if (waitingForRating == Util.MapStorage.GetMetadata(Map))
                ratingText.SetCurrent(waitingForRating.Rating);
            waitingForRating = null;
        }
    }
}

