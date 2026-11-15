@echo off
cd /d "%~dp0"
echo This allows only Finance Tracker on TCP 5142 from the local network.
echo Windows will ask for administrator permission.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Allow-Phone-Access.ps1"
if exist "Phone-Access-Result.txt" type "Phone-Access-Result.txt"
pause
