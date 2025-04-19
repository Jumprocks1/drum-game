using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;

namespace DrumGame.Game.Components;

// TODO TextFlowContainer is bad. We should just use FillFlowContainer
public class MarkupText : TextFlowContainer, IHasText
{
    public static string Color(string inner, Colour4 color)
    {
        return $"<c{color.ToHex()}>{inner}</c>";
    }
    public static string Color(string inner, Colour4? color)
        => color is Colour4 c ? Color(inner, c) : inner;
    public MarkupText(Action<SpriteText> defaultCreationParameters = null) : base(defaultCreationParameters)
    {
        AutoSizeAxes = Axes.Both;
    }
    protected override SpriteText CreateSpriteText()
    {
        var s = base.CreateSpriteText();
        if (Font is FontUsage f)
            s.Font = f;
        return s;
    }
    public MarkupText(string source) : this()
    {
        Data = source;
        Validate();
    }

    public string _data;
    public string Data
    {
        get => _data; set
        {
            if (value == _data) return;
            _data = value;
            invalid = true;
        }
    }

    LocalisableString IHasText.Text { get => Data; set { Data = value.ToString(); Validate(); } }

    bool invalid;

    protected override void Update()
    {
        Validate();
        base.Update();
    }

    public static string Escape(string s)
    {
        return s.Replace("\\", "\\\\").Replace("<", "\\<");
    }
    public static string Escape(object s) => Escape(s.ToString());

    void Validate()
    {
        if (invalid)
        {
            if (Flow.Children.Count > 0) Clear();
            AddData(_data);
            invalid = false;
        }
    }

    static Dictionary<string, Colour4> ColorLookup;

    public FontUsage? Font;

    Dictionary<string, Colour4> GetColorLookup()
    {
        if (ColorLookup != null) return ColorLookup;
        // ~2ms
        ColorLookup = new();
        var props = typeof(DrumColors).GetProperties(BindingFlags.Public | BindingFlags.Static);
        foreach (var prop in props)
            if (prop.PropertyType == typeof(Colour4))
                ColorLookup[prop.Name.ToLowerInvariant()] = (Colour4)prop.GetValue(null);
        var fields = typeof(DrumColors).GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
            if (field.FieldType == typeof(Colour4))
                ColorLookup[field.Name.ToLowerInvariant()] = (Colour4)field.GetValue(null);
        return ColorLookup;
    }

    void AddData(string data)
    {
        if (data == null) return;
        var buffer = new StringBuilder();
        var color = new Stack<Colour4>();


        void Add()
        {
            var col = color.Count > 0 ? color.Peek() : Colour4.White;
            AddText(buffer.ToString(), e => { e.Colour = col; });
            buffer.Clear();
        }

        for (var i = 0; i < data.Length; i++)
        {
            // currently only \ and < are special characters
            var c = data[i];
            if (c == '\\' && i < data.Length - 1)
            {
                buffer.Append(data[i + 1]);
                i += 1;
            }
            else if (c == '<')
            {
                var tag = "";
                var close = i < data.Length - 1 && data[i + 1] == '/';
                i += close ? 1 : 0;
                while (i < data.Length)
                {
                    i += 1;
                    var c2 = data[i];
                    if (c2 == '>') break;
                    tag += c2;
                }
                if (close)
                {
                    if (buffer.Length > 0) Add();
                    if ("color".StartsWith(tag))
                        if (color.Count > 0)
                            color.Pop();
                }
                else
                {
                    if (buffer.Length > 0) Add();
                    if (tag == "miss") color.Push(Util.HitColors.Miss);
                    else if (tag == "bad") color.Push(Util.HitColors.Bad);
                    else if (tag == "good") color.Push(Util.HitColors.Good);
                    else if (tag == "perfect") color.Push(Util.HitColors.Perfect);
                    else if (tag == "faded") color.Push(DrumColors.FadedText);
                    else if (tag.StartsWith("c#"))
                    {
                        if (Colour4.TryParseHex(tag[2..], out var hexC))
                            color.Push(hexC);
                    }
                    else
                    {
                        if (GetColorLookup().TryGetValue(tag.ToLowerInvariant(), out var lookupColor))
                            color.Push(lookupColor);
                    }
                }
            }
            else
            {
                buffer.Append(c);
            }
        }
        if (buffer.Length > 0) Add();
    }
}