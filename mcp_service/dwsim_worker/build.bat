@echo off
REM Build script for DwsimWorker (.NET Framework 4.8)
"D:\Apps\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" DwsimWorker.sln /p:Configuration=Debug /t:Restore,Build
