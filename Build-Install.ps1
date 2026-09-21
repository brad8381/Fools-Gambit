param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "FoolsGambit.csproj"

function Invoke-DotNet {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path (Join-Path $PSScriptRoot "local.props"))) {
    Write-Warning "local.props not found; using project auto-detection for STS2/Godot paths."
}

Write-Host "== Fool's Gambit: restore =="
Invoke-DotNet restore $project

Write-Host "== Fool's Gambit: build + install DLL =="
Invoke-DotNet build $project -c $Configuration -p:InstallToGame=true --no-restore

Write-Host "== Fool's Gambit: publish + export PCK =="
Invoke-DotNet publish $project -c $Configuration -p:InstallToGame=true --no-restore

Write-Host ""
Write-Host "Build/install complete."
