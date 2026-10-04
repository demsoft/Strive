<#
 Windows Firewall rules for the media ports of Strive (run in an elevated PowerShell). Ports 80/443 are IIS's and
 already open. Read the ports from src\.env.production. Re-running replaces the rules.

 Do not forget the firewall of the hosting provider, if there is one: the same ports, UDP and TCP.
#>
$ErrorActionPreference = 'Stop'
$env = Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'src\.env.production'
$vars = @{}; Get-Content $env | ForEach-Object { if ($_ -match '^([A-Z0-9_]+)=(.*)$') { $vars[$Matches[1]] = $Matches[2] } }
$range = "$($vars.MEDIASOUP_MIN_PORT)-$($vars.MEDIASOUP_MAX_PORT)"

Get-NetFirewallRule -DisplayName 'Strive media*' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
foreach ($protocol in 'UDP', 'TCP') {
   New-NetFirewallRule -DisplayName "Strive media ($protocol)" -Direction Inbound -Protocol $protocol -LocalPort $range -Action Allow -Profile Any | Out-Null
}
Write-Host "Allowed inbound UDP and TCP $range (Strive media)."
