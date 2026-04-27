$ErrorActionPreference = "Stop"

function Get-KumaFullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return [System.IO.Path]::GetFullPath($Path)
}

function Get-KumaDirectoryPrefix {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolved = Get-KumaFullPath -Path $Path
    $resolved = $resolved.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    return "$resolved$([System.IO.Path]::DirectorySeparatorChar)"
}

function Get-KumaOneDriveRoots {
    $roots = @()
    foreach ($environmentName in @("OneDrive", "OneDriveConsumer", "OneDriveCommercial")) {
        $value = [Environment]::GetEnvironmentVariable($environmentName)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $roots += $value
        }
    }

    $userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    if (-not [string]::IsNullOrWhiteSpace($userProfile)) {
        $roots += (Join-Path $userProfile "OneDrive")
    }

    return $roots |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { Get-KumaFullPath -Path $_ } |
        Select-Object -Unique
}

function Test-KumaPathUnderRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $pathPrefix = Get-KumaDirectoryPrefix -Path $Path
    $rootPrefix = Get-KumaDirectoryPrefix -Path $Root
    return $pathPrefix.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)
}

function Test-KumaPathIsOneDrive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    foreach ($root in Get-KumaOneDriveRoots) {
        if (Test-KumaPathUnderRoot -Path $Path -Root $root) {
            return $true
        }
    }

    return $false
}

function Assert-KumaNonOneDrivePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [string]$Description = "use this path"
    )

    if (Test-KumaPathIsOneDrive -Path $Path) {
        $resolved = Get-KumaFullPath -Path $Path
        throw "Refusing to $Description from a OneDrive-backed path: $resolved`nUse a non-synced checkout such as C:\Dev\Kuma-Engine-Kearu-Pixel-Studio."
    }
}

function Assert-KumaRepoRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot
    )

    $requiredPaths = @(
        "ProjectSPlus.sln",
        "NuGet.Config",
        "src\ProjectSPlus.App\ProjectSPlus.App.csproj",
        "tools\KumaWorkflowGuards.ps1"
    )

    foreach ($relativePath in $requiredPaths) {
        $fullPath = Join-Path $ProjectRoot $relativePath
        if (-not (Test-Path -LiteralPath $fullPath)) {
            throw "This does not look like the Kuma Engine source root. Missing: $relativePath"
        }
    }
}

function Assert-KumaSourceCheckout {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot,

        [string]$Purpose = "use Kuma Engine"
    )

    Assert-KumaRepoRoot -ProjectRoot $ProjectRoot
    Assert-KumaNonOneDrivePath -Path $ProjectRoot -Description $Purpose
}
