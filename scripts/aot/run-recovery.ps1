param(
    [Parameter(Mandatory)][string]$Executable,
    [string]$Container = 'rednb-nacos-324-test',
    [string]$DockerImage,
    [string]$DockerNetwork = 'rednb-nacos-test_default',
    [string]$OutputDirectory = 'artifacts/aot-recovery'
)
$ErrorActionPreference = 'Stop'
if ($Container -notmatch '^rednb-nacos-[a-z0-9-]+$') { throw 'Only isolated RedNb test containers may be restarted.' }
if ($env:NACOS_TEST_SERVER -and $env:NACOS_TEST_SERVER -notmatch '^(localhost|127\.0\.0\.1):') { throw 'Recovery driver requires a local server.' }
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$directory = (Resolve-Path $OutputDirectory).Path
$signal = Join-Path $directory ([Guid]::NewGuid().ToString('N'))
$env:NACOS_RECOVERY_SIGNAL = $signal
$start = @{ FilePath = (Resolve-Path $Executable).Path; ArgumentList = @('--live','--recovery'); PassThru = $true
    RedirectStandardOutput = (Join-Path $directory 'recovery.log'); RedirectStandardError = (Join-Path $directory 'recovery.err') }
$dockerName = $null
if ($DockerImage) {
    if ($DockerImage -notmatch '^[a-zA-Z0-9./:_-]+$' -or $DockerNetwork -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid Docker image or network name' }
    $dockerName = 'rednb-nacos-aot-recovery-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
    $binaryDirectory = Split-Path $start.FilePath
    $binaryName = Split-Path $start.FilePath -Leaf
    $start.FilePath = 'docker'
    $start.ArgumentList = @('run', '--rm', '--name', $dockerName, '--network', $DockerNetwork,
        '--mount', "`"type=bind,source=$binaryDirectory,target=/app,readonly`"",
        '--mount', "`"type=bind,source=$directory,target=/signals`"",
        '-e', "NACOS_TEST_SERVER=${Container}:8848", '-e', "NACOS_TEST_CONSOLE=${Container}:8080",
        '-e', 'NACOS_TEST_USERNAME=nacos', '-e', 'NACOS_TEST_PASSWORD=nacos',
        '-e', "NACOS_RECOVERY_SIGNAL=/signals/$(Split-Path $signal -Leaf)",
        $DockerImage, "/app/$binaryName", '--live', '--recovery')
}
if ($IsWindows) { $start.WindowStyle = 'Hidden' }
$process = Start-Process @start
try {
    $deadline = [DateTime]::UtcNow.AddMinutes(5)
    while (!(Test-Path "$signal.ready")) {
        if ($process.HasExited) { throw "Recovery executable exited early: $($process.ExitCode)" }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Recovery readiness timeout' }
        Start-Sleep -Milliseconds 500
    }
    docker restart $Container | Out-Null
    if ($LASTEXITCODE) { throw 'Container restart failed' }
    $ready = $false
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        try { Invoke-WebRequest 'http://localhost:8080/v3/console/health/readiness' -TimeoutSec 2 | Out-Null; $ready = $true; break } catch { Start-Sleep -Seconds 2 }
    }
    if (!$ready) { throw 'Server did not become ready' }
    Set-Content "$signal.restarted" 'restarted'
    if (!$process.WaitForExit(180000)) { throw 'Recovery executable timeout' }
    if ($process.ExitCode -ne 0) { throw "Recovery failed: $($process.ExitCode)" }
    if (!(Select-String -Path $start.RedirectStandardOutput -SimpleMatch -Pattern 'PASS idle config/naming/MCP recovery')) { throw 'Recovery checks did not execute' }
} finally {
    if (!$process.HasExited) { $process.Kill($true) }
    if ($dockerName -and (docker ps -aq --filter "name=^/$dockerName$")) { docker rm -f $dockerName | Out-Null }
    Remove-Item Env:NACOS_RECOVERY_SIGNAL
}
