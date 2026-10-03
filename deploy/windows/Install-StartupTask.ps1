<#
 Starts the stack when Windows boots (run in an elevated PowerShell). Docker has to be running first, so the task waits
 for the docker engine. The containers have restart: unless-stopped, this covers a cold start.
#>
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'strive.ps1'
$wait = @"
`$deadline = (Get-Date).AddMinutes(15)
while ((Get-Date) -lt `$deadline) { docker info *> `$null; if (`$LASTEXITCODE -eq 0) { break }; Start-Sleep -Seconds 10 }
& '$script' start
"@
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -Command `"$($wait -replace '"','\"' -replace "`r?`n",'; ')`""
$trigger = New-ScheduledTaskTrigger -AtStartup -RandomDelay (New-TimeSpan -Seconds 60)
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName 'Strive start' -Action $action -Trigger $trigger -Principal $principal -Force | Out-Null
Write-Host "Task 'Strive start' installed. Test it with a reboot at a quiet moment."
