@echo off
:: Abrir puerto TCP 5249 para el Servidor (si se instala el server)
netsh advfirewall firewall add rule name="CyberControl Server" dir=in action=allow protocol=TCP localport=5249

:: Permitir al Cliente comunicarse por UDP (Auto-descubrimiento)
netsh advfirewall firewall add rule name="CyberControl Discovery" dir=in action=allow protocol=UDP localport=8888

:: Permitir que el ejecutable del cliente salga a internet sin preguntar
netsh advfirewall firewall add rule name="CyberControl Client App" dir=in action=allow program="%~dp0PcControl.Client.exe" enable=yes