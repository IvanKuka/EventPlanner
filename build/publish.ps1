<#
.SYNOPSIS
  Self-contained publish of EventPlanner for one RID into artifacts/publish/<rid>.
.EXAMPLE
  ./build/publish.ps1 -Rid win-x64 -Version 0.1.0
#>
param(
    [Parameter(Mandatory)][ValidateSet('win-x64', 'osx-arm64', 'osx-x64')][string]$Rid,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "artifacts/publish/$Rid"

if (Test-Path $output) { Remove-Item -Recurse -Force $output }

# No trimming or single-file: start simple and optimize only after measurements (08-architecture.md).
dotnet publish (Join-Path $root 'EventPlanner.Desktop/EventPlanner.Desktop.csproj') `
    --configuration Release `
    --runtime $Rid `
    --self-contained true `
    --output $output `
    -p:Version=$Version `
    -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Rid" }

# Native dependency packages (SkiaSharp, HarfBuzz) ship ~100 MB of .pdb symbols that DebugType=none
# does not cover; end users never need them.
Get-ChildItem -Path $output -Filter '*.pdb' -Recurse | Remove-Item -Force

Write-Host "Published $Rid to $output"
