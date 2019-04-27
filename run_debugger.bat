@echo off
if exist "Octovisor.Debugger/bin/Release" (
	cd Octovisor.Debugger/bin/Release
	start Octovisor.Debugger.exe
)