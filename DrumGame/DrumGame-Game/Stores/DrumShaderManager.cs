using System;
using System.Collections.Generic;
using DrumGame.Game.Utils;
using osu.Framework.Graphics.Shaders;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;

namespace DrumGame.Game.Stores;

public class DrumShaderManager : ShaderManager
{
    public DrumShaderManager(IResourceStore<byte[]> store) : base(Util.Host.Renderer, store)
    {
    }

    public ShaderWatcher LoadHotWatch(string fragmentShaderPath, Action<IShader> setShader)
    {
        var initial = LoadSafe(fragmentShaderPath);
        setShader(initial);
        return new ShaderWatcher(fragmentShaderPath, setShader);
    }

    // really laggy if shader fails since it will try to compile it over and over since it doesn't end up in the cache
    HashSet<string> FailedShaders = new();

    public IShader LoadSafeOrNull(string fragmentShaderPath)
    {
        try
        {
            if (FailedShaders.Contains(fragmentShaderPath)) return null;
            return Load(VertexShaderDescriptor.TEXTURE_2, fragmentShaderPath);
        }
        catch (Exception e)
        {
            FailedShader(e, fragmentShaderPath);
            return null;
        }
    }

    void FailedShader(Exception e, string fragmentShaderPath)
    {
        FailedShaders.Add(fragmentShaderPath);
        Util.Palette.ShowMessage("Shader compilation failed, see console");
        Logger.Error(e, "Shader compilation failed");
    }

    public IShader LoadSafe(string fragmentShaderPath)
    {
        try
        {
            if (FailedShaders.Contains(fragmentShaderPath)) return null;
            return Load(VertexShaderDescriptor.TEXTURE_2, fragmentShaderPath);
        }
        catch (Exception e)
        {
            FailedShader(e, fragmentShaderPath);
            return Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
        }
    }

    public class ShaderWatcher : IDisposable
    {
        public ShaderWatcher(string fragmentShaderPath, Action<IShader> setShader)
        {
            SetShader = setShader;
            FragmentShaderPath = fragmentShaderPath;
            var fullPath = Util.Resources.GetAbsolutePath(fragmentShaderPath);
            Watcher = new FileWatcher(fullPath);
            Watcher.Register();
            Watcher.Changed += () => Util.UpdateThread.Scheduler.Add(ReloadShader);
        }
        Action<IShader> SetShader;
        FileWatcher Watcher;
        ShaderManager ShaderManager;
        string FragmentShaderPath;
        void ReloadShader()
        {
            var shaderStore = new ResourceStore<byte[]>();
            shaderStore.AddStore(new NamespacedResourceStore<byte[]>(Util.DrumGame.Resources, @"Shaders"));
            shaderStore.AddStore(Util.Resources);
            var newShaderManager = new ShaderManager(Util.Host.Renderer, shaderStore);
            try
            {
                var shader = newShaderManager.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderPath);
                SetShader(shader);
                ShaderManager?.Dispose();
                ShaderManager = newShaderManager;
            }
            catch (Exception e)
            {
                newShaderManager?.Dispose();
                Util.Palette.ShowMessage("Shader compilation failed, see console");
                Logger.Error(e, "Shader compilation failed");
            }
        }

        public void Dispose()
        {
            SetShader = null;
            Watcher?.Dispose();
            ShaderManager?.Dispose();
        }
    }
}
