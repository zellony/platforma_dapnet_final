$ErrorActionPreference = 'Stop'
$startDir = Get-Location
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$frontendDir = Join-Path $repoRoot 'src\frontend\electron'

Write-Host '=== PLATFORMA DAPNET PROD SIMULATOR ===' -ForegroundColor Cyan
Write-Host 'Simulating production environment (DevTools locked by default)...' -ForegroundColor Yellow

try {
    try {
        $npmVersion = npm -v
        Write-Host "Node.js/npm detected: $npmVersion" -ForegroundColor Green
    } catch {
        Write-Error 'NPM not found! Please restart Rider/Terminal or install Node.js.'
    }

    Write-Host 'Starting App (npm run dev:prod)...' -ForegroundColor Cyan
    Set-Location $frontendDir

    if (!(Test-Path 'node_modules')) {
        Write-Host 'Installing npm dependencies...' -ForegroundColor Yellow
        npm install
    }

    npm run dev:prod
} finally {
    Set-Location $startDir
}
