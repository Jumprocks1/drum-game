// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shaders;
using osu.Framework.IO.Stores;
using osu.Framework.Layout;
using osu.Framework.Text;
using osu.Framework.Graphics.Rendering;

// main difference between SpriteText is that this only supports single characters

namespace osu.Framework.Graphics.Sprites
{
    /// <summary>
    /// A container for simple text rendering purposes. If more complex text rendering is required, use <see cref="TextFlowContainer"/> instead.
    /// </summary>
    public class NoteSprite : Drawable, ITexturedShaderDrawable
    {
        [Resolved]
        private FontStore store { get; set; }
        public IShader TextureShader { get; private set; }

        public NoteSprite()
        {
            AddLayout(parentScreenSpaceCache);
            AddLayout(localScreenSpaceCache);
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);

            store.Get(font.FontName, Character);
        }

        public char Character;

        private FontUsage font = FrameworkFont.Regular;

        /// <summary>
        /// Contains information on the font used to display the text.
        /// </summary>
        public FontUsage Font
        {
            get => font;
            set
            {
                font = value;

                parentScreenSpaceCache.Invalidate();
                localScreenSpaceCache.Invalidate();

                Invalidate(Invalidation.DrawNode);
            }
        }

        #region Characters

        NoteTextBuilderGlyph? _characterGlyph;
        private NoteTextBuilderGlyph characterGlyph => _characterGlyph ??= computeCharacter();

        /// <summary>
        /// Compute character textures and positions.
        /// </summary>
        private NoteTextBuilderGlyph computeCharacter() => NoteTextBuilderGlyph.From(store, Font, Character);

        private readonly LayoutValue parentScreenSpaceCache = new LayoutValue(Invalidation.DrawSize | Invalidation.Presence | Invalidation.DrawInfo, InvalidationSource.Parent);
        private readonly LayoutValue localScreenSpaceCache = new LayoutValue(Invalidation.MiscGeometry, InvalidationSource.Self);

        private ScreenSpaceCharacterPart screenSpaceCharacterBacking;

        /// <summary>
        /// The characters in screen space. These are ready to be drawn.
        /// </summary>
        private ScreenSpaceCharacterPart screenSpaceCharacters
        {
            get
            {
                computeScreenSpaceCharacters();
                return screenSpaceCharacterBacking;
            }
        }

        private void computeScreenSpaceCharacters()
        {
            if (!parentScreenSpaceCache.IsValid)
            {
                localScreenSpaceCache.Invalidate();
                parentScreenSpaceCache.Validate();
            }

            if (localScreenSpaceCache.IsValid)
                return;

            Vector2 inflationAmount = DrawInfo.MatrixInverse.ExtractScale().Xy;

            var glyph = characterGlyph;
            screenSpaceCharacterBacking = new ScreenSpaceCharacterPart
            {
                DrawQuad = ToScreenSpace(glyph.DrawRectangle.Inflate(inflationAmount)),
                InflationPercentage = Vector2.Divide(inflationAmount, glyph.DrawRectangle.Size),
                Texture = glyph.Texture
            };

            localScreenSpaceCache.Validate();
        }

        #endregion
        protected override DrawNode CreateDrawNode() => new NoteSpriteDrawNode(this);

        class NoteSpriteDrawNode : TexturedShaderDrawNode // based on SpriteTextDrawNode
        {
            protected new NoteSprite Source => (NoteSprite)base.Source;

            private ScreenSpaceCharacterPart? part;

            public NoteSpriteDrawNode(NoteSprite source) : base(source) { }

            public override void ApplyState()
            {
                base.ApplyState();

                part = Source.screenSpaceCharacters;
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                BindTextureShader(renderer);

                if (part is ScreenSpaceCharacterPart p)
                    renderer.DrawQuad(p.Texture, p.DrawQuad, DrawColourInfo.Colour, inflationPercentage: p.InflationPercentage);

                UnbindTextureShader(renderer);
            }
        }
    }

    /// <summary>
    /// A character of a <see cref="NoteSprite"/> provided with screen space draw coordinates.
    /// </summary>
    internal struct ScreenSpaceCharacterPart
    {
        /// <summary>
        /// The screen-space quad for the character to be drawn in.
        /// </summary>
        public Quad DrawQuad;

        /// <summary>
        /// Extra padding for the character's texture.
        /// </summary>
        public Vector2 InflationPercentage;

        /// <summary>
        /// The texture to draw the character with.
        /// </summary>
        public Texture Texture;
    }

    /// <summary>
    /// A <see cref="ITexturedCharacterGlyph"/> provided as final output from a <see cref="NoteTextBuilder"/>.
    /// </summary>
    public struct NoteTextBuilderGlyph
    {
        public readonly Texture Texture => Glyph.Texture;
        public readonly float XOffset => Glyph.XOffset * textSize;
        public readonly float YOffset => Glyph.YOffset * textSize;
        public readonly float Width => Glyph.Width * textSize;
        public readonly float Height => Glyph.Height * textSize;

        public readonly ITexturedCharacterGlyph Glyph;

        /// <summary>
        /// The rectangle for the character to be drawn in.
        /// </summary>
        public RectangleF DrawRectangle { get; internal set; }

        private readonly float textSize;

        public static NoteTextBuilderGlyph From(ITexturedGlyphLookupStore store, FontUsage font, char character)
        {
            var fontStoreGlyph = store.Get(font.FontName, character);

            var glyph = new NoteTextBuilderGlyph(fontStoreGlyph, font.Size);

            glyph.DrawRectangle = new RectangleF(new Vector2(glyph.XOffset, glyph.YOffset), new Vector2(glyph.Width, glyph.Height));

            return glyph;

        }

        internal NoteTextBuilderGlyph(ITexturedCharacterGlyph glyph, float textSize)
        {
            this = default;
            this.textSize = textSize;

            Glyph = glyph;
        }
    }
}
