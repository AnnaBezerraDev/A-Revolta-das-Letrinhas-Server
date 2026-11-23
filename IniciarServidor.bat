@echo off
chcp 65001 >nul
title Servidor .NET - A Revolta das Letrinhas
cd /d "%~dp0RevoltaLetrinhas.Api"
echo ========================================================
echo   Iniciando Servidor .NET - A Revolta das Letrinhas
echo ========================================================
echo.
echo Abra no seu navegador: http://localhost:5000
echo.
echo IMPORTANTE: o jogo grava os dados na NUVEM (api.kairosstudios.site).
echo O painel aberto em http://localhost:5000 mostra a NUVEM por padrao,
echo entao o que for cadastrado no jogo aparece aqui.
echo Para ver o banco de testes deste computador, use o seletor
echo "Mostrar dados de" no canto superior direito do painel.
echo.
echo O painel da nuvem tambem pode ser aberto direto em:
echo   https://api.kairosstudios.site
echo.
dotnet run --urls "http://localhost:5000"
pause
