@echo off
:: 1. Abrir puerto TCP 5249 para recibir conexiones de los clientes
netsh advfirewall firewall add rule name="CyberControl Server Port" dir=in action=allow protocol=TCP localport=5249

:: 2. Permitir que el ejecutable escuche en la red
netsh advfirewall firewall add rule name="CyberControl Server App" dir=in action=allow program="%~dp0PcControl.server.exe" enable=yes

:: 3. (Opcional) Abrir puerto UDP 8888 para el auto-descubrimiento
netsh advfirewall firewall add rule name="CyberControl Discovery Server" dir=in action=allow protocol=UDP localport=8888