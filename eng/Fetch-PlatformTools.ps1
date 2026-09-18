[CmdletBinding()]
param(
    [string] $SourceUrl = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip",
    [string] $Destination = "$PSScriptRoot/../src/ModsBeforeFriday.App/Assets/PlatformTools"
)

$ErrorActionPreference = "Stop"
$destinationPath = [System.IO.Path]::GetFullPath($Destination)
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("mbf-platform-tools-" + [Guid]::NewGuid().ToString("N"))
$zipPath = Join-Path $tempRoot "platform-tools.zip"
$extractPath = Join-Path $tempRoot "extract"

try {
    New-Item -ItemType Directory -Force -Path $tempRoot, $extractPath | Out-Null
    Write-Host "Fetching Android platform-tools from $SourceUrl"
    Invoke-WebRequest -Uri $SourceUrl -OutFile $zipPath
    Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

    $source = Join-Path $extractPath "platform-tools"
    if (-not (Test-Path (Join-Path $source "adb.exe"))) {
        throw "Downloaded archive did not contain platform-tools/adb.exe."
    }

    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $destinationPath
    New-Item -ItemType Directory -Force -Path $destinationPath | Out-Null
    Copy-Item -Recurse -Force (Join-Path $source "*") $destinationPath
    Write-Host "Staged platform-tools: $destinationPath"
}
finally {
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $tempRoot
}
