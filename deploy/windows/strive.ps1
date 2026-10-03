<#
 Everyday commands for the production stack (Docker Desktop).

   .\strive.ps1 up        build and start (or update after a git pull)
   .\strive.ps1 down      stop
   .\strive.ps1 ps        status
   .\strive.ps1 logs [service]
   .\strive.ps1 restart [service]
#>
param(
   [Parameter(Position = 0)] [ValidateSet('up', 'down', 'ps', 'logs', 'restart', 'start')] [string] $Command = 'ps',
   [Parameter(Position = 1, ValueFromRemainingArguments)] [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot)
$src = Join-Path $repo 'src'
$envFile = Join-Path $src '.env.production'
if (-not (Test-Path $envFile)) { throw "$envFile is missing. Run New-ProductionEnv.ps1 first." }

$vars = @{}; Get-Content $envFile | ForEach-Object { if ($_ -match '^([A-Z0-9_]+)=(.*)$') { $vars[$Matches[1]] = $Matches[2] } }

Set-Location $src
if (Get-Command git -ErrorAction SilentlyContinue) {
   $env:GITCOMMIT = (git rev-parse --short HEAD)
   $env:GITREF = (git log -1 --pretty=format:'%D')
   $env:GITTIMESTAMP = (git log -1 --pretty=format:'%ai')
}

$compose = @('compose', '--env-file', '.env.production',
   '-f', 'docker-compose.yml', '-f', 'docker-compose.override.yml', '-f', 'docker-compose.production.yml')
if ($vars.RECORDING_ENABLED -eq 'true') { $compose = @('compose', '--profile', 'recording') + $compose[1..($compose.Length - 1)] }

switch ($Command) {
   'up'      { & docker @compose up -d --build --remove-orphans @Rest }
   'start'   { & docker @compose up -d @Rest }   # without building, used at boot
   'down'    { & docker @compose down @Rest }
   'ps'      { & docker @compose ps @Rest }
   'logs'    { & docker @compose logs --tail 200 -f @Rest }
   'restart' { & docker @compose restart @Rest }
}
