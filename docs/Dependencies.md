<!-- This is linked to via `BassUtil.cs` -->

# un4seen BASS - Linux
The cross-platform version of Drum Game doesn't come with some BASS plugins. They are only needed when:
- Using soundfonts (`.sf2` files)
- Exporting to `.dtx`
- Loading `aac`, `webm`, and `opus` audio on some platforms

To allow the game to use these plugins, download them and place them in `resources/lib`:
- Download from https://www.un4seen.com/ under Add-ons
- In-game, you can locate your `resources` folder using <kbd>F1</kbd> > `Open Resources Folder`
- You only need to place the lib*.so files in the `lib` folder

It shouldn't be neeeded on Windows, but the same process works using BASS's lib*.dll files instead.