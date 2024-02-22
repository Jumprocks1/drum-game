#!/usr/bin/env pwsh

param(
    [switch]$NoRestore,
    [switch]$NoCompile
)


$dir = Split-Path $MyInvocation.MyCommand.Path

$par = New-Object System.Collections.Generic.List[string]

$par.Add("--no-discord")

if ($NoCompile) {
    if ($args) {
        foreach ($arg in $args) {
            $par.Add($arg)
        }
    }
    & "$dir/DrumGame.Desktop/bin/Debug/net8.0/DrumGame.exe" @par
    exit;
}

if ($NoRestore) {
    $par.Add("--no-restore")
}

if ($args) {
    $par.Add("--")
    foreach ($arg in $args) {
        $par.Add($arg)
    }
}



dotnet run --project $dir/DrumGame.Desktop/DrumGame.Desktop.csproj @par
