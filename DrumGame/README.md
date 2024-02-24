# Drum Game Desktop
This folder contains the source for the primary desktop application for Drum Game. Code in this folder is licensed under GPLv3.


## Running From Source
On Windows, you can run `Start.ps1`. As of 2-22-2024, the game is using [2024.217.0](https://www.nuget.org/packages/ppy.osu.Framework/2024.217.0). The version directly from NuGet will not work since they switched to a fork of `ManagedBass`. To get it working, clone the correct version from [GitHub](https://github.com/ppy/osu-framework) and swap out the forked references for ManagedBass in `osu.Framework.csproj` so they look like this: `<PackageReference Include="ManagedBass" Version="3.1.0" />`. The reason this is required is because DrumGame uses the ManagedBass.Midi package, but the framework developers did not fork this package, so the references get all broken if the froked `ManagedBass` is used. I would like to try fixing this in the future.

## Contributing
Currently there's not much set up for other contributors. I personally use the VSCode C# extension with C# Dev Kit disabled. The "Dev Kit" includes AI and closed source components that I don't use. I have a better experience with `"dotnet.server.useOmnisharp": true` set, but the default language server should also be fine.

Feel free to open up an issue/submit a pull request if you find any changes you would like.

## Debugging
I find it easiest to just start the game normally with `./Start.ps1` or `./Start.ps1 -NoCompile` then attach to the process with VSCode. You can attach with the VSCode using the `Desktop Attach` debug configuration. To pause the game until the debugger attaches, add `--wait-for-debugger`. You can create additional debug configurations in `.vscode/launch.json` if desired.

## Linux
Everything should work the same on Linux. If you don't have `pwsh` installed on Linux, it should be pretty simple to translate the commands in the `ps1` files.