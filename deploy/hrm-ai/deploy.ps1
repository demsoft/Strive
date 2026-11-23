param([Parameter(Mandatory=$true)][string]$Release,[Parameter(Mandatory=$true)][string]$Installation)
$ErrorActionPreference='Stop'
# Existing IIS terminates TLS; this script does not change IIS or any HRM configuration.
python "$PSScriptRoot/release.py" up --release $Release --installation $Installation
if($LASTEXITCODE){throw 'GOS Meet deployment/readiness failed. Keep HRM flags disabled.'}
