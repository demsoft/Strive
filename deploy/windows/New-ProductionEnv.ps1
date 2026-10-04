<#
 Creates src\.env.production from src\.env.production.example with fresh random secrets.
 It never overwrites an existing file (the secrets in it protect the running system).

   .\New-ProductionEnv.ps1 -Domain goserp.co.uk -PublicIp 173.208.144.83
#>
param(
   [Parameter(Mandatory)] [string] $Domain,
   [Parameter(Mandatory)] [string] $PublicIp,
   [string] $AppHost = "meet.$Domain",
   [int] $MediaWorkers = 4,
   [int] $MediaPort = 40000
)
$ErrorActionPreference = 'Stop'
$src = Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'src'
$target = Join-Path $src '.env.production'
if (Test-Path $target) { throw "$target exists already. Delete it yourself if you really want new secrets (that signs everybody out and breaks recordings in progress)." }

function New-Secret([int] $bytes = 32) {
   $b = New-Object byte[] $bytes
   [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
   -join ($b | ForEach-Object { $_.ToString('x2') })
}

$values = @{
   SITE_HOST = $Domain
   FRONTEND_DNS_OR_IP = $AppHost
   ANNOUNCED_IP = $PublicIp
   TURN_HOST = "turn.$Domain"
   SMTP_FROM = "Strive <no-reply@$Domain>"
   STRIVE_TOKEN_SECRET = New-Secret
   STRIVE_API_KEY = New-Secret
   TURN_SECRET = New-Secret
   RECORDER_SHARED_SECRET = New-Secret
   RECORDER_TOKEN_SECRET = New-Secret 48
   MEDIASOUP_MIN_PORT = $MediaPort
   MEDIASOUP_MAX_PORT = $MediaPort + $MediaWorkers - 1
   MEDIASOUP_NUM_WORKERS = $MediaWorkers
}

$lines = Get-Content (Join-Path $src '.env.production.example') | ForEach-Object {
   if ($_ -match '^([A-Z0-9_]+)=') {
      $key = $Matches[1]
      if ($values.ContainsKey($key)) { return "$key=$($values[$key])" }
   }
   $_
}
# UTF-8 without BOM: docker compose does not like a byte order mark in the first variable name
[IO.File]::WriteAllLines($target, $lines, (New-Object Text.UTF8Encoding $false))
Write-Host "Created $target"
Write-Host "Now fill in: GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET, BREVO_API_KEY, SMTP_FROM (and the RECORDING_* values to use recording)."
