@echo off
taskkill /F /IM VoiceR.exe
dotnet build
exit /b