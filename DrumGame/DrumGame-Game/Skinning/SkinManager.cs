using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Channels;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Notation;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Logging;
using osuTK;

namespace DrumGame.Game.Skinning;

public static class SkinManager
{
    static List<AdjustableSkinElement> Elements = new();
    public readonly static List<(string, Drawable)> AnchorTargets = new(); // this is intentionally not a dictionary
    public static void RegisterElement(AdjustableSkinElement element)
    {
        Elements.Add(element);
    }
    public static void UnregisterElement(AdjustableSkinElement element)
    {
        Elements.Remove(element);
    }
    // TODO should probably make a custom interface here instead of drawable
    // Interface would need UpdateTargets or something similar
    // We would then call that whenever we want to update our targets, potionally in OnInvalidate
    public static void RegisterTarget(string target, Drawable drawable) => AnchorTargets.Add((target, drawable));
    public static void UnregisterTarget(Drawable drawable) => AnchorTargets.RemoveAll(e => e.Item2 == drawable);
    public static void UnregisterTarget(string target) => AnchorTargets.RemoveAll(e => e.Item1.Equals(target, StringComparison.OrdinalIgnoreCase));
    public static Drawable GetTarget(string target)
    {
        if (target == null) return null;
        return AnchorTargets.FirstOrDefault(e => e.Item1.Equals(target, StringComparison.OrdinalIgnoreCase)).Item2;
    }
    static bool AltState;
    public static void SetAltKey(bool altPressed)
    {
        if (altPressed == AltState) return;
        AltState = altPressed;
        foreach (var element in Elements)
        {
            if (altPressed) element.ShowOverlay();
            else element.HideOverlay();
        }
    }

    public static event Action SkinChanged; // triggered when skin is modified/reloaded and when different skin is selected

    // public static void ReloadSkin() => SkinChanged?.Invoke();
    static Bindable<string> Binding;
    public static string CurrentSkin => Binding.Value;

    public static void ChangeSkinTo(string skinPath)
    {
        if (skinPath == null)
            Binding.Value = null; // load default skin
        else if (FindSkin(skinPath) != null)
            Binding.Value = skinPath;
    }

    static void ChangeSkin(Skin skin) // called for first skin too
    {
        Util.Skin?.UnloadStores();
        skin.LoadStores();
        Util.Skin = skin;
    }

    public static void Initialize()
    {
        Binding = Util.ConfigManager.GetBindable<string>(DrumGameSetting.Skin);
        Binding.ValueChanged += e =>
        {
            var v = e.NewValue;
            if (Util.Skin?.Source == v) return;
            if (v == null)
            {
                ChangeSkin(LoadDefaultSkin());
                SkinChanged?.Invoke();
            }
            else
            {
                if (FileWatcher != null) SetHotWatcher(v);
                // note that if the skin fails to load, we don't change anything
                var newSkin = ParseSkin(v);
                if (newSkin != null)
                {
                    ChangeSkin(newSkin);
                    SkinChanged?.Invoke();
                }
            }
        };
        ChangeSkin(ParseSkin(Binding.Value) ?? LoadDefaultSkin());
        Util.CommandController.RegisterHandler(Command.ReloadSkin, ReloadSkin); // don't need to unregister
        Util.CommandController.RegisterHandler(Command.ExportCurrentSkin, ExportCurrentSkin); // don't need to unregister
    }

    public static IEnumerable<string> ListSkins()
    {
        var dir = Util.Resources.GetDirectory("skins");
        var subDirs = dir.GetDirectories();
        return dir.GetFiles("*.json")
            .Where(e => e.Name != "skin-schema.json")
            .Select(e => Path.GetFileNameWithoutExtension(e.Name))
            .Concat(subDirs.Select(e => e.Name));
    }
    // this is expensive-ish since it has to try parsing all skins
    public static List<(string skin, string name)> ListSkinsWithNames()
    {
        var skins = ListSkins();
        var res = new List<(string, string)>();
        foreach (var skin in skins)
        {
            var name = GetName(skin) ?? "Unknown";
            res.Add((skin, name));
        }
        return res;
    }
    class SkinNameModel { public string Name { get; set; } } // hopefully this is faster than a full skin parse
    public static string GetName(string skin)
    {
        var filename = FindSkin(skin);
        if (filename == null) return null;
        try
        {
            var serializer = new JsonSerializer();
            using var file = File.OpenRead(filename);
            using var sr = new StreamReader(file);
            using var jsonTextReader = new JsonTextReader(sr);
            return serializer.Deserialize<SkinNameModel>(jsonTextReader).Name;
        }
        catch { } // don't bother logging errors here since this is a quick/minor method

        return null;
    }

