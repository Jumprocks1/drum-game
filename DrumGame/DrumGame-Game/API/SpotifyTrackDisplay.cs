using System;
using System.Collections.Generic;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Browsers;
using DrumGame.Game.Browsers.BeatmapSelection;
using DrumGame.Game.Components;
using DrumGame.Game.Containers;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;

namespace DrumGame.Game.API;

public class SpotifyTrackDisplay : RequestModal
{
    List<Spotify.TrackResponse> Tracks;
    public readonly BeatmapSelector Selector;
    public readonly BeatmapSelectorMap Map;
    public SpotifyTrackDisplay(List<Spotify.TrackResponse> tracks, BeatmapSelectorMap map, BeatmapSelector selector) : base(new RequestConfig
    {
        Title = $"Spotify Search Results For {map.LoadedMetadata.Title} by {map.LoadedMetadata.Artist}",
        Width = 0.8f
    })
    {
        Selector = selector;
        Map = map;
        Tracks = tracks;
    }

    const int rowSize = 20;
    [BackgroundDependencyLoader]
    private void load()
    {
        var y = 0;
        foreach (var e in Tracks)
        {
            Add(new Row(e) { Y = y });
            y += rowSize;
        }
    }


    class Row : DrumButton, IHasContextMenu
    {
        Spotify.TrackResponse Track;
        Sprite Image;
        public Row(Spotify.TrackResponse track)
        {
            Track = track;
            Height = rowSize;
            RelativeSizeAxes = Axes.X;

            Action = LinkTrack;

            Image = new OnlineSprite(Track.Album.SmallImage.Url);
            Image.X = rowSize;
            Image.Origin = Anchor.CentreRight;
            Image.Y = rowSize / 2;
            Image.Size = new osuTK.Vector2(rowSize);
            Image.Depth = -1;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var fillFlow = new FillFlowContainer<SpriteText>
            {
                X = 30,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Direction = FillDirection.Horizontal,
                Height = rowSize
            };
            fillFlow.Add(new SpriteText
            {
                Font = FrameworkFont.Regular.With(size: 20),
                Text = Track.Name,
                Colour = DrumColors.BrightGreen
            });
            fillFlow.Add(new SpriteText
            {
                Font = FrameworkFont.Regular.With(size: 20),
                Text = " by "
            });
            fillFlow.Add(new SpriteText
            {
                Font = FrameworkFont.Regular.With(size: 20),
                Text = string.Join(", ", Track.Artists),
                Colour = DrumColors.BrightYellow
            });
            fillFlow.Add(new SpriteText
            {
                Font = FrameworkFont.Regular.With(size: 20),
                Text = " on "
            });
            fillFlow.Add(new SpriteText
            {
                Font = FrameworkFont.Regular.With(size: 20),
                Text = $"{Track.Album.Name} ({Track.Album.album_type})",
                Colour = DrumColors.BrightYellow
            });
            Add(fillFlow);
            LoadComponentAsync(Image, AddInternal);
        }

        void LinkTrack() => MutateAndClose(b =>
        {
            b.Spotify = Track.Id;
            b.ImageUrl = Track.Album.GoodImage.Url;
            b.HashImageUrl();
        });
        void UseImage() => MutateAndClose(b =>
        {
            b.ImageUrl = Track.Album.GoodImage.Url;
            b.HashImageUrl();
        });
        void UseTitle() => MutateAndClose(b =>
        {
            b.Title = Track.Name;
        });

        void MutateAndClose(Action<Beatmap> mutate)
        {
            var parent = this.FindClosestParent<SpotifyTrackDisplay>();
            parent.Selector.MutateBeatmap(parent.Map, mutate);
            parent.Close();
        }

        protected override bool OnHover(HoverEvent e)
        {
            Image.Size = new osuTK.Vector2(rowSize) * 6;
            return base.OnHover(e);
        }
        protected override void OnHoverLost(HoverLostEvent e)
        {
            Image.Size = new osuTK.Vector2(rowSize);
            base.OnHoverLost(e);
        }

        public MenuItem[] ContextMenuItems => ContextMenuBuilder.New(Track)
            .Add("Link Track to Beatmap", t => LinkTrack())
            .Add("Use Image", t => UseImage())
            .Add("Use Title", t => UseTitle())
            .Add("View on Spotify", t => Util.Host.OpenUrlExternally(t.WebUrl))
            .Build();

        protected override SpriteText CreateText() => null;
    }
}