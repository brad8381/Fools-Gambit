param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "FoolsGambit.csproj"

if (-not (Test-Path (Join-Path $PSScriptRoot "local.props"))) {
    Write-Warning "local.props not found. Copy local.props.example to local.props and set GodotPath if auto-detection is not enough."
}

Write-Host "== Fool's Gambit: restore =="
dotnet restore $project

Write-Host "== Fool's Gambit: build + install DLL =="
dotnet build $project -c $Configuration -p:InstallToGame=true --no-restore

Write-Host "== Fool's Gambit: publish + export PCK =="
dotnet publish $project -c $Configuration -p:InstallToGame=true --no-restore

Write-Host ""
Write-Host "Build/install complete."
