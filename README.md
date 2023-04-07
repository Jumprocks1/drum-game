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
To start, download the latest [release](https://github.com/Jumprocks1/drum-game/releases). Follow the instructions on [release v0.1.1](https://github.com/Jumprocks1/drum-game/releases/tag/v0.1.1) if you need help getting the game started. If you have any trouble, please [submit an issue](https://github.com/Jumprocks1/drum-game/issues/new).

On first launch, the game will prompt you to download the `resources` folder. This folder contains over 100 playable beatmaps, but the audio is not included for most due to copyright concerns. When updating the game, you can safely transfer this resource folder to the new installation (although it will not include any new maps).

## Android/iOS
If you are interested in getting Drum Game on one of these platforms, please [create an issue](https://github.com/Jumprocks1/drum-game/issues) and I will set up a build for that platform. The source code is already cross-platform, so it should just be a matter of building and testing.

## Tutorial
- Most things can be learned and accessed through the use of the command palette. To start, just press <kbd>F1</kbd> or <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>P</kbd> and begin searching.
- An easy way to learn some of the hotkeys is with the keyboard view, this can be accessed with <kbd>Shift</kbd>+<kbd>F1</kbd> by default.
- There are also some useful keybinds for `osu-framework` [here](https://github.com/ppy/osu-framework/wiki/Framework-Key-Bindings).

## Contributing
Currently the source code for the desktop version of Drum Game is in a private repository. To contribute to the desktop version, please submit an [issue](https://github.com/Jumprocks1/drum-game/issues). The web version is available publicly in this repository. It can be built using `webpack` and ran locally using `webpack serve`.

In the future I plan to make the full game open source (probably under GPLv3) so that others can more easily provide suggestions and contributions.