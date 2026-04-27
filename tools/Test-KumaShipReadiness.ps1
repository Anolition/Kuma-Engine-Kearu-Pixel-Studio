param(
    [string]$Configuration = "Debug",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$guardScript = Join-Path $PSScriptRoot "KumaWorkflowGuards.ps1"
. $guardScript

Assert-KumaSourceCheckout -ProjectRoot $projectRoot -Purpose "verify Kuma Engine"

$staleSoundLabelMatches = & git -C $projectRoot grep -n -E "Frog 1|Frog 2" -- "src" 2>$null
if ($LASTEXITCODE -eq 0) {
    throw "Found stale sound labels that should not ship:`n$($staleSoundLabelMatches -join [Environment]::NewLine)"
}

$stalePathMatches = & git -C $projectRoot grep -n -E "C:\\Users\\mrflu\\OneDrive|Codex Project" -- "." ":!tools/Test-KumaShipReadiness.ps1" 2>$null
if ($LASTEXITCODE -eq 0) {
    throw "Found stale OneDrive workflow text that should not ship:`n$($stalePathMatches -join [Environment]::NewLine)"
}

$appDataRoot = Join-Path $env:LOCALAPPDATA "Kuma Engine"
Assert-KumaNonOneDrivePath -Path $appDataRoot -Description "store Kuma app data"

if (-not $SkipBuild.IsPresent) {
    $solutionPath = Join-Path $projectRoot "ProjectSPlus.sln"
    & dotnet build $solutionPath -c $Configuration --no-restore -v:m -m:1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed."
    }
}

$appOutputRoot = Join-Path $projectRoot "src\ProjectSPlus.App\bin\$Configuration\net8.0"
Assert-KumaNonOneDrivePath -Path $appOutputRoot -Description "use Kuma build output"

$requiredOutputFiles = @(
    "ProjectSPlus.App.exe",
    "ProjectSPlus.App.dll",
    "Assets\Sounds\crash-bear.mp3",
    "Assets\Sounds\warning-frog-primary.mp3",
    "Assets\Sounds\warning-frog-secondary.mp3"
)

foreach ($relativePath in $requiredOutputFiles) {
    $fullPath = Join-Path $appOutputRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Missing required build output file: $fullPath"
    }
}

Write-Host "Kuma ship-readiness checks passed for $Configuration."
Write-Host "Source: $projectRoot"
Write-Host "App data: $appDataRoot"
Write-Host "Output: $appOutputRoot"
