@echo off
echo === Usual Intake Analysis Web App - Setup ===
echo.

:: Try python, py, python3 in order
set PYTHON=
python --version >nul 2>&1 && set PYTHON=python
if not defined PYTHON (py --version >nul 2>&1 && set PYTHON=py)
if not defined PYTHON (python3 --version >nul 2>&1 && set PYTHON=python3)
if not defined PYTHON goto nopython

echo Python found: %PYTHON%
%PYTHON% --version

node --version >nul 2>&1
if errorlevel 1 goto nonode
echo Node.js found:
node --version

echo.
echo [1/2] Installing Python packages...
cd /d "%~dp0backend"
%PYTHON% -m pip install -r requirements.txt
if errorlevel 1 goto pipfail

echo.
echo [2/2] Installing Node packages...
cd /d "%~dp0frontend"
npm install
if errorlevel 1 goto npmfail

:: Save detected python command for run.bat
echo %PYTHON% > "%~dp0python_cmd.txt"

echo.
echo Setup complete! Run run.bat to start.
pause
exit /b 0

:nopython
echo ERROR: Python not found. Install from https://python.org (check Add to PATH)
pause
exit /b 1

:nonode
echo ERROR: Node.js not found. Install from https://nodejs.org
pause
exit /b 1

:pipfail
echo ERROR: pip install failed.
pause
exit /b 1

:npmfail
echo ERROR: npm install failed.
pause
exit /b 1
