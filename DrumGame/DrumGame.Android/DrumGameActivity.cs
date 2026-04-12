using System;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using DrumGame.Game;
using DrumGame.Game.Utils;
using osu.Framework.Android;

namespace DrumGame.Android
{
    [Activity(ConfigurationChanges = DEFAULT_CONFIG_CHANGES, Exported = true, LaunchMode = DEFAULT_LAUNCH_MODE, MainLauncher = true)]
    public class DrumGameActivity : AndroidGameActivity
    {
        protected override void Main()
        {
            var host = new AndroidGameHost(this);
            Util.Host = host;
            host.Run(CreateGame());
        }

        protected override void OnStart()
        {
            base.OnStart();
            // screen was still shutting off without this (despite the below flags)
            // after setting this, it seems good
            Window.DecorView.KeepScreenOn = true;
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // these don't seem to work :(
            Window.AddFlags(WindowManagerFlags.Fullscreen);
            Window.AddFlags(WindowManagerFlags.KeepScreenOn);
            RequestedOrientation = ScreenOrientation.Landscape;
            SDL.SDL3.SDL_SetHint(SDL.SDL3.SDL_HINT_ORIENTATIONS, "LandscapeLeft LandscapeRight"u8);
            SDL.SDL3.SDL_SetHint(SDL.SDL3.SDL_HINT_VIDEO_ALLOW_SCREENSAVER, "0");
        }
        protected override osu.Framework.Game CreateGame()
            => new DrumGameAndroid(this);
    }
}
