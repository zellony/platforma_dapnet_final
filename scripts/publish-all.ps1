Param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$FrameworkDependent,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$startDir = Get-Location

try {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
    $frontendDir = Join-Path $repoRoot 'src\frontend\electron'
    $artifactsDir = Join-Path $repoRoot 'artifacts'
    $backendDir = Join-Path $artifactsDir 'backend'
    $backendModulesDir = Join-Path $backendDir 'modules'
    $frontendArtifactsDir = Join-Path $artifactsDir 'frontend'
    $installerDir = Join-Path $artifactsDir 'installer'

    Write-Host "=== PLATFORMA DAPNET PUBLISH ===" -ForegroundColor Cyan
    Write-Host "Configuration: $Configuration, Runtime: $Runtime" -ForegroundColor Gray

    Write-Host "`n[1/4] Cleaning artifacts..." -ForegroundColor Yellow
    foreach ($dir in @($backendDir, $frontendArtifactsDir, $installerDir)) {
        if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }

    Write-Host "[2/4] Building modules..." -ForegroundColor Yellow
    $moduleProjects = @(
        Join-Path $repoRoot 'src\modules\Platform.Module.Ksef\Platform.Module.Ksef.csproj'
        Join-Path $repoRoot 'src\modules\Platform.Module.Health\Platform.Module.Health.csproj'
    )

    foreach ($proj in $moduleProjects) {
        dotnet build $proj -c $Configuration
    }

    Write-Host "[3/4] Publishing backend..." -ForegroundColor Yellow
    if ($FrameworkDependent) {
        dotnet publish (Join-Path $repoRoot 'src\backend\Platform.Api\Platform.Api.csproj') `
            -c $Configuration `
            -r $Runtime `
            -p:PublishSingleFile=true `
            -p:SelfContained=false `
            -o $backendDir
    } else {
        dotnet publish (Join-Path $repoRoot 'src\backend\Platform.Api\Platform.Api.csproj') `
            -c $Configuration `
            -r $Runtime `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -o $backendDir
    }

    New-Item -ItemType Directory -Force -Path $backendModulesDir | Out-Null
    $moduleDlls = @(
        Join-Path $repoRoot "src\modules\Platform.Module.Ksef\bin\$Configuration\net8.0\Platform.Module.Ksef.dll"
        Join-Path $repoRoot "src\modules\Platform.Module.Health\bin\$Configuration\net8.0\Platform.Module.Health.dll"
    )
    foreach ($dll in $moduleDlls) {
        if (!(Test-Path $dll)) { throw "Missing module DLL: $dll" }
        Copy-Item $dll -Destination $backendModulesDir -Force
    }

    Write-Host "[4/4] Building frontend..." -ForegroundColor Yellow
    Set-Location $frontendDir
    if (!(Test-Path (Join-Path $frontendDir 'node_modules'))) {
        npm install
    }
    if ($SkipInstaller) {
        npm run build
    } else {
        npm run dist
    }

    if (Test-Path (Join-Path $frontendDir 'dist')) {
        Copy-Item (Join-Path $frontendDir 'dist') -Destination $frontendArtifactsDir -Recurse -Force
    }
    if (Test-Path (Join-Path $frontendDir 'dist-electron')) {
        Copy-Item (Join-Path $frontendDir 'dist-electron') -Destination $frontendArtifactsDir -Recurse -Force
    }

    Write-Host "`nPublish finished." -ForegroundColor Green
    Write-Host "Backend:   $backendDir" -ForegroundColor Gray
    Write-Host "Frontend:  $frontendArtifactsDir" -ForegroundColor Gray
    Write-Host "Installer: $installerDir" -ForegroundColor Gray
} finally {
    Set-Location $startDir
}
