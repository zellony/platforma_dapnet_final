Param(
    [string]$Configuration = 'Debug',
    [string]$Runtime = 'win-x64',
    [string]$OutputDir = ''
)
$ErrorActionPreference = 'Stop'
$startDir = Get-Location

try {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
    $apiProj  = Join-Path $repoRoot 'src\backend\Platform.Api\Platform.Api.csproj'
    $outDir   = if ([string]::IsNullOrWhiteSpace($OutputDir)) { Join-Path $repoRoot 'artifacts\backend' } else { $OutputDir }
    $modulesOut = Join-Path $outDir 'modules'

    Write-Host "=== BUILDING MODULES ===" -ForegroundColor Cyan
    $moduleProjects = @(
        Join-Path $repoRoot 'src\modules\Platform.Module.Ksef\Platform.Module.Ksef.csproj'
        Join-Path $repoRoot 'src\modules\Platform.Module.Health\Platform.Module.Health.csproj'
    )

    foreach ($proj in $moduleProjects) {
        if (Test-Path $proj) {
            Write-Host "Building: $(Split-Path $proj -Leaf)" -ForegroundColor Gray
            dotnet build $proj -c $Configuration # USUNIĘTO Out-Null - błędy będą widoczne
        }
    }

    Write-Host "`n=== PUBLISHING API ===" -ForegroundColor Cyan
    if (Test-Path $outDir) { Remove-Item -Path $outDir -Recurse -Force -ErrorAction SilentlyContinue }
    dotnet publish $apiProj -c $Configuration -r $Runtime --self-contained true -o $outDir

    Write-Host "`n=== COPYING MODULES ===" -ForegroundColor Cyan
    if (!(Test-Path $modulesOut)) { New-Item -ItemType Directory -Force -Path $modulesOut }

    $moduleBinRoots = @(
        Join-Path $repoRoot 'src\modules\Platform.Module.Ksef\bin'
        Join-Path $repoRoot 'src\modules\Platform.Module.Health\bin'
    )

    foreach ($binRoot in $moduleBinRoots) {
        if (!(Test-Path $binRoot)) { continue }
        $dlls = Get-ChildItem -Path $binRoot -Recurse -Filter 'Platform.Module.*.dll' | 
                Where-Object { $_.Name -notlike "*.Views.dll" -and $_.Name -notlike "*.staticwebassets.dll" } |
                Sort-Object LastWriteTime -Descending
        if ($dlls.Count -gt 0) {
            $targetDll = $dlls[0]
            Copy-Item $targetDll.FullName -Destination $modulesOut -Force
            Write-Host "Copied module: $($targetDll.Name)" -ForegroundColor Green
        }
    }
    Write-Host "`nDone." -ForegroundColor Cyan
} finally {
    Set-Location $startDir
}