    // only use on update thread
    static long LastReload;
    public static void ReloadSkin() => ReloadSkin(false);
    public static void ReloadSkin(bool throttle) // this will also start the hot watcher
    {
        Util.EnsureUpdateThread(() =>
        {
            var now = Stopwatch.GetTimestamp();
            // only allow 1 reload per second. This helps with the watcher
            if (throttle && now - LastReload < Stopwatch.Frequency) return;
            LastReload = now;
            var v = Binding.Value;
            if (v != null)
            {
                var newSkin = ParseSkin(v);
                if (newSkin != null)
                {
                    ChangeSkin(newSkin);
                    SkinChanged?.Invoke();
                    SetHotWatcher(v);
                    Util.Palette.ShowMessage("Skin reloaded");
                }
            }
        });
    }

    // costs ~16ms to parse skin first time
    // <1ms on second parse - this is likely because Newtonsoft has to generate a bunch of reflection code the first time
    public static Skin ParseSkin(string skinName)
    {
        // using var _ = Util.WriteTime();
        var path = FindSkin(skinName);
        if (path == null) return null;
        return SkinFromFile(path);
    }
    public static string FindSkin(string skinName)
    {
        if (string.IsNullOrWhiteSpace(skinName)) return null;
        var path = Util.Resources.GetAbsolutePath(Path.Join("skins", skinName));
        if (File.Exists(path)) return path;
        var json = path + ".json";
        if (File.Exists(json)) return json;
        if (Directory.Exists(path))
        {
            var subPath = Path.Join(path, "skin.json");
            if (File.Exists(subPath))
                return subPath;
            subPath = Path.Join(path, skinName + ".json");
            if (File.Exists(subPath))
                return subPath;
        }
        return null;
    }

    public static Skin LoadDefaultSkin()
    {
        var skin = new Skin();
        skin.LoadDefaults();
        return skin;
    }

    public static Skin SkinFromFile(string filename)
    {
        try
        {
            var text = File.ReadAllText(filename);
            var skin = JsonConvert.DeserializeObject<Skin>(text, new ColorHexConverter(), new DrumChannelConverter());
            skin.Source = filename;
            skin.SourceFolder = Path.GetDirectoryName(filename);
            skin.LoadDefaults();
            return skin;
        }
        catch (FileNotFoundException)
        {
            Logger.Log($"Failed to load skin, {filename} not found");
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Failed to load skin {filename}");
        }
        Util.Palette?.ShowMessage("Failed to load skin. See log for details");
        return null;
    }

    static FileWatcher FileWatcher;
    static void SetHotWatcher(string skin)
    {
        var path = File.Exists(skin) ? skin : FindSkin(skin);
        if (path == null) return;
        if (FileWatcher == null)
        {
            FileWatcher = new(path)
            {
                ExtraFilters = ["*.fs"]
            };
            FileWatcher.Changed += () => ReloadSkin(true);
            FileWatcher.Register();
        }
        else
            FileWatcher.UpdatePath(path);
    }
    public static void SetHotWatcher(Skin skin) => SetHotWatcher(skin?.Source);
    public static void MarkDirty(Skin skin, string path) => skin.AddDirtyPath(path);
    public static void Cleanup()
    {
        if (Util.Skin != null && Util.Skin.Dirty)
            SavePartialSkin(Util.Skin);
    }
    public static void SavePartialSkin(Skin skin, string outPath = null)
    {
        if (skin == null || !skin.Dirty) return;
        if (skin.Source == null && outPath == null)
        {
            Util.Palette.ShowMessage("Default skin cannot be saved. Please create a new skin before saving changes.");
            skin.DirtyPaths.Clear();
            return;
        }
        try
        {
            var serializer = JsonSerializer.Create(SerializerSettings);

            JToken freshSkin;
            using (var file = File.OpenText(skin.Source))
            using (var reader = new JsonTextReader(file))
            {
                freshSkin = JToken.ReadFrom(reader);
            }

            var loadedSkinJObject = JObject.FromObject(skin, serializer);

            Logger.Log($"Saving skin updates to: {string.Join(',', skin.DirtyPaths)}", level: LogLevel.Important);
            foreach (var path in skin.DirtyPaths)
                freshSkin.ReplaceOrAdd(path, loadedSkinJObject.SelectToken(path));

            outPath ??= skin.Source;
            using var outFile = File.CreateText(outPath);
            using var writer = new JsonTextWriter(outFile);
            serializer.Serialize(writer, freshSkin);
            skin.DirtyPaths.Clear();
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Failed to save skin {skin.Name}");
        }
    }

    public static JsonSerializerSettings SerializerSettings => new()
    {
        ContractResolver = new SkinContractResolver(),
        Converters = [new ColorHexConverter(), new StringEnumConverter(new CamelCaseNamingStrategy())],
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
    };

