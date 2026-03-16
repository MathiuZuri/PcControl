param([string]$ExePath)

$TaskName = "CyberControlClientAutoStart"

# Como tu código C# ya es inteligente y espera a la Barra de Tareas, 
# lanzamos la tarea inmediatamente al iniciar sesión.
$Trigger = New-ScheduledTaskTrigger -AtLogOn

# Es CRUCIAL usar "BUILTIN\Administrators" y no "SYSTEM".
# Si usas SYSTEM, tu función FindWindow("Shell_TrayWnd") fallará 
# porque SYSTEM no tiene "Barra de Tareas" visual.
$Principal = New-ScheduledTaskPrincipal -GroupId "BUILTIN\Administrators" -RunLevel Highest

$Action = New-ScheduledTaskAction -Execute $ExePath

# Configuraciones para asegurar que arranque siempre (incluso en laptops con batería)
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 0) -Priority 1

# Limpieza por si existía una versión vieja
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue

# Registrar la nueva tarea optimizada
Register-ScheduledTask -TaskName $TaskName -Trigger $Trigger -Principal $Principal -Action $Action -Settings $Settings -Force