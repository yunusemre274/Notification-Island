[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdkDirectory = Join-Path $projectRoot 'artifacts\dotnet'
$dotnet = Join-Path $sdkDirectory 'dotnet.exe'
$setupDirectory = Join-Path $projectRoot 'artifacts\setup'

New-Item -ItemType Directory -Force -Path $setupDirectory | Out-Null

if (-not (Test-Path -LiteralPath $dotnet)) {
    $installer = Join-Path $setupDirectory 'dotnet-install.ps1'
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Write-Host 'Downloading the official Microsoft .NET installer...'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Channel 8.0 -Architecture x64 -InstallDir $sdkDirectory -NoPath
    if ($LASTEXITCODE -ne 0) { throw '.NET SDK installation failed.' }
}

$env:DOTNET_ROOT = $sdkDirectory
$env:DOTNET_ROOT_X64 = $sdkDirectory
$env:PATH = "$sdkDirectory;$env:PATH"
$env:DOTNET_CLI_HOME = Join-Path $projectRoot 'artifacts\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot 'artifacts\nuget\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $projectRoot 'artifacts\nuget\http-cache'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

Push-Location $projectRoot
try {
    & $dotnet --info
    if ($LASTEXITCODE -ne 0) { throw 'The local .NET SDK could not be started.' }

    & $dotnet restore NI.sln
    if ($LASTEXITCODE -ne 0) { throw 'NuGet dependency restore failed. Check internet access and the output above.' }

    & $dotnet build NI.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'The Release build failed. See the output above.' }

    Write-Host 'Setup complete. Start the application with:'
    Write-Host 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start.ps1'
}
finally {
    Pop-Location
}
