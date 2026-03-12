@echo off
setlocal

cd /d "%~dp0"

python --version >nul 2>nul
if %errorlevel%==0 (
    python apply_updates.py
    goto after_run
)

py --version >nul 2>nul
if %errorlevel%==0 (
    py apply_updates.py
    goto after_run
)

echo [ERROR] Python not found
pause
exit /b 1

:after_run
if errorlevel 1 (
    echo.
    echo [ERROR] Update failed
    pause
    exit /b 1
)

echo.
echo [OK] Update completed successfully
pause