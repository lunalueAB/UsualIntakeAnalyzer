@echo off
echo === Usual Intake Analysis Web App - Starting ===
echo.

:: Detect python command
set PYTHON=
if exist "%~dp0python_cmd.txt" set /p PYTHON=<"%~dp0python_cmd.txt"

:: Trim spaces
if defined PYTHON set PYTHON=%PYTHON: =%

:: Fallback: detect manually
if not defined PYTHON (python --version >nul 2>&1 && set PYTHON=python)
if not defined PYTHON (py --version >nul 2>&1 && set PYTHON=py)
if not defined PYTHON (python3 --version >nul 2>&1 && set PYTHON=python3)
if not defined PYTHON goto nopython

echo Using Python: %PYTHON%
%PYTHON% --version

node --version >nul 2>&1
if errorlevel 1 goto nonode

echo Starting backend (http://localhost:8000)...
start "Backend (FastAPI)" cmd /k "cd /d "%~dp0backend" && %PYTHON% -m uvicorn main:app --reload --port 8000"

echo Waiting 5 seconds...
timeout /t 5 /nobreak > nul

echo Starting frontend (http://localhost:3000)...
start "Frontend (React)" cmd /k "cd /d "%~dp0frontend" && npm run dev"

echo Waiting 5 seconds...
timeout /t 5 /nobreak > nul

start http://localhost:3000
echo App running at http://localhost:3000
exit /b 0

:nopython
echo ERROR: Python not found. Install from https://python.org
pause
exit /b 1

:nonode
echo ERROR: Node.js not found. Install from https://nodejs.org
pause
exit /b 1
