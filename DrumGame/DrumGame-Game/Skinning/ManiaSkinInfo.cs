using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Channels;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Graphics;

namespace DrumGame.Game.Skinning;

public interface IChipInfo
{
    public SkinTexture Adornment { get; set; }
    public DrumChannel Channel { get; set; }
    public Colour4 Color { get; set; }
    public SkinTexture Chip { get; set; }
}
public class ManiaSkinInfo
{
    public static double ScrollRate => Util.Skin.Mania.ScrollMultiplier * 0.25d / 1000; // measured in vertical screens per millisecond

    public float GhostNoteWidth = 0.7f;
    public float JudgementLinePosition = 0.15f;
    public Colour4 JudgementLineColor = DrumColors.Yellow;
    public double JudgementLineOffset = 0; // ms
    public float JudgementLineThickness = 0.008f;
    public ManiaJudgementInfo Judgements = new();
    public float ChipThickness;
    public Colour4 BeatLineColor = new(200, 200, 200, 255);
    public Colour4 MeasureLineColor;
    public Colour4 BackgroundColor = new(16, 16, 16, 255);
    public Colour4 BackgroundFontColor;
    public float BeatLineThickness = 0.002f;
    public float MeasureLineThickness;
    public Colour4 BorderColor = new(100, 100, 100, 255);
    public AdjustableSkinData SongInfoPanel;
    public AdjustableSkinData HitErrorDisplay;
    public Beatmaps.Display.Mania.ManiaTimeline.PositionData PositionIndicator;
    public AdjustableSkinData PracticeInfoPanel;
    public AdjustableSkinData LaneContainer;
    public AdjustableSkinData Video;
    [Description("Increases or decreases spacing between mania chips.\nHigher values will make faster maps easier to read.\nThe numbers here should match scroll rates in DTXmania.")]
    [DefaultValue(2d)]
    public double ScrollMultiplier = 2;
    public void LoadDefaults()
    {
        for (var i = 0; i < Lanes.LaneList.Length; i++)
        {
            Lanes.LaneList[i].LoadDefaults(this);
            Lanes.LaneList[i].LoadedIndex = i;
        }
        if (ChipThickness == default)
            ChipThickness = JudgementLineThickness * 1.5f;
        if (MeasureLineThickness == default)
            MeasureLineThickness = BeatLineThickness * 3;
        if (MeasureLineColor == default)
            MeasureLineColor = BeatLineColor;
        if (BackgroundFontColor == default)
            BackgroundFontColor = DrumColors.ContrastText(BackgroundColor);
        SongInfoPanel?.LoadDefaults();
        HitErrorDisplay?.LoadDefaults();
        PositionIndicator?.LoadDefaults();
        PracticeInfoPanel?.LoadDefaults();
        LaneContainer?.LoadDefaults();
        Judgements?.LoadDefaults();
    }

    public class ManiaSkinInfo_Lane : IChipInfo
    {
        public ManiaSkinInfo_Lane(float width, float leftBorder, Colour4 accentColor, DrumChannel channel, Colour4 borderColor = default)
        {
            Width = width;
            LeftBorder = leftBorder;
            Color = accentColor;
            BorderColor = borderColor;
            Channel = channel;
        }
        public float LeftBorder; // left border thickness weight. The first border detail is also used for the final right border
        public Colour4 BorderColor;
        public float Width; // don't use this when rendering since it's actually a weighted width
        public float Index;
        public SkinTexture Icon;
        public SkinTexture Background;

        public const float DefaultJudgementTextPosition = 0.35f;
        [DefaultValue(DefaultJudgementTextPosition)]
        public float JudgementTextPosition = DefaultJudgementTextPosition; // 0-1, text is center on this point. 0 is bottom of screen, 1 is top

        [JsonIgnore] public DrumChannel Channel { get; set; }
        [JsonIgnore] public int LoadedIndex { get; set; }
        public Colour4 Color { get; set; }
        public SkinTexture Chip { get; set; }
        public SkinTexture Adornment { get; set; }

        public class SecondaryInfo : IChipInfo
        {
            public SecondaryInfo() { }
            public SecondaryInfo(DrumChannel channel, Colour4 color = default)
            {
                Channel = channel; Color = color;
            }
            public DrumChannel Channel { get; set; }
            public Colour4 Color { get; set; }
            public SkinTexture Chip { get; set; }
            public SkinTexture Adornment { get; set; }
        }
        [JsonConverter(typeof(SingleOrArrayConverter<SecondaryInfo>))]
        public SecondaryInfo[] Secondary;
        public void LoadDefaults(ManiaSkinInfo parent)
        {
            if (Secondary != null)
            {
                foreach (var s in Secondary)
                {
                    if (s.Color == default)
                        s.Color = Color;
                    s.Adornment ??= Adornment;
                    s.Chip ??= Chip;
                }
            }
            if (BorderColor == default)
                BorderColor = parent.BorderColor;
        }
    }
    // never instantiated, only used as a handler for the LaneConverter
    [JsonConverter(typeof(LaneConverter))]
    public ManiaSkinInfo_LaneList Lanes = new();

