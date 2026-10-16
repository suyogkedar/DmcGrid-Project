@echo off
rem Starts DMCGrid on http://localhost:5273 and opens it in the browser.
cd /d "%~dp0src\DmcGrid.Web"
start "" http://localhost:5273
dotnet run --launch-profile http
