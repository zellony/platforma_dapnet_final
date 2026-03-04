$ErrorActionPreference = 'Stop'
$startDir = Get-Location
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$frontendDir = Join-Path $repoRoot 'src\frontend\electron'

Write-Host '=== PLATFORMA DAPNET LAUNCHER ===' -ForegroundColor Cyan

try {
    try {
        $npmVersion = npm -v
        Write-Host "Node.js/npm detected: $npmVersion" -ForegroundColor Green
    } catch {
        Write-Error 'NPM not found! Please restart Rider/Terminal or install Node.js.'
    }

    Write-Host 'Starting App (npm run dev)...' -ForegroundColor Cyan
    Set-Location $frontendDir

    if (!(Test-Path 'node_modules')) {
        Write-Host 'Installing npm dependencies...' -ForegroundColor Yellow
        npm install
    }

    npm run dev
} finally {
    Set-Location $startDir
}
