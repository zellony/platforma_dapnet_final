Param(
    [switch]$WithInstaller
)

$ErrorActionPreference = 'Stop'
$startDir = Get-Location

try {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
    $frontendDir = Join-Path $repoRoot 'src\frontend\electron'

    Set-Location $frontendDir

    if (!(Test-Path 'node_modules')) {
        npm install
    }

    if ($WithInstaller) {
        npm run dist
    } else {
        npm run build
    }
} finally {
    Set-Location $startDir
}
