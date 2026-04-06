$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath = Join-Path $projectRoot 'src\ProjectSPlus.App\bin\Debug\net8.0\ProjectSPlus.App.exe'
$settingsPath = Join-Path $projectRoot 'src\ProjectSPlus.App\bin\Debug\net8.0\settings\appsettings.json'
$defaultWidth = 1600
$defaultHeight = 900
$minimumWidth = 960
$minimumHeight = 600

if (-not (Test-Path $exePath)) {
    throw "Project S+ executable not found at $exePath"
}

if (Test-Path $settingsPath) {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json

    if ($null -eq $settings.Window) {
        $settings | Add-Member -MemberType NoteProperty -Name Window -Value ([pscustomobject]@{})
    }

    if ([string]::IsNullOrWhiteSpace($settings.Window.Title)) {
        $settings.Window.Title = 'Project S+'
    }

    if ($settings.Window.Width -lt $minimumWidth) {
        $settings.Window.Width = $defaultWidth
    }

    if ($settings.Window.Height -lt $minimumHeight) {
        $settings.Window.Height = $defaultHeight
    }

    $settings | ConvertTo-Json -Depth 6 | Set-Content $settingsPath
}

Start-Process -FilePath $exePath -WorkingDirectory $projectRoot
