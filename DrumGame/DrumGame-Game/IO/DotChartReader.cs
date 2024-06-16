using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace DrumGame.Game.IO;

public class DotChart
{
    public List<DotChartSection> Sections;
}

public class DotChartSection
{
    public string Name;
    public List<(string Key, string Value)> Values;
}

public class DotChartReader : IDisposable, IEnumerable<DotChartSection>
{
    public readonly Stream Stream;
    readonly StreamReader Reader;
    public DotChartReader(Stream stream)
    {
        Stream = stream;
        Reader = new StreamReader(stream);
    }
    public DotChart Read()
    {
        return new DotChart();
    }
    public DotChartSection ReadSection()
    {
        var name = Reader.ReadLine();
        if (name == null) return null;
        if (!name.StartsWith('[') || !name.EndsWith(']')) throw new Exception($"Expected [SectionName], got: {name}");
        name = name[1..^1];
        var o = new DotChartSection
        {
            Name = name,
            Values = new()
        };
        var line = Reader.ReadLine();
        if (line != "{") throw new Exception("Expected {, got " + line);
        while ((line = Reader.ReadLine()) != "}")
        {
            var spl = line.Split('=', 2);
            var key = spl[0].Trim();
            var value = spl[1].Trim();
            if (value.StartsWith('"') && value.EndsWith('"')) value = value[1..^1];
            o.Values.Add((key, value));
        }
        return o;
    }
    public void Dispose()
    {
        Reader.Dispose();
        Stream.Dispose();
    }

    public IEnumerator<DotChartSection> GetEnumerator()
    {
        DotChartSection section;
        while ((section = ReadSection()) != null)
        {
            yield return section;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
}