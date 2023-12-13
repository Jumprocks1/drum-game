using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using osu.Framework.Graphics;

namespace DrumGame.Game.Stores.Skins;

public interface IChipInfo
{
    public SkinTexture Adornment { get; set; }
    public DrumChannel Channel { get; set; }
    public Colour4 Color { get; set; }
    public SkinTexture Chip { get; set; }
}
public class ManiaSkinInfo
{
    public static double ScrollRate => Util.ConfigManager.Get<double>(DrumGameSetting.ManiaScrollMultiplier) * 0.5d / 1000; // measured in vertical screens per millisecond

    public float Width = 0.55f;
    public float JudgementPosition = 0.15f;
    public Colour4 JudgementColor => DrumColors.Yellow;
    public float JudgementThickness = 0.01f;
    public float ChipThickness;
    public Colour4 BeatLineColor = new(200, 200, 200, 255);
    public Colour4 BackgroundColor = new(16, 16, 16, 255);
    public float BeatLineThickness = 0.002f;
    public float MeasureLineThickness;
    public Colour4 BorderColor = new(100, 100, 100, 255);

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
        public float Width;
        public int Index; // only used during skin loading
        [JsonIgnore] public DrumChannel Channel { get; set; }
        public Colour4 Color { get; set; }
        public SkinTexture Chip { get; set; }
        public SkinTexture Adornment { get; set; }
        public SkinTexture Icon;
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

    public void LoadDefaults()
    {
        foreach (var lane in Lanes.LaneList)
            lane.LoadDefaults(this);
        if (ChipThickness == default)
            ChipThickness = JudgementThickness * 1.5f;
        if (MeasureLineThickness == default)
            MeasureLineThickness = BeatLineThickness * 3;
    }

    public class ManiaSkinInfo_LaneList
    {
        public ManiaSkinInfo_Lane[] LaneList = new ManiaSkinInfo_Lane[] {
            new(74f, 4f, DrumColors.Magenta, DrumChannel.China) {
                Secondary = [new(DrumChannel.Splash)]
            },
            new(56f, 2f, DrumColors.Cyan, DrumChannel.ClosedHiHat) {
                Secondary = [new(DrumChannel.OpenHiHat, Colour4.White), new(DrumChannel.HalfOpenHiHat)]
            },
            new(48f, 2f, DrumColors.Magenta, DrumChannel.HiHatPedal){
                Secondary = [new(DrumChannel.BassDrum, new(147, 69, 255, 255))]
            },
            new(54f, 2f, DrumColors.Yellow, DrumChannel.Snare) {
                Secondary = [new(DrumChannel.SideStick), new(DrumChannel.Rim)]
            },
            new(46f, 2f, DrumColors.Green, DrumChannel.SmallTom),
            new(60f, 2f, new(147, 69, 255, 255), DrumChannel.BassDrum),
            new(46f, 2f, DrumColors.Red, DrumChannel.MediumTom),
            new(46f, 2f, DrumColors.Orange, DrumChannel.LargeTom),
            new(64f, 2f, DrumColors.Cyan, DrumChannel.Crash) {
                Secondary = [new(DrumChannel.Ride), new(DrumChannel.RideBell)]
            }
        };

        public ManiaSkinInfo_Lane LaneFor(DrumChannel channel) => LaneList.FirstOrDefault(e => e.Channel == channel);
    }

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
                    serializer.Populate(reader, lane);
                else
                {
                    clone ??= [.. res.LaneList];
                    lane = serializer.Deserialize<ManiaSkinInfo_Lane>(reader);
                    lane.Channel = channel;
                    clone.Insert(lane.Index, lane);
                }
                reader.Read();
            }

            if (clone != null)
                res.LaneList = [.. clone];

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