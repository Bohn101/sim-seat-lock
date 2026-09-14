@echo off
cd /d "%~dp0publish"
if not exist SimSeatLock.Pose.exe (
  echo Missing publish\SimSeatLock.Pose.exe
  echo Run: dotnet publish src\SimSeatLock.Pose\SimSeatLock.Pose.csproj -c Release -r win-x64 --self-contained false -o publish
  pause
  exit /b 1
)
start "" SimSeatLock.Pose.exe
