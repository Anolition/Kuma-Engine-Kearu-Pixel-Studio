param(
    [string]$Version = "0.0.40",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SelfContained,
    [switch]$SingleFile,
    [switch]$Archive
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$guardScript = Join-Path $PSScriptRoot "KumaWorkflowGuards.ps1"
. $guardScript

Assert-KumaSourceCheckout -ProjectRoot $projectRoot -Purpose "publish Kuma Engine"

$appProject = Join-Path $projectRoot "src\ProjectSPlus.App\ProjectSPlus.App.csproj"
$nugetConfig = Join-Path $projectRoot "NuGet.Config"
$releaseRoot = Join-Path $projectRoot "artifacts\releases"
$releaseName = "kuma-engine-$Version-$RuntimeIdentifier"
$publishDir = Join-Path $releaseRoot $releaseName
$selfContainedValue = if ($SelfContained.IsPresent) { "true" } else { "false" }
$singleFileValue = if ($SingleFile.IsPresent) { "true" } else { "false" }

if (-not (Test-Path -LiteralPath $appProject)) {
    throw "Could not find app project at $appProject"
}

if (-not (Test-Path -LiteralPath $nugetConfig)) {
    throw "Could not find NuGet.Config at $nugetConfig"
}

Assert-KumaNonOneDrivePath -Path $publishDir -Description "publish Kuma Engine"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

$versionParts = $Version.Split(".")
while ($versionParts.Count -lt 4) {
    $versionParts += "0"
}

$fileVersion = ($versionParts[0..3] -join ".")
$restoreArguments = @(
    "restore",
    $appProject,
    "-r", $RuntimeIdentifier,
    "--configfile", $nugetConfig
)
$publishArguments = @(
    "publish",
    $appProject,
    "-c", $Configuration,
    "-r", $RuntimeIdentifier,
    "--self-contained:$selfContainedValue",
    "--no-restore",
    "-o", $publishDir,
    "/p:PublishSingleFile=$singleFileValue",
    "/p:Version=$Version",
    "/p:AssemblyVersion=$fileVersion",
    "/p:FileVersion=$fileVersion",
    "/p:InformationalVersion=$Version"
)

Write-Host "Restoring dependencies for $RuntimeIdentifier ..."
dotnet @restoreArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed."
}

Write-Host "Publishing $releaseName ..."
dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$gitCommit = "unknown"
try {
    $gitCommit = (git -C $projectRoot rev-parse --short HEAD).Trim()
} catch {
}

$manifestPath = Join-Path $publishDir "release-manifest.txt"
$manifestLines = @(
    "Product: Kuma Engine + Kearu Pixel Studio",
    "Version: $Version",
    "Runtime: $RuntimeIdentifier",
    "Configuration: $Configuration",
    "SelfContained: $selfContainedValue",
    "SingleFile: $singleFileValue",
    "Commit: $gitCommit",
    "BuiltAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')",
    "SourceRoot: $projectRoot",
    "AppDataRoot: %LocalAppData%\Kuma Engine"
)
$manifestLines | Set-Content -LiteralPath $manifestPath

$readmeSource = Join-Path $projectRoot "README.md"
if (Test-Path -LiteralPath $readmeSource) {
    Copy-Item -LiteralPath $readmeSource -Destination (Join-Path $publishDir "README.md") -Force
}

if ($Archive.IsPresent) {
    $zipPath = Join-Path $releaseRoot "$releaseName.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath
    Write-Host "Archive created at $zipPath"
}

Write-Host "Publish complete: $publishDir"
