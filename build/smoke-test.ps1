<#
.SYNOPSIS
  Runs the packaged app with --smoke-test: starts the UI runtime, initializes a profile in an
  isolated temp data root and exits. Never touches the real user profile.
.EXAMPLE
  ./build/smoke-test.ps1 -Executable artifacts/publish/win-x64/EventPlanner.exe
#>
param(
    [Parameter(Mandatory)][string]$Executable,
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Executable)) { throw "Executable not found: $Executable" }

$process = Start-Process -FilePath $Executable -ArgumentList '--smoke-test' -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    $process.Kill($true)
    throw "Smoke test timed out after $TimeoutSeconds s"
}

if ($process.ExitCode -ne 0) { throw "Smoke test failed with exit code $($process.ExitCode)" }
Write-Host "Smoke test passed: $Executable"
