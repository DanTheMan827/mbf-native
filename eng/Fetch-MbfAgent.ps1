[CmdletBinding()]
param(
    [string] $SourceUrl = "https://mbf.bsquest.xyz/mbf-agent",
    [string] $ExpectedSha1 = "",
    [string] $Destination = "$PSScriptRoot/../src/ModsBeforeFriday.App/Assets/Agent/mbf-agent"
)

$ErrorActionPreference = "Stop"
$destinationPath = [System.IO.Path]::GetFullPath($Destination)
$destinationDirectory = Split-Path -Parent $destinationPath
New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null

$tempPath = "$destinationPath.download"
Write-Host "Fetching the deployed MBF Rust agent from $SourceUrl"
Invoke-WebRequest -Uri $SourceUrl -OutFile $tempPath

$item = Get-Item $tempPath
if ($item.Length -lt 100000) {
    Remove-Item $tempPath -Force
    throw "Downloaded agent is unexpectedly small ($($item.Length) bytes). Refusing to stage it."
}

$sha1 = (Get-FileHash -Algorithm SHA1 -Path $tempPath).Hash.ToUpperInvariant()
if ($ExpectedSha1 -and $sha1 -ne $ExpectedSha1.ToUpperInvariant()) {
    Remove-Item $tempPath -Force
    throw "Agent SHA1 mismatch. Expected $ExpectedSha1 but received $sha1."
}

Move-Item -Force $tempPath $destinationPath
Set-Content -NoNewline -Encoding ascii -Path "$destinationPath.sha1" -Value $sha1

$lock = [ordered]@{
    sourceUrl = $SourceUrl
    sha1 = $sha1
    length = (Get-Item $destinationPath).Length
    fetchedUtc = [DateTimeOffset]::UtcNow.ToString("O")
}
$lock | ConvertTo-Json | Set-Content -Encoding utf8 "$destinationDirectory/agent.lock.json"

Write-Host "Staged MBF agent: $destinationPath"
Write-Host "SHA1: $sha1"
