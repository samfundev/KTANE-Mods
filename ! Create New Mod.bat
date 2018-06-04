@echo off
title Create New Mod
set /p modname="Enter Mod Name: "
robocopy "! Template Project" "%modname%" /E /sl