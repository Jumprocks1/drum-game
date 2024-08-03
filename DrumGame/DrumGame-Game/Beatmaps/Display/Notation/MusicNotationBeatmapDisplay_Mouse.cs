using System;
using DrumGame.Game.Components;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics;
using osuTK;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Commands;

namespace DrumGame.Game.Beatmaps.Display;

public partial class MusicNotationBeatmapDisplay : IHasMarkupTooltip
{
    const float NoteHoverRadius = 0.75f;
    Circle _hoverIndicator;
    Circle HoverIndicator
    {
        get
        {
            if (_hoverIndicator == null)
            {
                NoteContainer.Add(_hoverIndicator = new Circle // very similar to DisplayScoreEvent
                {
                    Width = NoteHoverRadius * 2,
                    Height = NoteHoverRadius * 2,
                    Origin = Anchor.Centre,
                    Colour = DrumColors.ObjectSelection.MultiplyAlpha(0.6f),
                    Depth = -2 // make sure we're on top of notes
                });
            }
            return _hoverIndicator;
        }
    }
    public void UpdateHover()
    {
        var tooltip = GetTooltip();

        if (tooltip == null && _hoverIndicator != null)
            _hoverIndicator.Alpha = 0;

        MarkupTooltip = tooltip;
    }
    public string MarkupTooltip { get; private set; }


    string GetTooltip()
    {
        if (Player is not BeatmapEditor || Util.InputManager.MouseForceHidden) return null;
        // TODO will also need a config for this to invert/disable this check
        if (!Util.CommandController.IsHeld(Command.EditorMouse)) return null;

        // this accounts for the size of a notehead and is centered in the middle of a note
        // this is quite a bit different from how the mouse conversions work for things like selection,
        //    since those are aligned to the front of the notehead
        var noteContainerMouse = NoteContainer.ToLocalSpace(InputManager.CurrentState.Mouse.Position);
        var mouseBeat = (noteContainerMouse.X - MainNoteheadWidth / 2) / Font.Spacing;

        // note hitbox will be 1.5f diameter centered on the default notehead, same as a score event

        var tickRate = Beatmap.TickRate;
        var minTick = Beatmap.TickFromBeatSlow(mouseBeat - NoteHoverRadius / Font.Spacing);
        var maxTick = Beatmap.TickFromBeatSlow(mouseBeat + NoteHoverRadius / Font.Spacing);

        var squaredDistance = float.MaxValue;
        HitObject hitObject = null;
        var skin = Util.Skin.Notation;

        foreach (var i in Beatmap.GetHitObjectsInTicks(minTick, maxTick))
        {
            var ho = Beatmap.HitObjects[i];
            var horizontalDist = ((float)ho.Time / tickRate - mouseBeat) * Font.Spacing;
            var verticalDist = (float)skin.Channels[ho.Channel].Position / 2 - noteContainerMouse.Y;
            var dist = horizontalDist * horizontalDist + verticalDist * verticalDist;
            if (dist < squaredDistance)
            {
                squaredDistance = dist;
                hitObject = ho;
            }
        }
        if (hitObject == null || squaredDistance > NoteHoverRadius * NoteHoverRadius)
            return null;

        var sticking = "";
        if (hitObject.Modifiers.HasFlag(NoteModifiers.Left))
            sticking = ":" + MarkupText.Color("L", skin.LeftNoteColor);
        else if (hitObject.Modifiers.HasFlag(NoteModifiers.Right))
            sticking = ":" + MarkupText.Color("R", skin.RightNoteColor);

        var velocityModifier = "";
        if (hitObject.Modifiers.HasFlag(NoteModifiers.Ghost))
            velocityModifier = "<faded>(Ghost)</>";
        else if (hitObject.Modifiers.HasFlag(NoteModifiers.Accented))
            velocityModifier = "(Accented)";

        HoverIndicator.Alpha = 0.7f; // gets 1f alpha when mouse is down
        HoverIndicator.X = (float)hitObject.Time / tickRate * Font.Spacing + MainNoteheadWidth / 2;
        HoverIndicator.Y = (float)skin.Channels[hitObject.Channel].Position / 2;

        var preset = "";
        if (hitObject.Preset != null)
        {
            preset = $"\n<brightGreen>Preset:</c> <brightCyan>{MarkupText.Escape(hitObject.Preset.Key)}</c>";
            if (hitObject.Preset.Name != null)
            {
                preset += $" ({MarkupText.Escape(hitObject.Preset)})";
            }
        }

        return $"<midi>{hitObject.Channel}</c>{sticking}{velocityModifier}{preset}";
    }
}