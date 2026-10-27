{
  lib,
  buildDotnetModule,
  fetchFromGitHub,
  dotnetCorePackages,
  stdenvNoCC,
  coreutils,
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

buildDotnetModule (finalAttrs: {
  pname = "drum-game";
  version = "0.9.4";

  src = fetchFromGitHub {
    owner = "Jumprocks1";
    repo = "drum-game";
    tag = "v${finalAttrs.version}";
    sha256 = "sha256-c5FvD2m3mkP8/IYpG82Ib5mlfsoFBTwAeNTbg8WUsjw=";
  };

  projectFile = "DrumGame/DrumGame.Desktop/DrumGame.Desktop.csproj";
  nugetDeps = ./deps.json;

  dotnet-sdk = dotnetCorePackages.sdk_8_0;
  dotnet-runtime = dotnetCorePackages.runtime_8_0;

  nativeBuildInputs = [ coreutils ];

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
      ${lib.optionalString nativeWayland "--set SDL_VIDEODRIVER wayland"} \
      --run '
        d="''${XDG_DATA_HOME:-$HOME/.local/share}/DrumGame"
        if [ ! -e "$d/.seeded-${finalAttrs.version}" ]; then
          mkdir -p "$d"
          cp -r --no-preserve=mode,ownership ${placeholder "out"}/share/DrumGame/resources "$d/"
          touch "$d/.seeded-${finalAttrs.version}"
        fi
      '

    ln -sft $out/lib/${finalAttrs.pname} ${SDL2}/lib/libSDL2${stdenvNoCC.hostPlatform.extensions.sharedLibrary}
    ln -sft $out/lib/${finalAttrs.pname} ${sdl3}/lib/libSDL3${stdenvNoCC.hostPlatform.extensions.sharedLibrary}

    runHook postFixup
  '';

  executables = [ "DrumGame" ];

  meta = {
    description = "An open-source rhythm game for electronic drums that is meant to be used as a practice and performance tool.";
    homepage = "https://github.com/Jumprocks1/drum-game";
    mainProgram = "DrumGame";
  };
})
