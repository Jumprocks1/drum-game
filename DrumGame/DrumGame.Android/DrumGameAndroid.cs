using Android.App;
using DrumGame.Game;

namespace DrumGame.Android;

public class DrumGameAndroid : DrumGameGame
{
    public DrumGameAndroid(DrumGameActivity activity) : base(Application.Context.GetExternalFilesDir(null).AbsolutePath + "/resources")
    {
    }
}