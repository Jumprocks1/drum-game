{
  description = "drum-game environement";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-26.05";
    nixgl = {
      url = "github:nix-community/nixGL";
      inputs.nixpkgs.follows = "nixpkgs";
    };
  };

  outputs =
    {
      self,
      nixpkgs,
      nixgl,
    }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };
      gl = nixgl.packages.${system};
      appPkg = pkgs.callPackage ./package.nix { };
      mesaApp = pkgs.writeShellScriptBin "DrumGame" ''
        exec ${gl.nixVulkanIntel}/bin/nixVulkanIntel \
             ${gl.nixGLIntel}/bin/nixGLIntel \
             ${appPkg}/bin/DrumGame "$@"
      '';
      nvidiaApp = pkgs.writeShellScriptBin "DrumGame" ''
        exec ${gl.nixVulkanNvidia}/bin/nixVulkanNvidia \
             ${gl.nixGLNvidia}/bin/nixGLNvidia \
             ${appPkg}/bin/DrumGame "$@"
      '';
    in
    {
      packages.${system} = {
        default = appPkg;
        mesa = mesaApp;
        nvidia = nvidiaApp;
        local = appPkg.overrideAttrs (_: {
          src = builtins.path { path = ./.; };
        });
      };
      devShells.${system}.default = pkgs.mkShell {
        inputsFrom = [ appPkg ];
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
