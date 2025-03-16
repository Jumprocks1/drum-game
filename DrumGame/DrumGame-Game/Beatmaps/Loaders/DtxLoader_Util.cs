
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DrumGame.Game.Channels;
using osu.Framework.Logging;

namespace DrumGame.Game.Beatmaps.Loaders;

// this should only contain simple static methods
public partial class DtxLoader
{
    static int base36(ReadOnlySpan<char> s)
    {
        var o = 0;
        var value = 1;
        for (var i = s.Length - 1; i >= 0; i--)
        {
            var v = char.IsDigit(s[i]) ? s[i] - '0' : (s[i] - 'A' + 10);
            o += v * value;
            value *= 36;
        }
        return o;
    }
    static int hex(ReadOnlySpan<char> s)
    {
        var o = 0;
        var value = 1;
        for (var i = s.Length - 1; i >= 0; i--)
        {
            var v = char.IsDigit(s[i]) ? s[i] - '0' : (s[i] - 'A' + 10);
            o += v * value;
            value *= 16;
        }
        return o;
    }
    static int base36((char, char) s) => base36(s.Item1, s.Item2);
    static int base36(char a, char b) =>
        (char.IsDigit(a) ? a - '0' : (a - 'A' + 10)) * 36 +
        (char.IsDigit(b) ? b - '0' : (b - 'A' + 10));
    static bool shouldIgnore(string code) => code switch
    {
        "DTXC_LANEBINDEDCHIP" or "DTXC_LANEBINDEDCHIP_AL" or "DTXC_CHIPPALETTE" or
        "DTXC_WAVBACKCOLOR" or "GLEVEL" or "DTXVPLAYSPEED" or
        "BACKGROUND" or "RESULTIMAGE" or "HIDDENLEVEL" or
        "BLEVEL" or "STAGEFILE" => true,
        _ => false
    };
    // make sure to use this. We can't use default double.Parse since it doesn't always work with decimals ie `0.5`
    static double ParseDouble(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    static float ParseFloat(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    static bool ApplyDefInfo(List<Def> defs, string localFileName, Beatmap beatmap)
    {
        if (defs != null)
        {
            foreach (var set in defs)
            {
                foreach (var dtx in set.DtxDefs)
                {
                    if (dtx.Value.File == localFileName)
                    {
                        beatmap.DifficultyName = dtx.Value.Label;
                        if (!string.IsNullOrWhiteSpace(set.MapSet))
                            beatmap.MapSetId = set.MapSet;
                        return true;
                    }
                }
            }
        }
        beatmap.DifficultyName = Path.GetFileNameWithoutExtension(localFileName).ToUpper();
        if (beatmap.DifficultyName == "MSTR") beatmap.DifficultyName = "MASTER";
        return true;
    }

    static DrumChannel ChannelMap(string dtxChannel) => dtxChannel switch
    {
        // https://github.com/limyz/DTXmaniaNX/blob/master/DTXMania/Code/Score%2CSong/EChannel.cs
        "11" => DrumChannel.ClosedHiHat,
        "12" => DrumChannel.Snare,
        "13" => DrumChannel.BassDrum,
        "14" => DrumChannel.SmallTom,
        "15" => DrumChannel.MediumTom,
        "16" => DrumChannel.Crash, // should be right crash
        "17" => DrumChannel.LargeTom,
        "18" => DrumChannel.OpenHiHat,
        "19" => DrumChannel.Ride,
        "1A" => DrumChannel.Crash, // should be left crash
        "1B" => DrumChannel.HiHatPedal,
        "1C" => DrumChannel.BassDrum,
        _ => DrumChannel.None
    };
    static int DifficultyInt(string dtxDifficulty) => dtxDifficulty.ToUpper() switch
    { // just used for sorting, value not important
        "NOVICE" => 1,
        "BASIC" => 2,
        "ADVANCED" => 3,
        "EXTREME" => 4,
        "EXPERT" => 5,
        "MASTER" => 6,
        _ => -1
    };
    static IEnumerable<(string code, string value, string comment)> ReadDtxLines(Stream stream)
    {
        // Some maps come in 932 encoding, we will have to see what percentage to figure out if we should change this
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = new StreamReader(stream, encoding: Encoding.GetEncoding(932));
        // using var reader = new StreamReader(stream);
        string fullLine;
        while ((fullLine = reader.ReadLine()) != null)
        {
            if (!fullLine.StartsWith('#')) continue;
            var comment = fullLine.IndexOf(';');
            var commentString = comment == -1 ? null : fullLine[(comment + 1)..];
            var line = comment == -1 ? fullLine.AsSpan(1) : fullLine.AsSpan(1, comment - 1);
            // we split by : or space, prefer :
            var spl = line.IndexOf(":");
            if (spl == -1) spl = line.IndexOf(" ");
            if (spl == -1)
            {
                if (!shouldIgnore(new string(line[..(line.Length - 1)])))
                    Logger.Log($"skipping line: {line}", level: LogLevel.Important);
                continue; // Triggers on DTXC_CHIPPALETTE:
            }
            var code = new string(line[..spl]);
            if (shouldIgnore(code)) continue;
            var value = new string(line[(spl + 1)..].Trim());
            yield return (code, value, commentString);
        }
    }

    static void ReadCharter(Beatmap beatmap, string comment)
    {
        if (beatmap.Mapper != null) return;
        var regexs = new string[] {
            @"\bChart by ([\S]+)",
            @"\bChart: ([\S]+)",
            @"\bDTX by ([\S]+)",
        };
        try
        {
            foreach (var rege in regexs)
            {
                var regex = new Regex(rege, RegexOptions.IgnoreCase);
                var match = regex.Match(comment);
                if (match.Success)
                {
                    beatmap.Mapper = match.Groups[1].Value;
                    return;
                }
            }
        }
        catch (Exception e) { Logger.Error(e, "Error while reading map comments"); }
    }
}

