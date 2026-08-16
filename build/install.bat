@echo off
REM Markdown Viewer - Simple Batch Installer
REM This script creates shortcuts and copies files for easy installation

setlocal EnableDelayedExpansion

echo === Markdown Viewer - Simple Installer ===
echo.

REM Get the directory where this batch file is located
set "SCRIPT_DIR=%~dp0"
set "APP_NAME=Markdown Viewer"
set "MANUFACTURER=Fremen IT Workers"

REM Default installation folder
set "INSTALL_FOLDER=%ProgramFiles%\%APP_NAME%"

REM Ask for installation location
echo Installation folder: %INSTALL_FOLDER%
set /p "INSTALL_FOLDER=Press Enter to accept or type new path: "
if "!INSTALL_FOLDER!"=="" set "INSTALL_FOLDER=%ProgramFiles%\%APP_NAME%"

echo.
echo Installing to: !INSTALL_FOLDER!
echo.

REM Create installation folder
if not exist "!INSTALL_FOLDER!" (
    echo Creating folder...
    mkdir "!INSTALL_FOLDER!"
)

REM Copy application files
echo Copying application files...
xcopy /E /Y /I "%SCRIPT_DIR%app\*" "!INSTALL_FOLDER!"

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: Failed to copy files. Run as Administrator.
    pause
    exit /b 1
)

REM Create Start Menu folder
set "STARTMENU_FOLDER=%APPDATA%\Microsoft\Windows\Start Menu\Programs\%APP_NAME%"
if not exist "!STARTMENU_FOLDER!" mkdir "!STARTMENU_FOLDER!"

REM Create Start Menu shortcut
echo Creating Start Menu shortcut...
powershell -Command "$WshShell = New-Object -ComObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('!STARTMENU_FOLDER!\%APP_NAME%.lnk'); $Shortcut.TargetPath = '!INSTALL_FOLDER!\MarkdownViewer.exe'; $Shortcut.WorkingDirectory = '!INSTALL_FOLDER!'; $Shortcut.Description = 'Markdown Viewer and Editor'; $Shortcut.Save()"

REM Create Desktop shortcut (optional)
set /p "DESKTOP=Y - Create Desktop shortcut? (Y/N): "
if /i "!DESKTOP!"=="Y" (
    echo Creating Desktop shortcut...
    powershell -Command "$WshShell = New-Object -ComObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%USERPROFILE%\Desktop\%APP_NAME%.lnk'); $Shortcut.TargetPath = '!INSTALL_FOLDER!\MarkdownViewer.exe'; $Shortcut.WorkingDirectory = '!INSTALL_FOLDER!'; $Shortcut.Description = 'Markdown Viewer and Editor'; $Shortcut.Save()"
)

REM Register .md file association
echo.
echo Registering .md file association...
powershell -Command "New-ItemProperty -Path 'HKCU:\Software\Classes\.md' -Name 'OpenWithProgids' -Value 'MarkdownViewer.md' -Force -ErrorAction SilentlyContinue"
powershell -Command "New-ItemProperty -Path 'HKCU:\Software\Classes\MarkdownViewer.md\shell\open\command' -Name '(Default)' -Value '\"!INSTALL_FOLDER!\MarkdownViewer.exe\" \"%%1\"' -Force -ErrorAction SilentlyContinue"

echo.
echo === Installation Complete ===
echo.
echo Markdown Viewer has been installed to:
echo !INSTALL_FOLDER!
echo.
echo You can now:
echo   - Launch from Start Menu
echo   - Double-click .md files to open
echo.
pause
