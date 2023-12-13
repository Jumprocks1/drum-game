using System.Text;
using ManagedBass;

namespace DrumGame.Game.Utils;

public class AudioTags
{
    public string Artist;
    public string Title;
}
public static class AudioTagUtil
{
    public static AudioTags GetAudioTags(string filename)
    {
        try
        {
            var tagReader = TagReader.Read(filename);
            if (tagReader == null) return new();

            return new()
            {
                Artist = Clean(tagReader.Artist ?? tagReader.AlbumArtist),
                Title = Clean(tagReader.Title)
            };
        }
        catch { return new(); }
    }

    static string Clean(string tag)
    {
        // these show up a lot in tags for some reason. They are some sort of error characters.
        return tag.Replace("\uFFFD", "").Replace("\uFEFF", "");
    }

    public static string AsciiString(this string input) => input == null ? null : Encoding.ASCII.GetString(
        Encoding.Convert(
            Encoding.UTF8,
            Encoding.GetEncoding(
                Encoding.ASCII.EncodingName,
                new EncoderReplacementFallback(string.Empty),
                new DecoderExceptionFallback()
                ),
            Encoding.UTF8.GetBytes(input)
        )
    );
}

