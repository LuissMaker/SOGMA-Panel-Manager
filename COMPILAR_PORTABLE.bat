@echo off
setlocal
cd /d "%~dp0"

echo.
echo ============================================
echo   SOGMA Panel Manager - compilar portable
echo ============================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo No se encontro .NET SDK.
    echo Instala .NET 8 SDK desde:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

if not exist "src\SOGMAPanelManager\MainWindow.xaml" (
    echo Reconstruyendo archivos grandes desde _chunks...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$src='src/SOGMAPanelManager'; $chunks=Join-Path $src '_chunks'; $p=Get-ChildItem \"$chunks/MainWindow.xaml.*.part\"|Sort-Object Name; [IO.File]::WriteAllText((Join-Path $src 'MainWindow.xaml'),(($p|%%{Get-Content $_.FullName -Raw}) -join ''),[Text.UTF8Encoding]::new($false)); $p=Get-ChildItem \"$chunks/MainWindow.xaml.cs.*.part\"|Sort-Object Name; [IO.File]::WriteAllText((Join-Path $src 'MainWindow.xaml.cs'),(($p|%%{Get-Content $_.FullName -Raw}) -join ''),[Text.UTF8Encoding]::new($false))"
)

dotnet publish "src\SOGMAPanelManager\SOGMAPanelManager.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "PORTABLE"

if errorlevel 1 (
    echo.
    echo La compilacion fallo.
    pause
    exit /b 1
)

echo.
echo Listo: PORTABLE\SOGMA Panel Manager.exe
echo.
pause
