@echo off
rem Builds GLASSCORE without shutting the PC down.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_and_shutdown.ps1" -NoShutdown %*
pause
