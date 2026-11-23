param([Parameter(Mandatory=$true)][string]$Release,[Parameter(Mandatory=$true)][string]$Installation)
$ErrorActionPreference='Stop'
python "$PSScriptRoot/release.py" build --release $Release --installation $Installation
if($LASTEXITCODE){throw 'GOS Meet build or configuration validation failed.'}
