using System;
using System.Linq.Expressions;
using DrumGame.Game.Beatmaps.Display;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Commands;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;

namespace DrumGame.Game.Components;

public class VolumeControlGroup : AdjustableSkinElement
{
    public class CustomSkinData : AdjustableSkinData
    {
        public bool HideWhenNotHovered;
    }
    public override Expression<Func<Skin, AdjustableSkinData>> SkinPathExpression => e => e.Notation.VolumeControlGroup;
    public new CustomSkinData SkinData => (CustomSkinData)base.SkinData;
    public override CustomSkinData DefaultData() => new()
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


    public override void ModifyOverlayContextMenu(ContextMenuBuilder<AdjustableSkinElement> menu)
    {
        if (HideWhenNotHovered)
            menu.Add("Always show", _ =>
            {
                SkinData.HideWhenNotHovered = false;
                SkinPathExpression.Set(SkinData);
            });
        else
            menu.Add("Hide when not hovered", _ =>
            {
                SkinData.HideWhenNotHovered = true;
                SkinPathExpression.Set(SkinData);
            })
                .Tooltip("Recommended if this control gets in the way.\nClick <brightGreen>Save changes to skin</> afterwards to save.");
    }
    bool HideWhenNotHovered => SkinData.HideWhenNotHovered;
    void UpdateColor()
    {
        if (!HideWhenNotHovered) return;
        Colour = (IsHovered || Overlay != null) ? Colour4.White : Colour4.Transparent;
    }
    public override void HideOverlay()
    {
        base.HideOverlay();
        UpdateColor();
    }
    public override void ShowOverlay()
    {
        base.ShowOverlay();
        UpdateColor();
    }
    protected override bool OnHover(HoverEvent e)
    {
        UpdateColor();
        return base.OnHover(e);
    }
    protected override void OnHoverLost(HoverLostEvent e)
    {
        UpdateColor();
        base.OnHoverLost(e);
    }
    [BackgroundDependencyLoader]
    private void load(VolumeController controller)
    {
        UpdateColor();
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

