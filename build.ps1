# REDSHIFT — CI locale minimale (SPEC §9 P0).
# Build Windows en batchmode. L'editeur Unity doit etre FERME (lock projet).
# Usage : .\build.ps1
param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe"
)

$projectPath = $PSScriptRoot
$logFile = Join-Path $projectPath "Logs\build.log"

& $UnityPath -batchmode -quit `
    -projectPath $projectPath `
    -executeMethod Redshift.Editor.BuildScript.BuildWindows `
    -logFile $logFile

if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED (exit $LASTEXITCODE) — voir $logFile" -ForegroundColor Red
} else {
    Write-Host "BUILD OK — Builds\Windows\Redshift.exe" -ForegroundColor Green
}
exit $LASTEXITCODE
