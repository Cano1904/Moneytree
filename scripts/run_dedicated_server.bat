@echo off
rem Starts the headless dedicated server built by build_and_shutdown.ps1 (public lobby, auto-start when all READY).
rem Options: --port 27015 --code AB3K9Z --map 0..2 --max-players 2..8 --max-score 5..25 --private --teams --competitive
"%~dp0..\Build\Server\GlasscoreServer.exe" %*
pause
