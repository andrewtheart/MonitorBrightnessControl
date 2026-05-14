@echo off
cd /d "%~dp0"
dotnet run --project MonitorBrightness -c Release -- %*
