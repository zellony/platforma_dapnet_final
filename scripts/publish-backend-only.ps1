Param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$Runtime = 'win-x64',
    [string]$OutputDir = ''
)

$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'publish-backend-dev.ps1'

if (!(Test-Path $scriptPath)) {
    throw "Missing script: $scriptPath"
}

& $scriptPath -Configuration $Configuration -Runtime $Runtime -OutputDir $OutputDir
