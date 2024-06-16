
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DrumGame.Game.Beatmaps.Data;
using DrumGame.Game.Browsers.BeatmapSelection;
using DrumGame.Game.Commands;
using DrumGame.Game.Stores;
using DrumGame.Game.Stores.DB;
using NAudio.Wave;
using NAudio.Wave.Asio;
using Newtonsoft.Json.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osuTK;
using osu.Framework.Development;
using System.Reflection;
using System.IO.Compression;
using DrumGame.Game.Beatmaps;
using osu.Framework.Lists;
using osu.Framework.Threading;
using osu.Framework.Utils;
using DrumGame.Game.Containers;
using DrumGame.Game.Components.Overlays;
using DrumGame.Game.Skinning;
using System.Threading;
using osu.Framework.Input.States;
using System.Numerics;
using System.ComponentModel;
using osu.Framework.Allocation;
using System.Globalization;

namespace DrumGame.Game.Utils;

public static class Util
{
    public static DrumDbContext GetDbContext() => DrumGame.DbStorage.GetContext();
    public static CommandController CommandController;
    public static CommandPaletteContainer Palette => CommandController.Palette;
    public static DrumGameConfigManager ConfigManager;
    public static Skin Skin { get; set; }
    public static Skin.Skin_HitColors HitColors => Skin.HitColors;
    public static GameHost Host;
    public static DrumInputManager InputManager;
    public static KeyPressOverlay KeyPressOverlay;
    public static MouseState Mouse => InputManager.CurrentState.Mouse;
    public static NotificationOverlay NotificationOverlay => Palette.NotificationOverlay;
    public static FileSystemResources Resources => DrumGame.FileSystemResources;
    public static MapStorage MapStorage => DrumGame.MapStorage;
    public static GameThread UpdateThread => Host.UpdateThread;
    public static GameThread AudioThread => Host.AudioThread;
    public static DrumContextMenuContainer ContextMenuContainer => Palette.FindClosestParent<DrumContextMenuContainer>();
    public static DrumPopoverContainer PopoverContainer => Palette.FindClosestParent<DrumPopoverContainer>();
    public static DrumGameGameBase DrumGame;
    public static ExecutionMode ExecutionMode => (ExecutionMode)typeof(ThreadSafety).GetField("ExecutionMode", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    public static bool IsSingleThreaded => ExecutionMode == ExecutionMode.SingleThread;
    class Timer : IDisposable
    {
        Action OnComplete;
        public Timer(Action onComplete) { OnComplete = onComplete; }
        public void Dispose() => OnComplete();
    }
    public static IDisposable WriteTime(string label = null) // make sure to use with a using statement
    {
        var watch = Stopwatch.StartNew();
        return new Timer(() =>
        {
            watch.Stop();
            Console.WriteLine($"{label}{watch.Elapsed.TotalMilliseconds}ms");
        });
    }
    public static void WriteTime(Action method, string label = null)
    {
        var watch = Stopwatch.StartNew();
        method();
        watch.Stop();
        Console.WriteLine($"{label}{watch.Elapsed.TotalMilliseconds}ms");
    }
    public static T WriteTime<T>(Func<T> method)
    {
        var watch = Stopwatch.StartNew();
        var o = method();
        watch.Stop();
        Console.WriteLine($"{watch.Elapsed.TotalMilliseconds}ms");
        return o;
    }

    public static string FromPascalCase(this string s)
    {
        var o = new StringBuilder(s.Length * 2);
        o.Append(s[0]);
        for (int i = 1; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsDigit(c) || char.IsUpper(c)) o.Append(' ');
            o.Append(c);
        }
        return o.ToString();
    }
    public static int Mod(this int a, int n) => ((a %= n) < 0) ? a + n : a;
    public static double Mod(this double a, double n) => ((a %= n) < 0) ? a + n : a;

