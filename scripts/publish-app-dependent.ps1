$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'publish-all.ps1'

if (!(Test-Path $scriptPath)) {
    throw "Missing script: $scriptPath"
}

& $scriptPath -Configuration Release -Runtime win-x64 -FrameworkDependent
