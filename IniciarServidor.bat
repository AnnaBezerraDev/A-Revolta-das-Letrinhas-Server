@echo off
title Servidor .NET - A Revolta das Letrinhas
cd /d "%~dp0RevoltaLetrinhas.Api"
echo ========================================================
echo   Iniciando Servidor .NET - A Revolta das Letrinhas
echo ========================================================
echo.
echo Abra no seu navegador: http://localhost:5000
echo.
dotnet run --urls "http://localhost:5000"
pause
