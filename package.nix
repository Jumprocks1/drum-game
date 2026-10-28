{
  lib,
  buildDotnetModule,
  fetchFromGitHub,
  fetchzip,
  unzip,
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
  version = "0.9.4";

  src = fetchFromGitHub {
    owner = "Jumprocks1";
    repo = "drum-game";
    tag = "v${finalAttrs.version}";
    sha256 = "sha256-c5FvD2m3mkP8/IYpG82Ib5mlfsoFBTwAeNTbg8WUsjw=";
  };

  release = fetchzip {
    url = "https://github.com/Jumprocks1/drum-game/releases/download/v${finalAttrs.version}/DrumGame-v${finalAttrs.version}-net8.0.zip";
    sha256 = "s3h3wvdyXWKnbo0OxpsUxDIJnPcdIhoS5M7VCxnP8a8=";
    stripRoot = false;
  };

  bassOPUS = fetchzip {
    url = "https://www.un4seen.com/files/bassopus24-linux.zip";
    sha256 = "hF4En9lxP8lec162kD1sCSu9BhvLeJRJMXJFxvxeHw8=";
    stripRoot = false;
  };

  bassWEBM = fetchzip {
    url = "https://www.un4seen.com/files/basswebm24-linux.zip";
    sha256 = "TQARFSYm1zRgYr4INkQ/kQ9uFu6ne/FRfSm243gB2bQ=";
    stripRoot = false;
  };

  bassAAC = fetchzip {
    url = "https://www.un4seen.com/files/z/2/bass_aac24-linux.zip";
    sha256 = "8oTVnZUMyWCaUUPTap9sVr50fidFQbUFDhxCNDIB+l4=";
    stripRoot = false;
  };

  projectFile = "DrumGame/DrumGame.Desktop/DrumGame.Desktop.csproj";
  nugetDeps = ./deps.json;

  dotnet-sdk = dotnetCorePackages.sdk_8_0;
  dotnet-runtime = dotnetCorePackages.runtime_8_0;

  nativeBuildInputs = [ unzip ];

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
    mkdir -p $out/share/DrumGame/resources/lib
    cp -r "$release/resources" $out/share/DrumGame/
    cp "$bassOPUS/libs/x86_64/libbassopus.so" $out/share/DrumGame/resources/lib/
    cp "$bassWEBM/libs/x86_64/libbasswebm.so" $out/share/DrumGame/resources/lib/
    cp "$bassAAC/libs/x86_64/libbass_aac.so" $out/share/DrumGame/resources/lib/
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
