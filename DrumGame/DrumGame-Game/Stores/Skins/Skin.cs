using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using DrumGame.Game.Channels;
using DrumGame.Game.Components;
using DrumGame.Game.Notation;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;

namespace DrumGame.Game.Stores.Skins;

public class Skin
{
    [Description("Can be a semver range for more warnings/compatibility hints")]
    public string GameVersion;
    public string SkinVersion;
    public string Name;
    public string Description;
    public string Comments;
    public class Skin_HitColors
    {
        public Colour4 EarlyMiss;
        public Colour4 LateMiss;
        public Colour4 Miss = new(178, 12, 12, 255);

        public Colour4 EarlyBad;
        public Colour4 LateBad;
        public Colour4 Bad = Colour4.DarkOrange;

        public Colour4 EarlyGood;
        public Colour4 LateGood;
        public Colour4 Good = Colour4.LawnGreen;

        public Colour4 EarlyPerfect;
        public Colour4 LatePerfect;
        public Colour4 Perfect = Colour4.DeepSkyBlue;

        public const float DefaultShadeAmount = 0.4f;

        public void LoadDefaults()
        {
            if (EarlyMiss == default) EarlyMiss = Miss.Darken(DefaultShadeAmount);
            if (LateMiss == default) LateMiss = Miss.Lighten(DefaultShadeAmount);

            if (EarlyBad == default) EarlyBad = Bad.Darken(DefaultShadeAmount);
            if (LateBad == default) LateBad = Bad.Lighten(DefaultShadeAmount);

            if (EarlyGood == default) EarlyGood = Good.Darken(DefaultShadeAmount);
            if (LateGood == default) LateGood = Good.Lighten(DefaultShadeAmount);

            if (EarlyPerfect == default) EarlyPerfect = Perfect;
            if (LatePerfect == default) LatePerfect = Perfect;
        }
        public Skin_HitColors Clone() => (Skin_HitColors)MemberwiseClone();
    }
    public Skin_HitColors HitColors = new();
    public AdjustableSkinData KeyPressOverlay;

    public class Skin_NotationInfo
    {
        [Description("Color for stems, beams, accents, and ghost notes.")]
        public Colour4 NotationColor = Colour4.Black;
        [Description("Default color for noteheads")]
        public Colour4 NoteColor;
        [Description("Shows measure lines while playing")]
        public bool MeasureLines;
        public Colour4 MeasureLineColor;
        public Colour4 StaffLineColor;
        public Colour4 PlayfieldBackground = Colour4.White;
        public Colour4 InputDisplayBackground = Colour4.Gainsboro;
        public AdjustableSkinData SongInfoPanel;
        public AdjustableSkinData EventContainer;
        public AdjustableSkinData VolumeControlGroup;
        public AdjustableSkinData PracticeInfoPanel;
        public void LoadDefaults()
        {
            if (NoteColor == default) NoteColor = NotationColor;
            if (StaffLineColor == default) StaffLineColor = NotationColor;
            if (MeasureLineColor == default) MeasureLineColor = DrumColors.Blue.MultiplyAlpha(0.4f);
            foreach (var channel in Channels.Values)
            {
                if (channel.Color == default)
                    channel.Color = NoteColor;
            }
        }
        // we could add an array version of this since dictionary is much slower
        [JsonConverter(typeof(SkinManager.SkinChannelConverter))]
        public Dictionary<DrumChannel, SkinNote> Channels = new()
        {
            {DrumChannel.HiHatPedal, new SkinNote(9, MusicGlyph.noteheadXBlack)},
            {DrumChannel.Crash, new SkinNote(-2, MusicGlyph.noteheadXOrnate)},
            {DrumChannel.Splash, new SkinNote(-2, MusicGlyph.noteheadDiamondWhite)},
            {DrumChannel.China, new SkinNote(-2, MusicGlyph.noteheadXOrnateEllipse)},
            {DrumChannel.OpenHiHat, new SkinNote(-1, MusicGlyph.noteheadCircleX)},
            {DrumChannel.HalfOpenHiHat, new SkinNote(-1, MusicGlyph.noteheadCircleX)},
            {DrumChannel.ClosedHiHat, new SkinNote(-1, MusicGlyph.noteheadXBlack)},
            {DrumChannel.Ride, new SkinNote(0, MusicGlyph.noteheadXBlack)},
            {DrumChannel.RideBell, new SkinNote(0, MusicGlyph.noteheadDiamondBlack)},
            {DrumChannel.Snare, new SkinNote(3)},
            {DrumChannel.Rim, new SkinNote(1, MusicGlyph.noteheadXBlack)},
            {DrumChannel.SideStick, new SkinNote(3, MusicGlyph.noteheadCircledBlackLarge)},
            {DrumChannel.SmallTom, new SkinNote(1, color: new(0f, 0.5f, 0.58f, 1f))},
            {DrumChannel.MediumTom, new SkinNote(2, color: new(0.33f, 0f, 0.58f, 1f))},
            {DrumChannel.LargeTom, new SkinNote(5, color: new(0.58f, 0.17f, 0f, 1f))},
            {DrumChannel.BassDrum, new SkinNote(7)},
        };
    }
    public Skin_NotationInfo Notation = new();
    public ManiaSkinInfo Mania = new();

    public void LoadDefaults()
    {
        HitColors.LoadDefaults();
        Notation.LoadDefaults();
        Mania.LoadDefaults();
    }

    [JsonIgnore] public string Source;
    [JsonIgnore] public string SourceFolder;
    [JsonIgnore] public IResourceStore<TextureUpload> LoaderStore;
    public void UnloadTextureStore()
    {
        if (LoaderStore != null)
            Util.Resources.AssetTextureStore.RemoveTextureStore(LoaderStore);
    }
    public void LoadTextureStore()
    {
        if (SourceFolder != null)
        {
            var assetStore = new StorageBackedResourceStore(Util.Resources.Storage.GetStorageForDirectory(SourceFolder));
            LoaderStore = Util.Host.CreateTextureLoaderStore(assetStore);
            Util.Resources.AssetTextureStore.AddTextureSource(LoaderStore);
        }
    }
}