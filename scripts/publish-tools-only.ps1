Param(
    [ValidateSet('All', 'LicenseGenerator', 'LicenseGeneratorGUI')]
    [string]$Target = 'All',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$startDir = Get-Location

try {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
    $toolsDir = Join-Path $repoRoot 'artifacts\tools'
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null

    if ($Target -eq 'All' -or $Target -eq 'LicenseGenerator') {
        dotnet publish (Join-Path $repoRoot 'src\tools\LicenseGenerator\LicenseGenerator.csproj') `
            -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true `
            -o (Join-Path $toolsDir 'LicenseGenerator')
    }

    if ($Target -eq 'All' -or $Target -eq 'LicenseGeneratorGUI') {
        dotnet publish (Join-Path $repoRoot 'src\tools\LicenseGeneratorGUI\LicenseGeneratorGUI.csproj') `
            -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true `
            -o (Join-Path $toolsDir 'LicenseGeneratorGUI')
    }
} finally {
    Set-Location $startDir
}
