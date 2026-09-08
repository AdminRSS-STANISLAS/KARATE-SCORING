<#
.SYNOPSIS
    Publie KARATE SCORING (KarateScoring.Api) en un exécutable autonome prêt à distribuer
    sur un poste organisateur, sans installation préalable du .NET SDK.

.EXAMPLE
    ./scripts/publish.ps1
    ./scripts/publish.ps1 -Runtime linux-x64
#>
param(
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "publish"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src/KarateScoring.Api/KarateScoring.Api.csproj"
$outPath = Join-Path $root "$OutputDir/$Runtime"

Write-Host "Publication de KARATE SCORING pour $Runtime..." -ForegroundColor Cyan

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $outPath

if ($LASTEXITCODE -ne 0) { throw "La publication a échoué." }

$zipPath = Join-Path $root "$OutputDir/karate-scoring_$Runtime.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$outPath/*" -DestinationPath $zipPath

Write-Host "Terminé : $outPath" -ForegroundColor Green
Write-Host "Archive : $zipPath" -ForegroundColor Green
Write-Host ""
Write-Host "Pour lancer sur le poste organisateur : exécuter KarateScoring.Api.exe (voir DEPLOYMENT.md)."
