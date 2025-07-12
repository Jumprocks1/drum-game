using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using DrumGame.Game.Skinning;
using DrumGame.Game.Modals;

namespace DrumGame.Game.Notation;

public class DrumLegend : CompositeDrawable
{
    DrumChannel[][] LegendGroups => new[] {
        new DrumChannel[] {
            DrumChannel.Snare,
            DrumChannel.SmallTom,
            DrumChannel.MediumTom,
            DrumChannel.LargeTom,
            DrumChannel.SideStick,
            DrumChannel.Rim,
            DrumChannel.BassDrum,
            DrumChannel.HiHatPedal,
        },
        new DrumChannel[] {
            DrumChannel.ClosedHiHat,
            DrumChannel.OpenHiHat,
            DrumChannel.Ride,
            DrumChannel.RideBell,
            DrumChannel.RideCrash,
            DrumChannel.Crash,
            DrumChannel.China,
            DrumChannel.Splash,
        }
    };
    public DrumLegend()
    {
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;

        var font = Util.DrumGame.Dependencies.Get<Lazy<MusicFont>>().Value;
        var oldSpacing = font.Spacing; // wish I didn't have to do this
        font.Spacing = 5;
        var groups = LegendGroups;

        var noteheadWidth = font.GetNoteheadAnchor(MusicGlyph.noteheadBlack, false).x;
        var beats = groups.Max(e => e.Length) + 1; // add one for left side buffer
        var scale = 20f;
        var height = 12f;

        var headerHeight = 0;

        Height = scale * height * LegendGroups.Length + headerHeight;
        Width = scale * font.Spacing * beats;

        AddInternal(new Box { Colour = Util.Skin.Notation.PlayfieldBackground, RelativeSizeAxes = Axes.Both });

        var y = 0f;
        AddInternal(new SpriteText
        {
            Text = "Current Drum Legend",
            Y = 5,
            Colour = Util.Skin.Notation.NotationColor,
            Font = FrameworkFont.Regular.With(size: 25),
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre
        });

        y += headerHeight;

        for (var j = 0; j < LegendGroups.Length; j++)
        {
            var container = new NoteContainer(font, beats)
            {
                Scale = new osuTK.Vector2(scale),
                Y = y + (height - 4) * scale / 2
            };

            var channels = LegendGroups[j];
            for (var i = 0; i < channels.Length; i++)
            {
                var channel = channels[i];
                var ho = new HitObject(i + 1, channel);
                var group = NoteGroup.Create(1, new List<HitObject> { ho }, ho.IsFoot, i + 2);
                font.RenderGroup(container, group, 0, null);
                var label = channel.ToString().FromPascalCase();
                var offset = group.Down ? -1 : 1;
                container.Add(new SpriteText
                {
                    Colour = Util.Skin.Notation.NotationColor,
                    Scale = new osuTK.Vector2(1 / scale),
                    Text = label,
                    X = font.Spacing * (i + 1) + noteheadWidth / 2,
                    Y = (float)Math.Ceiling(group.Flags[0].BottomNote.Note.Position / 2f - 0.5f) + 0.5f + offset,
                    Origin = Anchor.Centre
                });
            }
            AddInternal(container);
            y += height * scale;
        }
        font.Spacing = oldSpacing;
        SkinManager.SkinChanged += SkinChanged;
    }
    static void SkinChanged()
    {
        Util.Palette.Close<OverlayModal<DrumLegend>>();
        Util.Palette.ViewDrumLegend();
    }
    protected override void Dispose(bool isDisposing)
    {
        SkinManager.SkinChanged -= SkinChanged;
        base.Dispose(isDisposing);
    }
}