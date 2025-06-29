@echo off
setlocal

:: Set the title of the command window
title GGUFDump Context Menu Setup

:: === Configuration ===
:: The name of the executable file. Must be in the same directory as this script.
set "EXECUTABLE=GGUFDump.exe"
:: The internal name for the file type. Can be anything unique.
set "FILETYPE_ID=GGUFFile"
:: The text that will appear in the right-click menu.
set "MENU_TEXT=Dump Info with GGUFDump"
:: The internal name for the context menu command (verb).
set "VERB=dumpgguf"
:: =====================

:: Get the directory where this script is located. %~dp0 includes a trailing backslash.
set "SCRIPT_DIR=%~dp0"
set "EXE_PATH=%SCRIPT_DIR%%EXECUTABLE%"

:: Check for the /remove argument for uninstallation
if /i "%~1" == "/remove" goto :uninstall

:: --- INSTALLATION ---
:install
echo.
echo  ===========================================
echo   GGUFDump Context Menu Installer
echo  ===========================================
echo.

:: Check if the executable file actually exists
if not exist "%EXE_PATH%" (
    echo [ERROR] Could not find "%EXECUTABLE%" in the current directory.
    echo         Please make sure "%EXECUTABLE%" is in the same folder as this script.
    goto :end
)

echo  This script will add a right-click menu option for .gguf files.
echo  It will only be installed for the current user (%USERNAME%).
echo.
echo  Executable Path: %EXE_PATH%
echo  Menu Text:       %MENU_TEXT%
echo.
pause

echo.
echo  Creating registry keys...

:: 1. Associate .gguf extension with our custom file type ID
::    Key:   HKCU\Software\Classes\.gguf
::    Value: (Default) = GGUFFile
reg add "HKCU\Software\Classes\.gguf" /v "" /d "%FILETYPE_ID%" /f >nul
if %errorlevel% neq 0 (goto :reg_error)

:: 2. Create the file type ID and give it a description
::    Key:   HKCU\Software\Classes\GGUFFile
::    Value: (Default) = GGUF Model File
reg add "HKCU\Software\Classes\%FILETYPE_ID%" /v "" /d "GGUF Model File" /f >nul
if %errorlevel% neq 0 (goto :reg_error)

:: 3. Create the shell verb (the action) and set the menu text
::    Key:   HKCU\Software\Classes\GGUFFile\shell\dumpgguf
::    Value: (Default) = Dump Info with GGUFDump
reg add "HKCU\Software\Classes\%FILETYPE_ID%\shell\%VERB%" /v "" /d "%MENU_TEXT%" /f >nul
if %errorlevel% neq 0 (goto :reg_error)

:: 4. (Optional) Set the icon for the menu item to be the one from the .exe
::    Key:   HKCU\Software\Classes\GGUFFile\shell\dumpgguf
::    Value: Icon = "C:\Path\To\GGUFDump.exe",0
reg add "HKCU\Software\Classes\%FILETYPE_ID%\shell\%VERB%" /v "Icon" /d "\"%EXE_PATH%\",0" /f >nul

:: 5. Set the command to execute. This is the most important part.
::    %1 is the placeholder for the file path that was right-clicked.
::    Quotes around "%EXE_PATH%" and "%1" are critical to handle paths with spaces.
::    The inner quotes must be escaped with a backslash for the reg command.
::    Key:   HKCU\Software\Classes\GGUFFile\shell\dumpgguf\command
::    Value: (Default) = "C:\Path\To\GGUFDump.exe" "%1"
reg add "HKCU\Software\Classes\%FILETYPE_ID%\shell\%VERB%\command" /v "" /d "\"%EXE_PATH%\" \"%%1\"" /f >nul
if %errorlevel% neq 0 (goto :reg_error)

echo.
echo  [SUCCESS] The context menu has been installed!
echo  You can now right-click any .gguf file to dump its info.
goto :end


:: --- UNINSTALLATION ---
:uninstall
echo.
echo  =============================================
echo   GGUFDump Context Menu Uninstaller
echo  =============================================
echo.
echo  This will remove the GGUFDump context menu for the current user.
echo.
pause

echo.
echo  Deleting registry keys...

:: To uninstall, we just delete the keys we created.
:: Deleting the file type ID will remove its shell commands automatically.
:: The .gguf key might be used by other programs, so we only remove our filetype association from it
:: if it's still pointing to our ID. A safer approach is just to delete our own custom key.
reg delete "HKCU\Software\Classes\%FILETYPE_ID%" /f >nul 2>nul
reg delete "HKCU\Software\Classes\.gguf" /f >nul 2>nul

echo.
echo  [SUCCESS] The context menu has been removed.
goto :end


:: --- ERROR HANDLING ---
:reg_error
echo.
echo  [FATAL ERROR] Failed to write to the registry.
echo  The script did not complete successfully.
goto :end


:: --- SCRIPT END ---
:end
echo.
echo  Press any key to exit.
pause >nul
endlocal
exit /b