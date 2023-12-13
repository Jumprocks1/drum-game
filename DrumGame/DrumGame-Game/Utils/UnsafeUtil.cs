using osuTK;
using osuTK.Graphics.ES30;

namespace DrumGame.Game.Utils;

public static class UnsafeUtil
{
    public static void UploadMatrices(int location, Matrix4[] matrices)
    {
        unsafe
        {
            fixed (void* p = &matrices[0])
            {
                GL.UniformMatrix4(location, matrices.Length, false, (float*)p);
            }
        }
    }
}