    public static void ExportCurrentSkin()
    {
        try
        {
            var skin = Util.Skin;
            var name = skin.Name ?? "Default Export";
            var exportName = Util.ToFilename($"export {name}", ".json");
            var outputPath = Util.Resources.GetAbsolutePath(Path.Join("skins", exportName));
            var settings = SerializerSettings;
            skin.GameVersion ??= Util.VersionString;
            skin.Comments ??= "This is an exported version of a skin. It may contain extra fields that are not needed.";
            var s = JsonConvert.SerializeObject(skin, settings);
            File.WriteAllText(outputPath, s);
            Util.RevealInFileExplorer(outputPath);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Export failed");
            Util.Palette.UserError("Export failed, see log for exception");
        }
    }
    public const string DefaultSkinFilename = "default";
    public static bool EnsureDefaultSkinExists()
    {
        try
        {
            var outputPath = Util.Resources.GetAbsolutePath(Path.Join("skins", DefaultSkinFilename + ".json"));
            if (File.Exists(outputPath)) return true;
            var skin = new Dictionary<string, string>()
            {
                ["name"] = "Default",
                ["$schema"] = "skin-schema.json"
            };
            var s = JsonConvert.SerializeObject(skin, SerializerSettings);
            File.WriteAllText(outputPath, s);
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Export failed");
            Util.Palette.UserError("Export failed, see log for exception");
            return false;
        }
    }

    public class SkinChannelConverter : JsonConverter<Dictionary<DrumChannel, SkinNote>>
    {
        public override Dictionary<DrumChannel, SkinNote> ReadJson(JsonReader reader, Type objectType,
            Dictionary<DrumChannel, SkinNote> res, bool hasExistingValue, JsonSerializer serializer)
        {
            res ??= new Dictionary<DrumChannel, SkinNote>();

            reader.Read();
            while (reader.TokenType != JsonToken.EndObject)
            {
                var key = (string)reader.Value;
                var channel = BJsonNote.GetDrumChannel(key);
                reader.Read();
                if (res.TryGetValue(channel, out var note))
                    serializer.Populate(reader, note);
                else
                    res.Add(channel, serializer.Deserialize<SkinNote>(reader));
                reader.Read(); // not sure why it doesn't read the close object
            }

            return res;
        }

        public override void WriteJson(JsonWriter writer, Dictionary<DrumChannel, SkinNote> value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            foreach (var pair in value)
            {
                writer.WritePropertyName(BJsonNote.GetChannelString(pair.Key));
                serializer.Serialize(writer, pair.Value);
            }
            writer.WriteEndObject();
        }
    }

    public class SkinContractResolver : DefaultContractResolver
    {
        public SkinContractResolver()
        {
            NamingStrategy = new CamelCaseNamingStrategy();
        }
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var list = base.CreateProperties(type, memberSerialization);
            if (type == typeof(Skin))
            {
                list = list.Where(e => e.Writable).ToList();
                list.Add(new JsonProperty
                {
                    PropertyName = "$schema",
                    PropertyType = typeof(string),
                    DeclaringType = type,
                    ValueProvider = new SchemaValueProvider(),
                    Readable = true,
                    Writable = false,
                    ShouldSerialize = _ => true
                });
            }
            else if (type == typeof(RectangleF))
            {
                list = list.Where(e => e.Writable && e.PropertyType == typeof(float)).ToList();
            }
            else if (type == typeof(Vector2))
            {
                list = list.Where(e => e.Writable && e.PropertyType == typeof(float)).ToList();
            }
            return list;
        }

        private class SchemaValueProvider : IValueProvider
        {
            public object GetValue(object target) => "skin-schema.json";
            public void SetValue(object target, object value) { }
        }
    }

    public class ColorHexConverter : JsonConverter<Colour4>
    {
        public override Colour4 ReadJson(JsonReader reader, Type objectType, Colour4 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var v = (string)reader.Value;
            if (v.StartsWith("rgb"))
            {
                var trim = v.AsSpan(3);
                if (trim.StartsWith("a")) trim = trim[1..];
                if (trim.StartsWith("(")) trim = trim[1..];
                if (trim.EndsWith(")")) trim = trim[..^1];
                var ind = trim.IndexOf(",");
                var r = byte.Parse(trim[..ind]);
                trim = trim[(ind + 1)..];
                ind = trim.IndexOf(",");
                var g = byte.Parse(trim[..ind]);
                trim = trim[(ind + 1)..];
                ind = trim.IndexOf(",");
                if (ind == -1)
                {
                    return new Colour4(r, g, byte.Parse(trim), 255);
                }
                else
                {
                    var b = byte.Parse(trim[..ind]);
                    trim = trim[(ind + 1)..];
                    return new Colour4(r, g, b, byte.Parse(trim));
                }
            }
            else return Colour4.FromHex(v);
        }

        public override void WriteJson(JsonWriter writer, Colour4 value, JsonSerializer serializer)
            => writer.WriteValue(value.ToHex());
    }
}