# NativeAOT smoke: publish the AOT sample and run its self-checks.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# The AOT targets locate MSVC's link.exe via vswhere; ensure it is on PATH
# when launched from a non-developer shell. No-op where it does not exist.
$vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (Test-Path (Join-Path $vsInstaller 'vswhere.exe')) {
    $env:PATH = "$vsInstaller;$env:PATH"
}

dotnet publish "$root/samples/RedNb.Nacos.Sample.Aot/RedNb.Nacos.Sample.Aot.csproj" -c Release -r win-x64 --self-contained
if ($LASTEXITCODE -ne 0) {
    Write-Error "publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

& "$root/samples/RedNb.Nacos.Sample.Aot/bin/Release/net10.0/win-x64/publish/RedNb.Nacos.Sample.Aot.exe"
exit $LASTEXITCODE
