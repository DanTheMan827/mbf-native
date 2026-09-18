[CmdletBinding()]
param(
    [string] $AgentUrl = "https://mbf.bsquest.xyz/mbf-agent",
    [string] $ExpectedAgentSha1 = ""
)

$ErrorActionPreference = "Stop"
& "$PSScriptRoot/Fetch-MbfAgent.ps1" -SourceUrl $AgentUrl -ExpectedSha1 $ExpectedAgentSha1
& "$PSScriptRoot/Fetch-PlatformTools.ps1"
