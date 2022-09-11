# Drum Game
A rhythm game for electronic drums that is meant to be used as a practice and performance tool.

It is built using [osu!framework](https://github.com/ppy/osu-framework) with a lot of it's design inspired by [osu!](https://github.com/ppy/osu). There are also many features/hotkeys based on [VS Code](https://github.com/microsoft/vscode) and [mpv](https://github.com/mpv-player/mpv).

You can see the game in action on my [YouTube channel](https://www.youtube.com/playlist?list=PLBsK4hG6ZcIgSahbTFiFBoQb39ITodnFM).

## Status
This project is still in early development. Most of the features are geared towards making the game work specifically for me as the developer. It is intended to work for all configurations, but due to lack of testing, many configurations may not function right away.

## Running Drum Game
To start, download the latest [release](https://github.com/Jumprocks1/drum-game/releases). Follow the instructions on [release v0.1.1](https://github.com/Jumprocks1/drum-game/releases/tag/v0.1.1) if you need help getting the game started. If you have any trouble, please [submit an issue](https://github.com/Jumprocks1/drum-game/issues/new).

You will also need a copy of the `resources` folder, which for now you can obtain from [release v0.2.0](https://github.com/Jumprocks1/drum-game/releases/tag/v0.2.0).

The `resources` folder contains all beatmaps and audio files used by Drum Game. For the most part, releases will be separate from the `resources` folder, meaning you can update the game without updating `resources` (and vice-versa).

Currently I do not distribute most the mp3's required for playing the audio for beatmaps (meaning they have to be acquired elsewhere).  In the future I plan to enable a YouTube hook that lets you play without having any local audio files. If anyone knows legal ways of doing this type of thing, please let me know.

## Android/iOS
If you are interested in getting Drum Game on one of these platforms, please [create an issue](https://github.com/Jumprocks1/drum-game/issues/new) and I will set up a build for that platform. The source code is already cross-platform, so it should just be a matter of building and testing.

## Tutorial
- Most things can be learned and accessed through the use of the command palette. To start, just press <kbd>F1</kbd> or <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>P</kbd> and begin searching.
- An easy way to learn some of the hotkeys is with the keyboard view, this can be accessed with <kbd>Shift</kbd>+<kbd>F1</kbd> by default.
- There are some useful keybinds for `osu-framework` [here](https://github.com/ppy/osu-framework/wiki/Framework-Key-Bindings).
    - Most of these are not listed under the command palette since the bindings are not controlled by Drum Game directly.

## Contributing
Currently the source code for Drum Game is not released. In the future I plan to make the game open source (probably under GPLv3) so that others can more easily provide suggestions and contributions.