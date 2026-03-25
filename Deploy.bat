@echo off
echo Building ClaudeWatcher (Release)...
dotnet publish src\ClaudeWatcher\ClaudeWatcher.csproj -c Release -o "%LOCALAPPDATA%\ClaudeWatcher"
if %ERRORLEVEL% NEQ 0 (
    echo BUILD FAILED
    exit /b 1
)

echo Copying hook scripts...
xcopy /Y /I scripts\hooks\*.sh "%LOCALAPPDATA%\ClaudeWatcher\hooks\" >nul
if %ERRORLEVEL% NEQ 0 (
    echo COPY FAILED
    exit /b 1
)

echo.
echo Deployed to: %LOCALAPPDATA%\ClaudeWatcher
echo   exe:   %LOCALAPPDATA%\ClaudeWatcher\ClaudeWatcher.exe
echo   hooks: %LOCALAPPDATA%\ClaudeWatcher\hooks\
echo.