    // Warning: this can return the index of an existing item with the same value.
    public static int InsertSortedPosition<T, K>(this List<T> list, K o) where T : IComparable<K> => BinarySearch(list, o);
    // should only be used with continous values since this will not return safe starts/ends
    // expected to be used with a simple for loop
    public static (int, int) FindRangeContinuous<T, K>(this List<T> list, K start, K end) where T : IComparable<K>
        => (list.BinarySearch(start), list.BinarySearch(end));
    public static (int, int) FindRangeDiscrete<T, K>(this List<T> list, K start, K end) where T : IComparable<K>
        => (list.BinarySearchFirst(start), list.BinarySearchThrough(end));
    // TODO we can probably convert this to use the built in version
    // Returns position of element if in list
    // If multiple in list, returns arbiritary index
    // If not in list, returns index where it should be inserted
    // ex: 0,1,2,3,4,5,6,7,8,9
    // search(5) => 5
    // search(5.5) => 6
    // search(0) => 0
    // search(-1) => 0
    // search(9) => 9
    // search(10) => 10
    public static int BinarySearch<T, K>(this List<T> list, K o) where T : IComparable<K>
    {
        var min = 0;
        var max = list.Count;
        var pos = (min + max) / 2;
        while (min < max)
        {
            var c = list[pos].CompareTo(o);
            if (c == 0)
            {
                break;
            }
            else if (c > 0)
            {
                max = pos;
            }
            else
            {
                min = pos + 1;
            }
            pos = (min + max) / 2;
        }
        return pos;
    }
    // Guarentees that list[result] will not be equal to o
    // If used for insertions, this means the new value will always be after all existing values of the same time
    // Works well for played through logic - the resulting index will be a time that has NOT yet been reached
    // This means result - 1 will be an index that HAS been reached
    public static int BinarySearchThrough<T, K>(this List<T> list, K o) where T : IComparable<K>
    {
        var i = list.BinarySearch(o);
        while (i < list.Count && list[i].CompareTo(o) == 0) i += 1;
        return i;
    }
    // Returns the first index of o, or index of item after where o would appear
    public static int BinarySearchFirst<T, K>(this List<T> list, K o) where T : IComparable<K>
    {
        var i = list.BinarySearch(o);
        while (i > 0 && list[i - 1].CompareTo(o) == 0) i -= 1;
        return i;
    }

    public static double ExpLerp(double current, double target, double pow, double dt, double linearStep = 0)
    {
        if (current == target) return current;
        // could also use blend = Math.Exp(-decay * dt) instead
        // decay = -log(pow)
        var blend = Math.Pow(pow, dt); // 0.99 means we will move 1% percent towards target for each ms
        current = target + (current - target) * blend;

        if (linearStep > 0)
        {
            linearStep *= dt; // this gives us a very small linear movement, which helps stabilize
            var diff = target - current;
            if (Math.Abs(diff) < linearStep)
            {
                current = target;
            }
            else
            {
                current += Math.Sign(diff) * linearStep;
            }
        }

        return current;
    }

    public static void Destroy<T, K>(this Container<T> container, ref K drawable) where T : Drawable where K : T
    {
        container.Remove(drawable, true);
        drawable = null;
    }

    public static string SafeFullPath(string path, string parentPath)
    {
        var fullPath = Path.GetFullPath(path, parentPath);
        if (!fullPath.StartsWith(parentPath))
        {
            Logger.Log($"Failed to load resource: {path}, not inside {parentPath}", level: LogLevel.Error);
            return null;
        }
        return fullPath;
    }

    public static string RelativeOrNullPath(string path, string parentPath)
    {
        var fullPath = Path.GetFullPath(path, parentPath);
        if (!fullPath.StartsWith(parentPath)) return null;
        return Path.GetRelativePath(parentPath, path);
    }

    public static bool checkFileReady(string filename)
    {
        try
        {
            using FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None);
            return inputStream.Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static Matrix4 ReadMatrix4(this BinaryReader binaryReader) => new Matrix4(
        binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(),
        binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(),
        binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(),
        binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle()
    );

    public static void WriteJson(object o) =>
        Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(o, Newtonsoft.Json.Formatting.Indented));

