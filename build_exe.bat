@echo off
cd /d %~dp0
echo [1/2] Installing required packages...
python -m pip install --upgrade python-osc pystray pillow pyinstaller
if errorlevel 1 goto error
echo [2/2] Building NeroMoon.exe ...
python -m PyInstaller --onefile --noconsole --name NeroMoon nero_moon_app.py
if errorlevel 1 goto error
echo.
echo ==============================================
echo  Done!  dist\NeroMoon.exe was created.
echo ==============================================
explorer dist
pause
exit /b 0
:error
echo.
echo Something went wrong. Please send a screenshot of this window.
pause
exit /b 1
