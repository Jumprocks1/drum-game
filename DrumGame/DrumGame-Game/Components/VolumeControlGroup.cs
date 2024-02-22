using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Commands;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Components;

public class VolumeControlGroup : AdjustableSkinElement
{
    public override ref AdjustableSkinData SkinPath => ref Util.Skin.Notation.VolumeControlGroup;
    public override AdjustableSkinData DefaultData() => new()
    {
        Anchor = Anchor.BottomRight,
        Y = -BeatmapTimeline.Height - 20
    };
    public BindableNumber<double> RelativeSongVolume;
    public VolumeControlGroup(BeatmapEditor editor = null)
    {
        AutoSizeAxes = Axes.Both;
        if (editor != null)
        {
            // Idk why we use an unbound copy
            RelativeSongVolume = editor.Track.Track.Volume.GetUnboundCopy();
            RelativeSongVolume.BindValueChanged(e =>
            {
                if (editor.Beatmap.RelativeVolume == e.NewValue) return;
                editor.Beatmap.RelativeVolume = e.NewValue;
                editor.Track.Track.Volume.Value = editor.Beatmap.CurrentRelativeVolume;
                // would be nice to handle this with a IHistoryChange instead
                // to do that, I think we need to adjust how we handle this bindable
                // the main problem is we don't want to fire an event when we use undo
                editor.ForceDirty();
            });
            editor.Beatmap.RelativeVolume = RelativeSongVolume.Value;
            editor.Track.Track.Volume.Value = editor.Beatmap.CurrentRelativeVolume;
        }
    }
    [BackgroundDependencyLoader]
    private void load(VolumeController controller)
    {
        AddInternal(new VolumeControl(controller.MasterVolume, "Master", FontAwesome.Solid.VolumeDown,
            new VolumeButton { Command = Command.ToggleMute }));
        AddInternal(new VolumeControl(controller.TrackVolume, "Music", FontAwesome.Solid.Music)
        {
            X = VolumeControl.Thickness * 1
        });
        AddInternal(new VolumeControl(controller.HitVolume, "Hit", FontAwesome.Solid.Drum,
            helperText: $"Controls volume of hit samples played from the game.\nTypically used with {IHasCommand.GetMarkupTooltip(Command.ToggleAutoPlayHitSounds)}.")
        {
            X = VolumeControl.Thickness * 2
        });
        AddInternal(new VolumeControl(controller.MetronomeVolume, "Metronome", FontAwesome.Solid.Clock, new VolumeButton { Command = Command.ToggleMetronome })
        {
            X = VolumeControl.Thickness * 3
        });
        if (RelativeSongVolume != null)
        {
            AddInternal(new VolumeControl(new LevelVolumeBinding(RelativeSongVolume), "Map Relative Volume", FontAwesome.Solid.Music,
            new VolumeButton { Command = Command.SetNormalizedRelativeVolume, Icon = FontAwesome.Solid.WaveSquare },
                helperText: "This is used to adjust the volume for this map only.\nIt gets saved to the map file.")
            {
                X = VolumeControl.Thickness * 4
            });
        }
    }
}