    public class ManiaSkinInfo_LaneList
    {
        static float DefaultJudgementTextPosition => ManiaSkinInfo_Lane.DefaultJudgementTextPosition;
        const float TextOffsetIncrement = 32f / 720;
        // Text offsets:
        // https://github.com/limyz/DTXmaniaNX/blob/169223512d5c98a4efb0b97bd68b4ceef57e0454/DTXMania/Code/Stage/07.Performance/DrumsScreen/CActPerfDrumsJudgementString.cs#L1495
        public ManiaSkinInfo_Lane[] LaneList = new ManiaSkinInfo_Lane[] {
            new(74f, 4f, DrumColors.Magenta, DrumChannel.China) {
                Secondary = [new(DrumChannel.Splash)],
                JudgementTextPosition = DefaultJudgementTextPosition + 2 * TextOffsetIncrement
            },
            new(56f, 2f, DrumColors.Cyan, DrumChannel.ClosedHiHat) {
                Secondary = [new(DrumChannel.OpenHiHat, Colour4.White), new(DrumChannel.HalfOpenHiHat)]
            },
            new(48f, 2f, DrumColors.Magenta, DrumChannel.HiHatPedal){
                Secondary = [new(DrumChannel.BassDrum, new(147, 69, 255, 255))],
                JudgementTextPosition = DefaultJudgementTextPosition - TextOffsetIncrement
            },
            new(54f, 2f, DrumColors.Yellow, DrumChannel.Snare) {
                Secondary = [new(DrumChannel.SideStick), new(DrumChannel.Rim)]
            },
            new(46f, 2f, DrumColors.Green, DrumChannel.SmallTom) {
                JudgementTextPosition = DefaultJudgementTextPosition + TextOffsetIncrement
            },
            new(60f, 2f, new(147, 69, 255, 255), DrumChannel.BassDrum){
                JudgementTextPosition = DefaultJudgementTextPosition - TextOffsetIncrement
            },
            new(46f, 2f, DrumColors.Red, DrumChannel.MediumTom) {
                JudgementTextPosition = DefaultJudgementTextPosition + TextOffsetIncrement
            },
            new(46f, 2f, DrumColors.Orange, DrumChannel.LargeTom),
            new(64f, 2f, DrumColors.Cyan, DrumChannel.Crash) {
                Secondary = [new(DrumChannel.Ride), new(DrumChannel.RideBell)],
                JudgementTextPosition = DefaultJudgementTextPosition + 2 * TextOffsetIncrement
            }
        };

        public ManiaSkinInfo_LaneList()
        {
            for (var i = 0; i < LaneList.Length; i++)
                LaneList[i].Index = i;
        }

        public ManiaSkinInfo_Lane LaneFor(DrumChannel channel) => LaneList.FirstOrDefault(e => e.Channel == channel);
    }

    public class ManiaSkinInfo_Shutter
    {
        public SkinTexture Texture;
        public float Height;
    }
    public ManiaSkinInfo_Shutter Shutter;

    public class LaneConverter : JsonConverter<ManiaSkinInfo_LaneList>
    {
        public override ManiaSkinInfo_LaneList ReadJson(JsonReader reader, Type objectType,
            ManiaSkinInfo_LaneList res, bool hasExistingValue, JsonSerializer serializer)
        {
            res ??= new();
            reader.Read();
            List<ManiaSkinInfo_Lane> clone = null;
            while (reader.TokenType != JsonToken.EndObject)
            {
                var key = (string)reader.Value;
                var channel = BJsonNote.GetDrumChannel(key);
                reader.Read();
                var lane = res.LaneFor(channel);
                if (lane != null)
                {
                    var oldI = lane.Index;
                    serializer.Populate(reader, lane);
                    // if index changes, we need to sort
                    if (lane.Index != oldI)
                        clone ??= [.. res.LaneList];
                }
                else
                {
                    clone ??= [.. res.LaneList];
                    lane = serializer.Deserialize<ManiaSkinInfo_Lane>(reader);
                    lane.Channel = channel;
                    clone.Add(lane);
                }
                reader.Read();
            }

            if (clone != null)
                res.LaneList = [.. clone.OrderBy(e => e.Index)];

            return res;
        }

        public override void WriteJson(JsonWriter writer, ManiaSkinInfo_LaneList value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            var i = 0;
            foreach (var lane in value.LaneList)
            {
                writer.WritePropertyName(BJsonNote.GetChannelString(lane.Channel));
                lane.Index = i;
                serializer.Serialize(writer, lane);
            }
            writer.WriteEndObject();
        }
    }
}