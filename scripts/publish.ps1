<#
.SYNOPSIS
    Publie KARATE SCORING en un exécutable autonome prêt à distribuer sur un poste organisateur,
    sans installation préalable du .NET SDK. Sur Windows, publie aussi la coque desktop
    (KarateScoring.exe — icône, écran de démarrage, fenêtre native) en plus du serveur API, dans
    la disposition attendue par ApiHostLauncher : KarateScoring.exe à la racine, l'API dans "api/".

.EXAMPLE
    ./scripts/publish.ps1
    ./scripts/publish.ps1 -Runtime linux-x64   # serveur API seul, pas de coque desktop (WPF = Windows uniquement)
#>
param(
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "publish"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $root "src/KarateScoring.Api/KarateScoring.Api.csproj"
$desktopProject = Join-Path $root "src/KarateScoring.Desktop/KarateScoring.Desktop.csproj"
$outPath = Join-Path $root "$OutputDir/$Runtime"
$estWindows = $Runtime.StartsWith("win")
$apiOutPath = if ($estWindows) { Join-Path $outPath "api" } else { $outPath }

Write-Host "Publication de KARATE SCORING pour $Runtime..." -ForegroundColor Cyan

dotnet publish $apiProject `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $apiOutPath

if ($LASTEXITCODE -ne 0) { throw "La publication de l'API a échoué." }

if ($estWindows) {
    Write-Host "Publication de la coque desktop (KarateScoring.exe)..." -ForegroundColor Cyan
    dotnet publish $desktopProject `
        --configuration Release `
        --runtime $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        --output $outPath

    if ($LASTEXITCODE -ne 0) { throw "La publication de la coque desktop a échoué." }
}

$zipPath = Join-Path $root "$OutputDir/karate-scoring_$Runtime.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path "$outPath/*" -DestinationPath $zipPath

Write-Host "Terminé : $outPath" -ForegroundColor Green
Write-Host "Archive : $zipPath" -ForegroundColor Green
Write-Host ""
if ($estWindows) {
    Write-Host "Pour lancer sur le poste organisateur : exécuter KarateScoring.exe (ou passer par l'installateur, voir installer/setup.iss)."
} else {
    Write-Host "Pour lancer : exécuter KarateScoring.Api.exe (pas de coque desktop sur cette plateforme, voir DEPLOYMENT.md)."
}
