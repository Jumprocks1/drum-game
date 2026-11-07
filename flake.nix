{
  description = "DrumGame with the host's GPU drivers and audio";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    nixgl.url = "github:nix-community/nixGL";
  };

  outputs =
    {
      self,
      nixpkgs,
      nixgl,
    }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs {
        inherit system;
        config.allowUnfree = true;
      };
      mesa = import nixgl { inherit pkgs; };
      nvidiaVersion = builtins.head (
        builtins.match ".*Module .* ([0-9.]+)  .*" (
          builtins.readFile (
            pkgs.runCommand "nvidia-version" {
              time = builtins.currentTime;
              preferLocalBuild = true;
            } "cp /proc/driver/nvidia/version $out"
          )
        )
      );
      nvidia = import nixgl {
        pkgs = import nixgl.inputs.nixpkgs {
          inherit system;
          config.allowUnfree = true;
        };
        inherit nvidiaVersion;
      };

      nvidiaLoaded =
        builtins.pathExists "/proc/driver/nvidia/version" && !builtins.pathExists "/run/opengl-driver";

      drivers = [
        mesa.nixGLIntel
        mesa.nixVulkanIntel
      ]
      ++ pkgs.lib.optionals nvidiaLoaded [
        nvidia.nixGLNvidia
        nvidia.nixVulkanNvidia
      ];

      alsa = pkgs.alsa-lib-with-plugins.override {
        plugins = [
          pkgs.alsa-plugins
          pkgs.pipewire
        ];
      };
      game = pkgs.callPackage nix/package.nix { alsa-lib = alsa; };

      run = pkgs.writeShellScript "drum-game" ''
        if [[ -S $XDG_RUNTIME_DIR/pipewire-0 && ! -e /etc/asound.conf && ! -e ~/.asoundrc
              && ! -e ''${XDG_CONFIG_HOME:-~/.config}/alsa/asoundrc ]]; then
          export ALSA_CONFIG_PATH=${alsa}/share/alsa/alsa.conf:${pkgs.pipewire}/share/alsa/alsa.conf.d/99-pipewire-default.conf
        fi
        exec ${game}/bin/DrumGame "$@"
      '';
    in
    {
      packages.${system} = {
        default = pkgs.writeShellScriptBin "DrumGame" ''
          [[ -e /run/opengl-driver ]] && exec ${run} "$@"
          ${pkgs.lib.optionalString (!nvidiaLoaded) ''
            [[ -e /proc/driver/nvidia/version ]] \
              && { echo "DrumGame: run with --impure to detect the NVIDIA driver" >&2; exit 1; }
          ''}
          exec ${pkgs.lib.concatMapStringsSep " " (d: "${d}/bin/*") drivers} ${run} "$@"
        '';
        local = game.overrideAttrs (_: {
          src = builtins.path { path = ./.; };
        });
      };
      devShells.${system}.default = pkgs.mkShell {
        inputsFrom = [ game ];
        buildInputs = with pkgs; [
          roslyn-ls
        ];
        shellHook = ''
          # Bring xdg data dirs of build inputs into the environement
          xdg_inputs=( "''${buildInputs[@]}" )
          for p in ''${xdg_inputs[@]}; do
            if [[ -d "$p/share" ]]; then
              XDG_DATA_DIRS="''${XDG_DATA_DIRS}''${XDG_DATA_DIRS+:}$p/share"
            fi
          done
          export XDG_DATA_DIRS
        '';
      };
    };
}
