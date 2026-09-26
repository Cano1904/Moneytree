@echo off
rem Double-click: builds GLASSCORE (tests, dedicated server, Unity game) and shuts the PC down afterwards.
rem Cancel a pending shutdown with:  shutdown /a
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_and_shutdown.ps1" %*
