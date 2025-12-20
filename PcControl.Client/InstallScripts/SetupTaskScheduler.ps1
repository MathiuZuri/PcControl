param([string]$ExePath)

$TaskName = "CyberControlClientAutoStart"
$Trigger = New-ScheduledTaskTrigger -AtLogOn
$User = "SYSTEM" # Se ejecuta como SYSTEM para que sea difícil de matar
$Action = New-ScheduledTaskAction -Execute $ExePath
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 0)

# Borrar tarea si ya existe para evitar duplicados
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue

# Crear la nueva tarea con privilegios máximos
Register-ScheduledTask -TaskName $TaskName -Trigger $Trigger -User $User -Action $Action -Settings $Settings -RunLevel Highest -Force