    public static List<T> ListFromToken<T>(JToken token, Func<JObject, T> cons)
    {
        var o = new List<T>();
        if (token != null)
        {
            if (token is JArray array)
            {
                foreach (var t in array)
                {
                    if (t is JObject jo) o.Add(cons(jo));
                }
            }
            else if (token is JObject jo)
            {
                o.Add(cons(jo));
            }
        }
        return o;
    }

    public static JToken ListToToken<T>(List<T> list, Func<T, JToken> token, Func<T, JToken> single = null) where T : ITickTime
    {
        if (list == null || list.Count == 0)
        {
            return null;
        }
        else if (list.Count == 1)
        {
            return (single ?? token)(list[0]);
        }
        else
        {
            return new JArray(list.Select(token));
        }
    }
    public static ModifierKey Modifier(this UIEvent e) => e.CurrentState.Keyboard.Modifier();
    public static ModifierKey Modifier(this KeyboardState e)
    {
        var modifier = ModifierKey.None;
        if (e.ControlPressed) modifier |= ModifierKey.Ctrl;
        if (e.ShiftPressed) modifier |= ModifierKey.Shift;
        if (e.AltPressed) modifier |= ModifierKey.Alt;
        return modifier;
    }

    public static bool AudioExtension(string ext) => ext switch
    {
        ".mp3" => true,
        ".m4a" => true,
        ".ogg" => true,
        ".webm" => true,
        ".flac" => true,
        ".wav" => true,
        _ => false
    };
    public static bool ArchiveExtension(string ext) => ext switch
    {
        ".zip" => true,
        _ => false
    };
    public static bool VideoExtension(string ext) => ext switch
    {
        ".mp4" => true,
        ".mkv" => true,
        _ => false
    };

