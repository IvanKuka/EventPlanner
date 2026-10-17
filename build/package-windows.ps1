<#
.SYNOPSIS
  Builds EventPlanner-windows-x64.zip and EventPlanner-Setup-x64.exe from artifacts/publish/win-x64.
  Requires Inno Setup 6 (iscc) for the installer.
#>
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'artifacts/publish/win-x64'
$packages = Join-Path $root 'artifacts/packages'
New-Item -ItemType Directory -Force $packages | Out-Null

if (-not (Test-Path (Join-Path $publish 'EventPlanner.exe'))) { throw "Run build/publish.ps1 -Rid win-x64 first." }

$zip = Join-Path $packages 'EventPlanner-windows-x64.zip'
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip

$iscc = Get-Command iscc -ErrorAction SilentlyContinue
$isccPath = if ($iscc) { $iscc.Source } else { Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe' }
if (-not (Test-Path $isccPath)) { throw "Inno Setup compiler (ISCC.exe) not found." }

& $isccPath "/DAppVersion=$Version" "/DSourceDir=$publish" "/DOutputDir=$packages" (Join-Path $root 'installer/windows/EventPlanner.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

Write-Host "Windows packages written to $packages"
