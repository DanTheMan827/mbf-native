$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

$nonUiProjects = @(
    (Join-Path $repo 'src/ModsBeforeFriday.Adb')
    (Join-Path $repo 'src/ModsBeforeFriday.Core')
    (Join-Path $repo 'src/ModsBeforeFriday.Application')
    (Join-Path $repo 'src/ModsBeforeFriday.Backend')
)

$uiTokens = @(
    'Microsoft.UI',
    'Microsoft.WindowsAppSDK',
    'Windows.Storage.Pickers',
    'Microsoft.UI.Xaml'
)

foreach ($project in $nonUiProjects) {
    $files = Get-ChildItem $project -Recurse -File -Include *.cs,*.csproj
    foreach ($token in $uiTokens) {
        $matches = $files | Select-String -SimpleMatch $token
        if ($matches) {
            $matches | ForEach-Object { Write-Error "UI dependency leaked into non-UI project: $($_.Path):$($_.LineNumber): $($_.Line)" }
        }
    }
}

$app = Join-Path $repo 'src/ModsBeforeFriday.App'
$forbiddenAppTokens = @(
    'host:transport:',
    '/data/local/tmp/mbf-agent',
    'mods.bsquest.xyz/'
)
$files = Get-ChildItem $app -Recurse -File -Include *.cs,*.xaml
foreach ($token in $forbiddenAppTokens) {
    $matches = $files | Select-String -SimpleMatch $token
    if ($matches) {
        $matches | ForEach-Object { Write-Error "Backend/protocol detail leaked into WinUI project: $($_.Path):$($_.LineNumber): $($_.Line)" }
    }
}

Write-Host 'Architecture boundary verification passed.'
