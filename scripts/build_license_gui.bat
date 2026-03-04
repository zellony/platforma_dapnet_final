@echo off
echo Budowanie Generatora Licencji (GUI)...

set SCRIPT_DIR=%~dp0
set REPO_ROOT=%SCRIPT_DIR%..

dotnet publish "%REPO_ROOT%\src\tools\LicenseGeneratorGUI\LicenseGeneratorGUI.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "%REPO_ROOT%\artifacts\tools\LicenseGeneratorGUI"

echo.
echo Gotowe! Narzedzie znajduje sie w: artifacts/tools/LicenseGeneratorGUI/LicenseGeneratorGUI.exe
echo.
pause
