@echo off
title Guardian PcControl

:loop
:: 1. VERIFICAR SI EL ADMIN PIDIÓ APAGAR
:: Si existe este archivo, el guardián se cierra a sí mismo y termina.
if exist "apagar_guardian.tmp" (
    goto fin
)

:: 2. VERIFICAR SI EL CLIENTE CORRE
tasklist /FI "IMAGENAME eq PcControl.Client.exe" 2>NUL | find /I /N "PcControl.Client.exe">NUL
if "%ERRORLEVEL%"=="1" (
    echo El cliente se cerro. Reiniciando...
    start "" "PcControl.Client.exe"
)

timeout /t 2 /nobreak >nul
goto loop

:fin
exit