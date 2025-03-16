using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;

namespace DrumGame.Game.Skinning;

public class NotationJudgementInfo
{
    public class CircleInfo
    {
        [DefaultValue(1000d)]
        public double Duration = 1000d; // ms

        [DefaultValue(1.5f)]
        public float Size = 1.5f; // In staff line units
    }
    public CircleInfo Circle = new();

    public class BarInfo
    {
        public enum BarStyle
        {
            Disabled,
            [Description("Shows a bar judgement on each note after it's hit.\nCan be difficult to see since they will continue scrolling to the left after hit.")]
            Note,
            [Description("Shows 2 bar judgements in front of the judgement line. Split between hands and feet.\nPosition and size can be configured in the skin's JSON file.")]
            Shared2,
            [Description("Shows a bar judgement for each notation lane.\nPosition and size can be configured in the skin's JSON file.")]
            SharedLane,
            [Description("Shows a single bar judgement in front of the judgement line.\nPosition and size can be configured in the skin's JSON file.")]
            Shared
        }
        public double Duration;
        [Display(Name = "Bar Judgement Style",
        Description = "Enables colored bars that show how early or late a note is hit.\nDetailed customization available by clicking the advanced settings button.")]
        public BarStyle Style; // defaults to disabled
        [Description("Bar will not be shown if error is below this value.")]
        public double MinimumError = 8;
        [Description("Bar will be at maximum width for errors at or above this value.\nDefaults to the current perfect window.")]
        public double? MaximumError;
        [Description("Offset in front of the judgement line for shared styles. Units are in quarter notes. Defaults to 1.")]
        public float LaneOffset = 1;
        [Description("Vertical position of bar judgements. 0 is top staff line, 8 is bottom staff life.")]
        public int SharedPosition = 1;
        [Description("Only used when style set to `Shared2`")]
        public int SharedPositionFeet = 7;
        public float MaxWidth;
        public float MaxHeight;
        public float Padding = 0.26f; // ratio on height
        public Colour4 BackgroundColor = DrumColors.Black00;
        public Colour4 EarlyColor = Colour4.Red;
        public Colour4 LateColor = Colour4.Lime;
        public float MinmumAspectRatio = 1.62f;
        public void LoadDefaults(NotationJudgementInfo info)
        {
            // should consider moving this logic to a property and letting the skin values be nullable
            // It originally caused issue since the style could change without this being called, but I change that by forcing a skin reload
            if (Duration == default)
                Duration = Style == BarStyle.Note ? info.Circle.Duration : 1500;
            if (MaxWidth == default)
                MaxWidth = Style == BarStyle.Note ? info.Circle.Size / 2 : 2;
            if (MaxHeight == default)
                MaxHeight = Style == BarStyle.Note ? 0.3f : 0.5f;
        }
    }
    public BarInfo Bar = new();

    public void LoadDefaults()
    {
        Bar?.LoadDefaults(this);
    }
}
