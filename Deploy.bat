@echo off
echo Building ClaudeWatcher (Release)...
dotnet publish src\ClaudeWatcher\ClaudeWatcher.csproj -c Release -o "%LOCALAPPDATA%\ClaudeWatcher"
if %ERRORLEVEL% NEQ 0 (
    echo BUILD FAILED
    pause
    exit /b 1
)
echo.
echo Deployed to: %LOCALAPPDATA%\ClaudeWatcher\ClaudeWatcher.exe
echo.
pause
