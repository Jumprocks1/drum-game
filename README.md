# Drum Game
An open-source rhythm game for electronic drums that is meant to be used as a practice and performance tool.

You can download the latest release [here](https://github.com/Jumprocks1/drum-game/releases).

It is built using [osu!framework](https://github.com/ppy/osu-framework) with a lot of it's design inspired by [osu!](https://github.com/ppy/osu). There are also many features/hotkeys based on [VS Code](https://github.com/microsoft/vscode) and [mpv](https://github.com/mpv-player/mpv).

You can see the game in action on my [YouTube channel](https://www.youtube.com/playlist?list=PLBsK4hG6ZcIgSahbTFiFBoQb39ITodnFM).

Learn more on [Discord](https://discord.gg/RTc3xDKabU)

[![Discord](https://img.shields.io/discord/1019266871888973976.svg?logo=discord&logoColor=white&labelColor=7289DA&label=Discord&color=17cf48)](https://discord.gg/RTc3xDKabU)

## Status
This project is still in early development. Most of the features are geared towards making the game work specifically for me as the developer. It is intended to work for all configurations, but due to lack of testing, many configurations may not function right away.

There is also a web version available [here](https://jumprocks1.github.io/drum-game) but it currently only supports display with audio playback via YouTube. The desktop version is significantly more polished.

## Running Drum Game
To start, download the latest [release](https://github.com/Jumprocks1/drum-game/releases). On Windows, you should download the `win-x64` version, unzip it, and run either `DrumGame.exe` or `DrumGame.bat`. For other desktop platforms (Linux/macOS), download the `net8.0` version, unzip, and then run it using the command `dotnet DrumGame.dll`. The cross-platform version requires .NET 8.0 to be installed. If you have any trouble, please [submit an issue](https://github.com/Jumprocks1/drum-game/issues) or post in the [Discord](https://discord.gg/RTc3xDKabU).

To download maps, I recommend using the in-game repository browser. This can be accessed by pressing <kbd>F1</kbd> and typing `View Repositories`, then pressing enter. YouTubeDL is required to load audio for some maps - the game should instruct you accordingly if this is necessary.

You can also load an existing map library of `.dtx` files by running the command `Configure Map Libraries`. This should work for thousands of files.

## Running/Compiling from Source
See [running from source](DrumGame/README.md#running-from-source)

## Android/iOS
If you are interested in getting Drum Game on one of these platforms, please [create an issue](https://github.com/Jumprocks1/drum-game/issues) and I will set up a build for that platform. The source code is already cross-platform, so it should just be a matter of building and testing.

## Tutorial
- Most things can be learned and accessed through the use of the command palette. To start, just press <kbd>F1</kbd> or <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>P</kbd> and begin searching.
- An easy way to learn some of the hotkeys is with the keyboard view, this can be accessed with <kbd>Shift</kbd>+<kbd>F1</kbd> by default.

## Contributing
The source for the desktop game is located in the `DrumGame` folder. See [README.md](DrumGame/README.md) for more details. Feel free to open issues/pull requests as needed.