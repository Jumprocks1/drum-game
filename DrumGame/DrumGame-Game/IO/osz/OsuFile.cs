using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace DrumGame.Game.IO.Osz;

public class OsuFile
{
    public GeneralSection General = new GeneralSection();
    public MetadataSection Metadata = new MetadataSection();
    public List<Event> Events = new();
    public List<TimingPoint> TimingPoints = new();
    public List<HitObject> HitObjects = new();
    public object GetSection(string section) => section switch
    {
        "General" => General,
        // "Editor" => ": ",
        "Metadata" => Metadata,
        // "Difficulty" => ":",
        "Events" => Events,
        "TimingPoints" => TimingPoints,
        // "Colours" => " : ",
        "HitObjects" => HitObjects,
        _ => null
    };
    public static string Separator(string section) => section switch
    {
        "General" => ": ",
        "Editor" => ": ",
        "Metadata" => ":",
        "Difficulty" => ":",
        "Events" => ",",
        "TimingPoints" => ",",
        "Colours" => " : ",
        "HitObjects" => ",",
        _ => null
    };

    public static void SetField(FieldInfo field, object target, string value)
    {
        if (field == null) return;
        if (field.FieldType == typeof(int) || field.FieldType.IsEnum)
        {
            field.SetValue(target, int.Parse(value));
        }
        else if (field.FieldType == typeof(decimal))
        {
            field.SetValue(target, decimal.Parse(value));
        }
        else if (field.FieldType == typeof(double))
        {
            field.SetValue(target, double.Parse(value, CultureInfo.InvariantCulture));
        }
        else
        {
            field.SetValue(target, value);
        }
    }

    public OsuFile(string path) : this(File.OpenRead(path)) { }
    public OsuFile(Stream stream) : this(new StreamReader(stream, Encoding.UTF8)) { }

    public OsuFile(StreamReader reader)
    {
        using (reader)
        {
            object section = null;
            string separator = null;
            Type sectionType = null;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("//") || line.Length == 0) continue;
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    var sectionName = line.Substring(1, line.Length - 2);
                    separator = Separator(sectionName);
                    section = GetSection(sectionName);
                    sectionType = section?.GetType();
                }
                else
                {
                    if (section != null)
                    {
                        if (separator == ",")
                        {
                            string[] split;
                            if (section == Events)
                            {
                                split = line.Split(separator, 3);
                            }
                            else if (section == HitObjects)
                            {
                                split = line.Split(separator, 6);
                            }
                            else
                            {
                                split = line.Split(separator);
                            }
                            var t = sectionType.GetGenericArguments()[0];
                            var fields = t.GetFields().OrderBy(field => field.MetadataToken).ToArray();
                            var o = Activator.CreateInstance(t);
                            for (var i = 0; i < split.Length; i++) SetField(fields[i], o, split[i]);
                            sectionType.GetMethod("Add").Invoke(section, new[] { o });
                        }
                        else
                        {
                            var split = line.Split(separator, 2);
                            if (split.Length == 2)
                            {
                                SetField(sectionType.GetField(split[0]), section, split[1]);
                            }
                        }
                    }
                }
            }
        }
    }
    public class GeneralSection
    {
        public string AudioFilename;
        public int AudioLeadIn;
        public Mode Mode;
    }
    public enum Mode
    {
        Osu = 0,
        Taiko = 1,
        Catch = 2,
        Mania = 3
    }
    public class MetadataSection
    {
        public string Title;
        public string Version;
        public string Artist;
        public string Creator;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct Event
    {
        public string EventType;
        public int StartTime;
        public string Parameters;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct TimingPoint
    {
        public double Time;
        public decimal BeatLength;
        public int Meter;
        public int SampleSet;
        public int SampleIndex;
        public int Volume;
        public int Uninherited;
        public int Effects;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct HitObject
    {
        public int X;
        public int Y;
        public int Time;
        public HitType Type;
        public HitSound HitSound;
        public string Params;
    }
    [Flags]
    public enum HitSound
    {
        None = 0, // also treated as Normal
        Normal = 1,
        Whistle = 2,
        Finish = 4,
        Clap = 8
    }
    [Flags]
    public enum HitType
    {
        None = 0,
        HitCircle = 1,
        Slider = 2,
        NewCombo = 4,
        Spinner = 8,
        Hold = 128
    }

}

