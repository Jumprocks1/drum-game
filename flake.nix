{
  description = "drum-game environement";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  };

  outputs =
    { self, nixpkgs }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };
      appPkg = pkgs.callPackage ./package.nix { };
    in
    {
      packages.${system}.default = appPkg;
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
