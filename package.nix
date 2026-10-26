{
  lib,
  buildDotnetModule,
  fetchFromGitHub,
  dotnetCorePackages,
  stdenvNoCC,
  ffmpeg,
  alsa-lib,
  SDL2,
  sdl3,
  lttng-ust,
  numactl,
  libglvnd,
  libxi,
  udev,
  vulkan-loader,
  nativeWayland ? false,
}:

buildDotnetModule rec {
  pname = "drum-game";
  version = "0.9.4";

  src = fetchFromGitHub {
    owner = "Jumprocks1";
    repo = "drum-game";
    tag = "v${version}";
    sha256 = "sha256-c5FvD2m3mkP8/IYpG82Ib5mlfsoFBTwAeNTbg8WUsjw=";
  };

  projectFile = "DrumGame/DrumGame.Desktop/DrumGame.Desktop.csproj";
  nugetDeps = ./deps.json;

  dotnet-sdk = dotnetCorePackages.sdk_8_0;
  dotnet-runtime = dotnetCorePackages.runtime_8_0;

  runtimeDeps = [
    ffmpeg
    alsa-lib
    SDL2
    sdl3
    lttng-ust
    numactl
    libglvnd
    libxi
    udev
    vulkan-loader
  ];

  postInstall = ''
    mkdir -p $out/share/DrumGame
    cp -r resources $out/share/DrumGame/
  '';

  fixupPhase = ''
    runHook preFixup

    wrapProgram $out/bin/DrumGame \
      ${lib.optionalString nativeWayland "--set SDL_VIDEODRIVER wayland"}

    ln -sft $out/lib/${pname} ${SDL2}/lib/libSDL2${stdenvNoCC.hostPlatform.extensions.sharedLibrary}
    ln -sft $out/lib/${pname} ${sdl3}/lib/libSDL3${stdenvNoCC.hostPlatform.extensions.sharedLibrary}

    runHook postFixup
  '';

  executables = [ "DrumGame" ];

  meta = {
    description = "An open-source rhythm game for electronic drums that is meant to be used as a practice and performance tool.";
    homepage = "https://github.com/Jumprocks1/drum-game";
    mainProgram = "DrumGame";
  };
}
