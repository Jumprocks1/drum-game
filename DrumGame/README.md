# Drum Game Desktop
This folder contains the source for the primary desktop application for Drum Game. Code in this folder is licensed under GPLv3.


## Running From Source
On Windows, you can run `Start.ps1`. If it complains about missing `osu.Framework`, then you will need to checkout the correct version of the framework.
As of 12-12-2023, version 2022.1110.0 from NuGet will also work. To switch to the NuGet version, simple toggle the comments in `DrumGame.Game/DrumGame.Game.csproj` so that the NuGet version is referenced.

## Contributing
Currently there's not much set up for other contributors. I personally use the VSCode C# extension with C# Dev Kit disabled. The "Dev Kit" includes AI and closed source components that I don't use. I have a better experience with `"dotnet.server.useOmnisharp": true` set, but the default language server should also be fine.

Feel free to open up an issue/submit a pull request if you find any changes you would like.

## Debugging
I find it easiest to just start the game normally with `./Start.ps1` or `./Start.ps1 -NoCompile` then attach to the process with VSCode. You can attach with the VSCode using the `Desktop Attach` debug configuration. To pause the game until the debugger attaches, add `--wait-for-debugger`. You can create additional debug configurations in `.vscode/launch.json` if desired.

## Linux
Everything should work the same on Linux. If you don't have `pwsh` installed on Linux, it should be pretty simple to translate the commands in the `ps1` files.