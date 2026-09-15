# Compatibility entry point. Requires an isolated Nacos 3.2.4 server.
param(
    [ValidateSet('net8.0','net10.0')][string]$Framework = 'net10.0',
    [ValidateSet('win-x64','linux-x64')][string]$Rid = 'win-x64'
)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/aot/validate.ps1" -Framework $Framework -Rid $Rid -App Aot
