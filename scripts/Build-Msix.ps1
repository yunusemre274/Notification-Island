[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')][string]$Architecture = 'x64',
    [string]$Publisher = 'CN=NotificationIsland',
    [string]$CertificateThumbprint,
    [string]$WindowsSdkBin
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $WindowsSdkBin) {
    $sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $sdkVersion = Get-ChildItem $sdkRoot -Directory | Where-Object { $_.Name -match '^10\.0\.' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if (-not $sdkVersion) { throw 'Windows 10/11 SDK (MakeAppx, SignTool) gerekli.' }
    $WindowsSdkBin = Join-Path $sdkVersion.FullName 'x64'
}
$stage = Join-Path $repo "artifacts\msix-$Architecture"
$output = Join-Path $repo "artifacts\NotificationIsland-$Architecture.msix"
New-Item -ItemType Directory -Force -Path $stage | Out-Null
# Prevent stale files from previous publish configurations entering the package.
Get-ChildItem $stage -Force | Remove-Item -Recurse -Force
dotnet publish (Join-Path $repo 'NI.csproj') -c Release -r "win-$Architecture" --self-contained true -o $stage
if ($LASTEXITCODE -ne 0) { throw 'Publish başarısız.' }
[xml]$manifest = Get-Content (Join-Path $repo 'Packaging\Package.appxmanifest') -Raw
$manifest.Package.Identity.ProcessorArchitecture = $Architecture
$manifest.Package.Identity.Publisher = $Publisher
$manifest.Save((Join-Path $stage 'AppxManifest.xml'))
Copy-Item (Join-Path $repo 'Packaging\Assets') $stage -Recurse -Force
& (Join-Path $WindowsSdkBin 'makeappx.exe') pack /d $stage /p $output /o
if ($LASTEXITCODE -ne 0) { throw 'MakeAppx başarısız.' }
if ($CertificateThumbprint) {
    & (Join-Path $WindowsSdkBin 'signtool.exe') sign /fd SHA256 /sha1 $CertificateThumbprint $output
    if ($LASTEXITCODE -ne 0) { throw 'İmzalama başarısız. Sertifika subject ve Publisher eşleşmeli.' }
} else {
    Write-Warning 'Paket imzalanmadı. Kurulum öncesi güvenilen ve Publisher ile eşleşen bir sertifikayla imzalayın.'
}
Write-Output $output
