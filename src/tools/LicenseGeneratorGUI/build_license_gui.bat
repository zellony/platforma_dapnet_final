@echo off
echo Budowanie Generatora Licencji...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true -o ./publish
echo.
echo Gotowe! Pliki znajduja sie w folderze publish.
pause
