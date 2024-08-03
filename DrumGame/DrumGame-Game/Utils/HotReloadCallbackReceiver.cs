using System;
using JetBrains.Annotations;

namespace DrumGame.Game.Utils;

public static class HotReloadCallbackReceiver
{
    public static event Action<Type[]> CompilationFinished;
    public static void UpdateApplication([CanBeNull] Type[] updatedTypes) => CompilationFinished?.Invoke(updatedTypes);
}