    public static int GCD(int a, int b)
    {
        while (b > 0)
        {
            var rem = a % b;
            a = b;
            b = rem;
        }
        return a;
    }
    public static string FormatTime(double ms)
    {
        var t = ms > 0 ? (int)ms / 1000 : 0;
        var d = t / 60;
        return $"{d}:{t - d * 60:00}";
    }
    public static double? ParseTime(string time, CultureInfo culture = null)
    {
        culture ??= CultureInfo.InvariantCulture;
        var spl = time.Split(":", options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        try
        {
            if (spl.Length == 1)
            {
                return double.Parse(spl[0], culture) * 1000;
            }
            else if (spl.Length == 2)
            {
                return double.Parse(spl[0], culture) * 1000 * 60 + double.Parse(spl[1], culture) * 1000;
            }
            else if (spl.Length >= 3)
            {
                return double.Parse(spl[0], culture) * 1000 * 60 + double.Parse(spl[1], culture) * 1000 + double.Parse(spl[2], culture);
            }
        }
        catch { }
        return null;
    }

    public static HashSet<T> AsHashSet<T>(this IEnumerable<T> e) => e as HashSet<T> ?? e.ToHashSet();
    public static List<T> AsList<T>(this IEnumerable<T> e) => e as List<T> ?? e.ToList();
    public static T[] AsArray<T>(this IEnumerable<T> e) => e as T[] ?? e.ToArray();

    public static V[] Map<T, V>(this T[] array, Func<T, V> map)
    {
        var o = new V[array.Length];
        for (var i = 0; i < array.Length; i++) o[i] = map(array[i]);
        return o;
    }

    public static string ToFilename(this string s, string ext = null)
    {
        var o = new StringBuilder();
        var lastChar = '-';
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) && char.IsAscii(c))
            {
                if (lastChar != '-' && !char.IsUpper(lastChar) && char.IsUpper(c))
                {
                    o.Append('-');
                }
                o.Append(char.ToLower(c));
                lastChar = c;
            }
            else
            {
                if (lastChar != '-') o.Append(lastChar = '-');
            }
        }
        while (o.Length > 0 && o[^1] == '-') o.Length -= 1; // remove trailing `-`
        if (ext != null) o.Append(ext);
        return o.ToString();
    }

    public static T Pop<T>(this List<T> list)
    {
        var o = list[^1];
        list.RemoveAt(list.Count - 1);
        return o;
    }

    public static IOrderedQueryable<ReplayInfo> Sort(this IQueryable<ReplayInfo> source, SortMethod sort) => sort switch
    {
        SortMethod.Accuracy => source.OrderByDescending(e => e.AccuracyHit),
        SortMethod.Score => source.OrderByDescending(e => e.Score),
        SortMethod.Time => source.OrderByDescending(e => e.CompleteTimeTicks),
        SortMethod.Misses => source.OrderBy(e => e.Miss),
        SortMethod.MaxCombo => source.OrderByDescending(e => e.MaxCombo),
        _ => null
    };
    public static IOrderedEnumerable<ReplayDisplay> Sort(this IEnumerable<ReplayDisplay> source, SortMethod sort) => sort switch
    {
        SortMethod.Accuracy => source.OrderByDescending(e => e.ReplayInfo.AccuracyHit),
        SortMethod.Score => source.OrderByDescending(e => e.ReplayInfo.Score),
        SortMethod.Time => source.OrderByDescending(e => e.ReplayInfo.CompleteTimeTicks),
        SortMethod.Misses => source.OrderBy(e => e.ReplayInfo.Miss),
        SortMethod.MaxCombo => source.OrderByDescending(e => e.ReplayInfo.MaxCombo),
        _ => null
    };

    public static string DisplayName<T>(this T value) where T : struct, Enum
    {
        var s = value.ToString();
        var f = typeof(T).GetField(s);
        if (f == null) return s.FromPascalCase();
        var attr = f.GetCustomAttribute<DisplayAttribute>();
        if (attr != null) return attr.Name;
        return s.FromPascalCase();
    }
    public static string MarkupDescription<T>(this T value) where T : struct, Enum
    {
        var s = value.ToString();
        return MarkupDescription(typeof(T).GetField(s));
    }
    public static string MarkupDescription(MemberInfo member)
    {
        if (member != null)
        {
            var attr = member.GetCustomAttribute<DisplayAttribute>();
            if (attr != null && attr.Description != null) return attr.Description;
            var descAttr = member.GetCustomAttribute<DescriptionAttribute>();
            if (descAttr != null) return descAttr.Description;
        }
        return null; // if not explicit description, we have nothing to default to
    }

    public static void EnsureExists(string path) { if (!Directory.Exists(path)) Directory.CreateDirectory(path); }

    public static int GetAsInterleavedSamples(this AsioAudioAvailableEventArgs args, float[] samples, int offset)
    {
        var channels = args.InputBuffers.Length;
        if ((samples.Length - offset) < args.SamplesPerBuffer * channels) throw new ArgumentException("Buffer not big enough");
        int index = offset;
        unsafe
        {
            // TD-27 seems to use Int32LSB
            if (args.AsioSampleType == AsioSampleType.Int32LSB)
            {
                for (int n = 0; n < args.SamplesPerBuffer; n++)
                    for (int ch = 0; ch < channels; ch++)
                        samples[index++] = *((int*)args.InputBuffers[ch] + n) / (float)Int32.MaxValue;
            }
            else if (args.AsioSampleType == AsioSampleType.Float32LSB)
            {
                for (int n = 0; n < args.SamplesPerBuffer; n++)
                    for (int ch = 0; ch < channels; ch++)
                        samples[index++] = *((float*)args.InputBuffers[ch] + n);
            }
            else throw new NotImplementedException(string.Format("ASIO Sample Type {0} not supported", args.AsioSampleType));
        }
        return args.SamplesPerBuffer * channels;
    }

    static bool FFmpegLoaded = false;
    // this overrides the location of FFmpeg to wherever we specify in our config
    // if we call this function, the VideoDecoder used will also use this FFmpeg location
    // The reason this is important is because the FFmpeg shipped with osu!framework doesn't have x264 encoding
    public static void LoadFFmpeg()
    {
        if (FFmpegLoaded) return;
        FFmpegLoaded = true;
        var location = ConfigManager.FFmpegLocation.Value;
        if (!string.IsNullOrWhiteSpace(location))
        {
            // this sets FFmpeg.AutoGen.ffmpeg.RootPath, which is also used by VideoDecoder.cs
            FFMediaToolkit.FFmpegLoader.FFmpegPath = location;
            FFMediaToolkit.FFmpegLoader.LoadFFmpeg();
        }
    }

    static bool PathEquals(string path1, string path2)
    {
        if (path1.Length != path2.Length) return false;
        for (var i = 0; i < path1.Length; i++)
        {
            var a = path1[i];
            if (a == '\\') a = '/';
            var b = path2[i];
            if (b == '\\') b = '/';
            if (a != b) return false;
        }
        return true;
    }

    public static ZipArchiveEntry FindEntry(this ZipArchive archive, string path) =>
        archive.Entries.FirstOrDefault(e => PathEquals(e.FullName, path));

    // subpath should be like `resources/`
    public static void ExtractToDirectory(this ZipArchive archive, string subPath, string destinationDirectory)
    {
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith(subPath) && entry.Name != string.Empty)
            {
                var stripped = entry.FullName.Substring(subPath.Length);
                var outputPath = Path.Join(subPath, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                entry.ExtractToFile(outputPath);
            }
        }
    }

    // copies an audio file either directly or from a zip file and returns the beatmap-relative audio path
    // returns null on failure
    public static string CopyAudio(string file, Beatmap beatmap)
    {
        var extension = Path.GetExtension(file);
        if (Util.ArchiveExtension(extension))
        {
            using var zip = ZipFile.OpenRead(file);
            foreach (var entry in zip.Entries)
            {
                if (Util.AudioExtension(Path.GetExtension(entry.Name)))
                {
                    try
                    {
                        var relativePath = "audio/" + Path.GetFileName(entry.Name);
                        var fullPath = beatmap.FullAssetPath(relativePath);
                        if (!File.Exists(fullPath))
                        {
                            entry.ExtractToFile(fullPath);
                            return relativePath;
                        }
                        else if (File.GetLastWriteTimeUtc(fullPath) == entry.LastWriteTime)
                        {
                            return relativePath;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Error when extracting zip file");
                    }
                }
            }
        }
        else if (Util.AudioExtension(extension))
            return Util.DrumGame.MapStorage.StoreExistingFile(file, beatmap, "audio");
        return null;
    }

    public static void RevealInFileExplorer(string path) => Host?.PresentFileExternally(path);

    public static IEnumerable<T> FindAll<T>(CompositeDrawable parent) where T : Drawable
    {
        var queue = new Queue<CompositeDrawable>();
        queue.Enqueue(parent);
        var t = typeof(CompositeDrawable);
        var prop = t.GetField("internalChildren", BindingFlags.Instance | BindingFlags.NonPublic);
        CompositeDrawable e;
        while (queue.TryDequeue(out e))
        {
            var children = (SortedList<Drawable>)prop.GetValue(e);
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is T r) yield return r;
                else if (children[i] is CompositeDrawable cd) queue.Enqueue(cd);
            }
        }
    }
    // expensive
    public static T Find<T>(CompositeDrawable parent) where T : Drawable
    {
        var queue = new Queue<CompositeDrawable>();
        queue.Enqueue(parent);
        var t = typeof(CompositeDrawable);
        var prop = t.GetField("internalChildren", BindingFlags.Instance | BindingFlags.NonPublic);
        while (queue.TryDequeue(out var e))
        {
            var children = (SortedList<Drawable>)prop.GetValue(e);
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] is T r) return r;
                else if (children[i] is CompositeDrawable cd) queue.Enqueue(cd);
            }
        }
        return null;
    }

    public static T GetParent<T>(Drawable drawable)
    {
        while ((drawable = drawable.Parent) != null)
            if (drawable is T t) return t;
        return default;
    }

    public static void ForceThread(GameThread thread)
    {
        Debug.Assert(ExecutionMode == ExecutionMode.SingleThread);
        typeof(GameThread).GetMethod("MakeCurrent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(thread, null);
    }

    public static ZipArchiveEntry GetEntryCaseless(this ZipArchive archive, string entry)
        => archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, entry, StringComparison.InvariantCultureIgnoreCase));

    public static bool TryPop<T>(this List<T> list, out T o)
    {
        if (list.Count == 0)
        {
            o = default;
            return false;
        }
        o = list[^1];
        list.RemoveAt(list.Count - 1);
        return true;
    }

    public static T Random<T>(this IList<T> list) => list[RNG.Next(list.Count)];

    public static Clipboard Clipboard => Host.Dependencies.Get<Clipboard>();
    public static string ShortClipboard
    {
        get
        {
            var t = Clipboard?.GetText();
            if (t != null && t.Length < 500) return t;
            return null;
        }
    }
    public static void SetClipboard(string text) => Clipboard.SetText(text);
    public static Stopwatch StartTime;
    public static void CheckStartDuration()
    {
        if (StartTime != null && StartTime.IsRunning)
        {
            StartTime.Stop();
            // 1000ms is a good target
            Logger.Log($"Startup took {Util.StartTime.Elapsed.TotalMilliseconds}ms", level: LogLevel.Important);
        }
    }

    public static void WaitForDebugger()
    {
        while (!Debugger.IsAttached) System.Threading.Thread.Sleep(100);
    }

    public static Type GetNullableType(this Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type; // avoid type becoming null
        if (type.IsValueType)
            return typeof(Nullable<>).MakeGenericType(type);
        else
            return type;
    }

    public static Colour4 DarkenOrLighten(this Colour4 colour, float amount)
    {
        var grey = colour.R + colour.G + colour.B;
        if (grey == 0) return new Colour4(amount, amount, amount, colour.A);
        else if (grey < 1.5f) return colour.Lighten(amount);
        else return colour.Darken(amount);
    }

    public static bool IsLocal => VersionString.StartsWith("local");

    static string _versionString;
    public static string VersionString
    {
        get
        {
            if (_versionString != null) return _versionString;
            var assembly = Assembly.GetEntryAssembly();
            var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (version == null || version.InformationalVersion == "0.0.0")
                _versionString = "local " + (DebugUtils.IsDebugBuild ? @"debug" : @"release");
            else
                _versionString = version.InformationalVersion;
            return _versionString;
        }
    }

    public static (int, int) ToFraction(double value, double maxError)
    {
        var sign = Math.Sign(value);

        if (sign == -1)
            value = Math.Abs(value);

        var n = (int)Math.Floor(value);
        value -= n;

        if (value < maxError)
            return (sign * n, 1);

        // this produces weird results simply because we are expecting a fraction less than 1, but this can return 1/1
        // if (1 - maxError < value)
        //     return (sign * (n + 1), 1);

        // The lower fraction is 0/1
        var lower_n = 0;
        var lower_d = 1;

        // The upper fraction is 1/1
        var upper_n = 1;
        var upper_d = 1;

        while (true)
        {
            // The middle fraction is (lower_n + upper_n) / (lower_d + upper_d)
            var middle_n = lower_n + upper_n;
            var middle_d = lower_d + upper_d;

            if (middle_d * (value + maxError) < middle_n)
            {
                // real + error < middle : middle is our new upper
                upper_n = middle_n;
                upper_d = middle_d;
            }
            else if (middle_n < (value - maxError) * middle_d)
            {
                // middle < real - error : middle is our new lower
                lower_n = middle_n;
                lower_d = middle_d;
            }
            else
            {
                // Middle is our best fraction
                return ((n * middle_d + middle_n) * sign, middle_d);
            }
        }
    }

    public static string MD5(params string[] strings)
    {
        var len = strings.Sum(e => e.Length);
        var buffer = new byte[len * 2];
        var i = 0;
        foreach (var s in strings)
            i += Encoding.Unicode.GetBytes(s, 0, s.Length, buffer, i);
        return Convert.ToHexString(System.Security.Cryptography.MD5.HashData(buffer));
    }
    public static string MD5(Stream stream)
    {
        using var s = stream;
        return Convert.ToHexString(System.Security.Cryptography.MD5.HashData(s));
    }
    public static string MD5(IEnumerable<string> strings)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        foreach (var s in strings)
        {
            if (s == null) continue;
            var bytes = Encoding.Unicode.GetBytes(s);
            md5.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        md5.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(md5.Hash);
    }
    public static void Google(string search) => Host.OpenUrlExternally($"https://google.com/search?q={Uri.EscapeDataString(search)}");
    public static void YouTube(string search) => Host.OpenUrlExternally($"https://www.youtube.com/results?search_query={Uri.EscapeDataString(search)}");
    static ThreadedTaskScheduler _loadScheduler;
    public static ThreadedTaskScheduler LoadScheduler => _loadScheduler ??= (ThreadedTaskScheduler)(typeof(CompositeDrawable)
        .GetField("SCHEDULER_STANDARD", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));

    public static event Action OnGameDisposed;
    public static void GameDisposed()
    {
        OnGameDisposed?.Invoke();
        OnGameDisposed = null;
    }

    public static bool TryCancel(this ScheduledDelegate scheduled)
    {
        // use to cancel a waiting task
        // useful because it prevents halting the calling thread
        if (scheduled == null) return true;

        var internalRunLock = typeof(ScheduledDelegate).GetField("runLock", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scheduled);

        if (Monitor.TryEnter(internalRunLock)) // this prevents scheduled.State from changing
        {
            try
            {
                if (scheduled.State != ScheduledDelegate.RunState.Running)
                {
                    scheduled.Cancel();
                    return true;
                }
            }
            finally
            {
                Monitor.Exit(internalRunLock);
            }
        }
        return false;
    }

    public static ScheduledDelegate EnsureUpdateThread(Action action) => UpdateThread.Scheduler.Add(action, false);
    public static void ActivateCommandUpdateThread(Command command) => EnsureUpdateThread(() => CommandController.ActivateCommand(command));


    public static T Call<T>(object instance, string name)
    {
        var t = instance.GetType();
        var method = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return (T)method.Invoke(instance, []);
    }
    public static void Set(object instance, string name, object val)
    {
        var t = instance.GetType();
        var field = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
            field.SetValue(instance, val);
        else
        {
            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            prop.SetValue(instance, val);
        }
    }
    public static T Get<T>(object instance, string name)
    {
        var t = instance.GetType();
        var field = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
            return (T)field.GetValue(instance);
        var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return (T)prop.GetValue(instance);
    }
    static int? _trackMixerHandle;
    public static int TrackMixerHandle => _trackMixerHandle ??= Get<int>(DrumGame.Audio.TrackMixer, "Handle");
    public static (T, T) Order<T>(T a, T b) where T : IComparisonOperators<T, T, bool> => a > b ? (b, a) : (a, b);
    public static List<T> Shuffled<T>(this IEnumerable<T> source)
    {
        var list = source.ToList();
        var n = list.Count;
        for (var i = 0; i < n - 1; i++)
        {
            var k = RNG.Next(i, n);
            (list[i], list[k]) = (list[k], list[i]);
        }
        return list;
    }
}

