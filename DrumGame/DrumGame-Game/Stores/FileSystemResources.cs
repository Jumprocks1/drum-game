using System;
using System.IO;
using DrumGame.Game.Utils;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;

namespace DrumGame.Game.Stores;

public class FileSystemResources : StorageBackedResourceStore
{
    public readonly NativeStorage Storage;
    public readonly NativeStorage GlobalStorage;
    public readonly IResourceStore<byte[]> GlobalStorageStore;
    public readonly ITrackStore Tracks;
    public FileSystemResources(string path, NativeStorage storage, ITrackStore trackStore = null) : base(storage)
    {
        AbsolutePath = Path.GetFullPath(path);
        Storage = storage;
        GlobalStorage = new GlobalNativeStorage(AbsolutePath, Util.Host);
        GlobalStorageStore = new StorageBackedResourceStore(GlobalStorage);
        Tracks = trackStore ?? Util.DrumGame.Audio.GetTrackStore(GlobalStorageStore);
        ResourceStore = new ResourceStore<byte[]>(this);
        if (Util.Host?.Renderer != null)
        {
            var loader = Util.Host.CreateTextureLoaderStore(GlobalStorageStore);
            // mipmaps needed for card thumbnails
            // couldn't figure out how to do manual mipmaps, so this will have to work
            // osu-framework has no docs on manual mipmaps
            LargeTextures = new LargeTextureStore(Util.Host.Renderer, loader, manualMipmaps: false);

            var assetStore = new StorageBackedResourceStore(Storage.GetStorageForDirectory("assets"));
            loader = Util.Host.CreateTextureLoaderStore(assetStore);
            NearestAssetTextureStore = new TextureStore(Util.Host.Renderer, loader,
                // nearest helps prevent fringing on edges, there may be a better way though
                true, TextureFilteringMode.Nearest, true, 1);
            LinearAssetTextureStore = new TextureStore(Util.Host.Renderer, loader,
                true, TextureFilteringMode.Linear, true, 1);
            AssetTextureStoreNoAtlas = new TextureStore(Util.Host.Renderer, loader,
                false, TextureFilteringMode.Linear, true, 1);
        }
    }
    public TextureStore NearestAssetTextureStore;
    public TextureStore LinearAssetTextureStore;
    public TextureStore AssetTextureStoreNoAtlas;
    public Track GetTrack(string path) // simple wrapper so we can load webm
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (!BassUtil.WebmChecked || !BassUtil.OpusChecked || !BassUtil.AacChecked)
        {
            // this is far from perfect. We don't actually need to load these if it's a .ogg vorbis file
            // we have another issue in that I hard-coded the YouTube track lookup to use .ogg even if it's a .webm file
            if (path.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".opus", StringComparison.OrdinalIgnoreCase))
            {
                // costs ~4ms if cached, ~100ms if first time
                // run on audio thread to make sure we don't run it twice
                Util.AudioThread.Scheduler.Add(() =>
                {
                    BassUtil.LoadWebm();
                    BassUtil.LoadOpus();
                    BassUtil.LoadAac();
                });
            }
        }
        return Tracks.Get(path);
    }
    public ITextureStore LargeTextures;
    public ResourceStore<byte[]> ResourceStore;
    public readonly string AbsolutePath;
    public DirectoryInfo GetDirectory(string path) => Directory.CreateDirectory(GetAbsolutePath(path));
    public DirectoryInfo GetDirectory(params string[] path) => Directory.CreateDirectory(GetAbsolutePath(Path.Join(path)));
    public DirectoryInfo Temp => Directory.CreateDirectory(GetAbsolutePath("temp"));
    public string TryFind(string path)
    {
        if (path == null) return null;
        var abs = GetAbsolutePath(path);
        if (File.Exists(abs)) return abs;
        return null;
    }
    public string GetTemp(string filename = null) => Path.Join(Temp.FullName, filename ?? Guid.NewGuid().ToString());
    // Could add security check here
    public string GetAbsolutePath(string path) => Path.GetFullPath(path, AbsolutePath);
    public string GetRelativePath(string path) => Path.GetRelativePath(AbsolutePath, path);
    public bool Contains(string path) => Path.GetFullPath(path).StartsWith(AbsolutePath);
    public bool Exists(string path) => Storage.Exists(path);

    public string YouTubeAudioPath(string id) => id == null ? null : Path.Join(GetAbsolutePath("temp/youtube"), id + ".ogg");


    static string[] _path;
    public static string[] EnvironmentPath => _path ??= Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator);
    public static string GetEnvironmentLocation(string fileName)
    {
        if (File.Exists(fileName))
            return Path.GetFullPath(fileName);

        foreach (var path in EnvironmentPath)
        {
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }
        return null;
    }
    public static bool ExistsInPath(string fileName) => GetEnvironmentLocation(fileName) != null;
    public string LocateExecutable(params string[] locations)
    {
        const string extension = ".exe";
        foreach (var location in locations)
        {
            // prefer our lib folder
            var libPath = GetAbsolutePath(Path.Join("lib", location));
            if (File.Exists(libPath))
                return libPath;
            libPath += extension;
            if (File.Exists(libPath))
                return libPath;
        }
        foreach (var location in locations)
        {
            if (ExistsInPath(location))
                return location;
            var e = location + extension;
            if (ExistsInPath(e))
                return location;
        }
        return null;
    }
    public Texture GetAssetTexture(string filename, TextureFilteringMode filteringMode,
        WrapMode wrapModeS = default, WrapMode wrapModeT = default)
        => (filteringMode == TextureFilteringMode.Linear ? LinearAssetTextureStore : NearestAssetTextureStore)
            .Get(filename, wrapModeS, wrapModeT);
    public Texture GetAssetTextureNoAtlas(string filename,
        WrapMode wrapModeS = default, WrapMode wrapModeT = default)
        => AssetTextureStoreNoAtlas.Get(filename, wrapModeS, wrapModeT);
}
