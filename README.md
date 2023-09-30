# Drum Game
A rhythm game for electronic drums that is meant to be used as a practice and performance tool.

You can download the latest release [here](https://github.com/Jumprocks1/drum-game/releases).

It is built using [osu!framework](https://github.com/ppy/osu-framework) with a lot of it's design inspired by [osu!](https://github.com/ppy/osu). There are also many features/hotkeys based on [VS Code](https://github.com/microsoft/vscode) and [mpv](https://github.com/mpv-player/mpv).

You can see the game in action on my [YouTube channel](https://www.youtube.com/playlist?list=PLBsK4hG6ZcIgSahbTFiFBoQb39ITodnFM).

Learn more on [Discord](https://discord.gg/RTc3xDKabU) (this is still very new/WIP, don't hesitate to write a message!)

## Status
This project is still in early development. Most of the features are geared towards making the game work specifically for me as the developer. It is intended to work for all configurations, but due to lack of testing, many configurations may not function right away.

There is also a web version available [here](https://jumprocks1.github.io/drum-game) but it currently only supports display with audio playback via YouTube. The desktop version is significantly more polished.

## Running Drum Game
To start, download the latest [release](https://github.com/Jumprocks1/drum-game/releases). On Windows, you should download the `win-x64` version, unzip it, and run either `DrumGame.exe` or `DrumGame.bat`. For other desktop platforms (Linux/macOS), download the `net7.0` version, unzip, and then run it using the command `dotnet DrumGame.dll`. The cross-platform version requires .NET 7.0 to be installed. If you have any trouble, please [submit an issue](https://github.com/Jumprocks1/drum-game/issues/new) or post in the [Discord](https://discord.gg/RTc3xDKabU).

To download maps, I recommend using the in-game repository browser. This can be accessed by pressing <kbd>F1</kbd> and typing `View Repositories`, then pressing enter. YouTubeDL is required to load audio for some maps - the game should instruct you accordingly if this is necessary.

## Updating Drum Game (v0.3 -> v0.4)
The best way to update is to install the new version to a new folder. Once unzipped, you can copy any important data from your previous installation. All the relevant data will be in the `resources` folder inside your previous install. I recommend copying (or moving) the following folders and files:
- `resources/maps` - stores all the beatmaps that you have downloaded
- `resource/database.db` - summary information of previous plays (misses, accuracy, time) as well as beatmap ratings
- `resources/collections` - only needed if you have custom collections
- `resources/repositories/download.txt` - contains the list of maps you have already downloaded. Only needed if you use the checkmarks in the repository browser
- `resources/temp/youtube` - contains audio downloaded from YouTube. Only needed if you have been using YouTubeDL

In the future I would like to make this process automatic, but until I am releasing updates more frequently, I think there are better things to prioritize.

Basic settings and keybinds are stored in `%appdata%/DrumGame` - these do not need to be moved and can be shared between releases.

## Android/iOS
If you are interested in getting Drum Game on one of these platforms, please [create an issue](https://github.com/Jumprocks1/drum-game/issues) and I will set up a build for that platform. The source code is already cross-platform, so it should just be a matter of building and testing.

## Tutorial
- Most things can be learned and accessed through the use of the command palette. To start, just press <kbd>F1</kbd> or <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>P</kbd> and begin searching.
- An easy way to learn some of the hotkeys is with the keyboard view, this can be accessed with <kbd>Shift</kbd>+<kbd>F1</kbd> by default.

## Contributing
Currently the source code for the desktop version of Drum Game is in a private repository. To contribute to the desktop version, please submit an [issue](https://github.com/Jumprocks1/drum-game/issues). The web version is available publicly in this repository. It can be built using `webpack` and ran locally using `webpack serve`.

In the future I plan to make the full game open source (probably under GPLv3) so that others can more easily provide suggestions and contributions.