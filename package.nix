{
  lib,
  buildDotnetModule,
  fetchFromGitHub,
  fetchzip,
  unzip,
  makeWrapper,
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

buildDotnetModule (finalAttrs: {
  pname = "drum-game";
  version = "0.9.5";

  src = fetchFromGitHub {
    owner = "Jumprocks1";
    repo = "drum-game";
    tag = "v${finalAttrs.version}";
    sha256 = "jPFJHkGt3FIa1E297J4o2IKpy1DPaE2gG9sCv+/Z4E8=";
  };

  release = fetchzip {
    url = "https://github.com/Jumprocks1/drum-game/releases/download/v${finalAttrs.version}/DrumGame-v${finalAttrs.version}-net10.0.zip";
    sha256 = "NxApYBCUXA9kKT3NLH4y5gn1o5OWPcVtzP9+eECe5ik=";
    stripRoot = false;
  };

  projectFile = "DrumGame/DrumGame.Desktop/DrumGame.Desktop.csproj";
  nugetDeps = ./deps.json;

  dotnet-sdk = dotnetCorePackages.sdk_10_0;
  dotnet-runtime = dotnetCorePackages.runtime_10_0;

  nativeBuildInputs = [
    unzip
    makeWrapper
  ];

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
    cp -r "$release/resources" $out/share/DrumGame/
  '';

  fixupPhase = ''
    runHook preFixup

    wrapProgram $out/bin/DrumGame \
      ${lib.optionalString nativeWayland "--set SDL_VIDEODRIVER wayland"} \
      --run '
        d="''${XDG_DATA_HOME:-$HOME/.local/share}/DrumGame"
        if [ ! -e "$d/.seeded-${finalAttrs.version}" ]; then
          mkdir -p "$d"
          cp -r --no-preserve=mode,ownership --update=none ${placeholder "out"}/share/DrumGame/resources "$d/"
          touch "$d/.seeded-${finalAttrs.version}"
        fi
      '

    ln -sft $out/lib/${finalAttrs.pname} ${SDL2}/lib/libSDL2${stdenvNoCC.hostPlatform.extensions.sharedLibrary}
    ln -sft $out/lib/${finalAttrs.pname} ${sdl3}/lib/libSDL3${stdenvNoCC.hostPlatform.extensions.sharedLibrary}

    runHook postFixup
  '';

  executables = [ "DrumGame" ];

  meta = {
    description = "Free rhythm game for electronic drummers";
    homepage = "https://github.com/Jumprocks1/drum-game";
    mainProgram = "DrumGame";
    license = with lib.licenses; [
      mit
      unfreeRedistributable # bass plugins
    ];
    platforms = [
      "x86_64-linux"
    ];
  };
})
