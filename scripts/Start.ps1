[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdkDirectory = Join-Path $projectRoot 'artifacts\dotnet'
$dotnet = Join-Path $sdkDirectory 'dotnet.exe'

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw 'First run: powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Setup.ps1'
}

$env:DOTNET_ROOT = $sdkDirectory
$env:DOTNET_ROOT_X64 = $sdkDirectory
$env:PATH = "$sdkDirectory;$env:PATH"
$env:DOTNET_CLI_HOME = Join-Path $projectRoot 'artifacts\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot 'artifacts\nuget\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $projectRoot 'artifacts\nuget\http-cache'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:NI_LOG_DIRECTORY = Join-Path $projectRoot 'artifacts\logs'

Push-Location $projectRoot
try {
    & $dotnet build NI.csproj --configuration Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. If dependencies changed or setup was incomplete, run scripts\Setup.ps1 first.' }
    $applicationStartedAt = Get-Date
    & $dotnet run --project NI.csproj --configuration Release --no-build --no-restore
    if ($LASTEXITCODE -ne 0) {
        $applicationExitCode = $LASTEXITCODE
        $crashLog = Join-Path $env:NI_LOG_DIRECTORY 'crash.log'
        if ((Test-Path -LiteralPath $crashLog) -and (Get-Item -LiteralPath $crashLog).LastWriteTime -ge $applicationStartedAt) {
            Get-Content -LiteralPath $crashLog -Tail 40 | Write-Host
        }
        throw "Application exited with code $applicationExitCode. Crash log: $crashLog"
    }
}
finally {
    Pop-Location
}
