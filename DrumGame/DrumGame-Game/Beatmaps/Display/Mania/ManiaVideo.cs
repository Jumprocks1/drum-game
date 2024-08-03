using System;
using System.IO;
using System.Linq.Expressions;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Skinning;
using DrumGame.Game.Stores;
using DrumGame.Game.Timing;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;

namespace DrumGame.Game.Beatmaps.Display.Mania;

public class ManiaVideo : AdjustableSkinElement
{
    public override Expression<Func<Skin, AdjustableSkinData>> SkinPathExpression => e => e.Mania.Video;

    readonly BeatmapPlayer Player;
    BeatClock Track => Player.Track;
    Beatmap Beatmap => Player.Beatmap;
    public ManiaVideo(BeatmapPlayer player)
    {
        SkinManager.RegisterTarget(SkinAnchorTarget.Video, this);
        Player = player;
        Util.CommandController.RegisterHandlers(this);
    }
    [BackgroundDependencyLoader]
    private void load()
    {
        if (Util.ConfigManager.Get<bool>(DrumGameSetting.AutoLoadVideo))
        {
            var videoPath = Beatmap.FullAssetPath(Beatmap.Video);
            if (videoPath != null && File.Exists(videoPath))
                LoadVideo();
        }
    }
    public override AdjustableSkinData DefaultData() => new()
    {
        Anchor = Anchor.TopLeft,
        Origin = Anchor.BottomLeft,
        AnchorTarget = SkinAnchorTarget.SongInfoPanel,
        Width = 288, // 16:9
        Height = 162
    };

    public SyncedVideo Video { get; private set; }
    // mostly copied from BeatmapAuxDisplay
    public void LoadVideo() => LoadVideo(Beatmap.FullAssetPath(Beatmap.Video));
    public void LoadVideo(string videoPath)
    {
        if (Video != null) return;
        if (videoPath == null) return;
        if (!File.Exists(videoPath))
        {
            Util.Palette.UserError($"{videoPath} not found");
            return;
        }
        AddInternal(Video = new SyncedVideo(Track, videoPath)
        {
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            FillMode = FillMode.Fit
        });
        Video.Offset.Value = Beatmap.VideoOffset;
        Video.Offset.BindValueChanged(e => Beatmap.VideoOffset = e.NewValue);
    }
    [CommandHandler]
    public void ToggleVideo()
    {
        if (Video == null) LoadVideo();
        else
        {
            RemoveInternal(Video, true);
            Video = null;
        }
    }
    protected override void Dispose(bool isDisposing)
    {
        SkinManager.UnregisterTarget(SkinAnchorTarget.Video);
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }
}