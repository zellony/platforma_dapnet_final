@echo off
echo Budowanie Generatora Licencji...

set SCRIPT_DIR=%~dp0
set REPO_ROOT=%SCRIPT_DIR%..

dotnet publish "%REPO_ROOT%\src\tools\LicenseGenerator\LicenseGenerator.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "%REPO_ROOT%\artifacts\tools\LicenseGenerator"

echo.
echo Gotowe! Narzedzie znajduje sie w: artifacts/tools/LicenseGenerator/LicenseGenerator.exe
echo.
pause
