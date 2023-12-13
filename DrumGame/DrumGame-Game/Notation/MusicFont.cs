using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.Skins;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Stores;

namespace DrumGame.Game.Notation;

// we should probably add another abstract here that allows us to have multiple MusicFont instances sharing the same JSON source
// I think we can mostly keep this MusicFont class, we should just move the configurable things into the Skin class
// Right now, we could move RollColour, StemHeight, and Spacing into the Skin class
// all of the other values are tied pretty closely to the Bravura SMuFL definition
public partial class MusicFont
{
    public Skin Skin => Util.Skin;
    // couldn't find this specified anywhere
    // this is the distance between the right side of the note and the left side of the dot
    public const float AugmentationDotGap = 0.1f;
    // distance above the top of the stem for accent placement
    public const float AccentGap = 0.1f;
    public Colour4 Colour => Skin.Notation.NotationColor;
    public static readonly Colour4 RollColour = DrumColors.DarkBlue.MultiplyAlpha(0.75f);
    public static readonly float RollHeight = 0.5f;
    public readonly string Name;
    public readonly JObject Anchors;
    public readonly EngravingDefaults EngravingDefaults;
    // This height starts from halfway up the notehead, meaning 0.5 would go to the exact height of the head, 1.5 would go one space above the head
    // This is also only the minimum suggested height. It will be overridden as necessary
    public float StemHeight = 2.5f;
    // technically this will prevent us from displaying 2 maps at once (with different spacings), but I don't really care
    public const float DefaultSpacing = 5f;
    public float Spacing = DefaultSpacing;
    public readonly FontUsage FontUsage;
    public Drawable Notehead(SkinNote note, float x) => new NoteSprite
    {
        Character = note.Notehead.Codepoint(),
        Y = -8 + note.Position / 2f,
        X = x,
        Colour = note.Color,
        Font = FontUsage,
        Depth = -note.Position - 5
    };
    // this is just for rendering the fancy flags
    public Drawable RenderFlag(Flag flag, float targetHeight, bool down)
    {
        MusicGlyph? glyph = null;
        if (flag.EffectiveDuration <= 0.375)
        {
            glyph = down ? MusicGlyph.flag16thDown : MusicGlyph.flag16thUp;
        }
        else if (flag.EffectiveDuration <= 0.75)
        {
            glyph = down ? MusicGlyph.flag8thDown : MusicGlyph.flag8thUp;
        }
        if (glyph.HasValue)
        {
            // this extension should technically be applied to targetHeight prior to this
            var extension = down ? 0.5f : -0.5f;
            var y = -8 + targetHeight + extension;
            var anchor = GetAnchorValue(glyph.Value, down ? "stemDownSW" : "stemUpNW");
            return new NoteSprite
            {
                Character = glyph.Value.Codepoint(),
                X = flag.FlagLeft - anchor[0],
                Y = y + anchor[1],
                Colour = Colour,
                Font = FontUsage
            };
        }
        else
        {
            return null;
        }
    }
    public Drawable Beam(float start, float end, float y) => new Box
    {
        Width = end - start,
        X = start,
        Y = y,
        Colour = Colour,
        Height = EngravingDefaults.beamThickness,
    };
    public MusicFont(IResourceStore<byte[]> resources, string name, string metadata)
    {
        Name = name;
        FontUsage = new FontUsage(Name, 16);
        using var stream = resources.GetStream(metadata);
        var serializer = new JsonSerializer();

        using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
        using (var sr = new StreamReader(gzip))
        using (var jsonTextReader = new JsonTextReader(sr))
        {
            var obj = serializer.Deserialize<JObject>(jsonTextReader);
            Anchors = obj.GetValue("glyphsWithAnchors").ToObject<JObject>();
            EngravingDefaults = obj.GetValue("engravingDefaults").ToObject<EngravingDefaults>();
        }
    }

    Dictionary<(MusicGlyph, bool), (float x, float y)> AnchorCache;
    public (float x, float y) GetNoteheadAnchor(MusicGlyph notehead, bool down)
    {
        if (AnchorCache == null) AnchorCache = new();
        if (AnchorCache.TryGetValue((notehead, down), out var found))
        {
            return found;
        }
        else
        {
            var res = GetAnchorValue(notehead, down ? "stemDownNW" : "stemUpSE");
            var tup = (res[0], res[1]);
            AnchorCache[(notehead, down)] = tup;
            return tup;
        }
    }

    public float[] GetAnchorValue(MusicGlyph notehead, string value) =>
        (Anchors.GetValue(notehead.ToString()) as JObject)
            .GetValue(value).ToObject<float[]>();
}

