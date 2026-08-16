@echo off
REM Markdown Viewer - Uninstaller

setlocal EnableDelayedExpansion

echo === Markdown Viewer - Uninstaller ===
echo.

set "APP_NAME=Markdown Viewer"
set "INSTALL_FOLDER=%ProgramFiles%\%APP_NAME%"

REM Confirm uninstall
set /p "CONFIRM=Are you sure you want to uninstall %APP_NAME%? (Y/N): "
if /i not "!CONFIRM!"=="Y" (
    echo.
    echo Uninstall cancelled.
    pause
    exit /b 0
)

echo.
echo Removing Start Menu shortcuts...
set "STARTMENU_FOLDER=%APPDATA%\Microsoft\Windows\Start Menu\Programs\%APP_NAME%"
if exist "!STARTMENU_FOLDER!" rmdir /S /Q "!STARTMENU_FOLDER!"

echo Removing Desktop shortcuts...
del /Q "%USERPROFILE%\Desktop\%APP_NAME%.lnk" 2>nul

echo Removing file associations...
powershell -Command "Remove-Item -Path 'HKCU:\Software\Classes\.md' -Force -ErrorAction SilentlyContinue"
powershell -Command "Remove-Item -Path 'HKCU:\Software\Classes\MarkdownViewer.md' -Recurse -Force -ErrorAction SilentlyContinue"

echo Removing application files...
if exist "!INSTALL_FOLDER!" (
    rmdir /S /Q "!INSTALL_FOLDER!"
)

echo Removing registry settings...
powershell -Command "Remove-Item -Path 'HKCU:\Software\Fremen IT Workers\Markdown Viewer' -Recurse -Force -ErrorAction SilentlyContinue"

echo.
echo === Uninstall Complete ===
echo.
echo %APP_NAME% has been removed from your system.
echo.
pause
