[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [ValidateSet('cold-start','compact-idle','media','expanded','notifications','cycles-100','ai-recovery','large-fixture','alarm','leak')][string]$Scenario = 'compact-idle',
 [int]$Seconds = 660,
 [ValidateRange(50,1000)][int]$SampleMs = 100,
 [string]$OutputDirectory,
 [string]$TaskStatePath = "$env:LOCALAPPDATA\NotificationIsland\agent\last-task.json"
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo ('artifacts\benchmark-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$Executable = (Resolve-Path $Executable).Path
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
# Read-only test instrumentation; no app settings, OS permissions or environment variables are changed.
$cpu = Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors
$system = Get-CimInstance Win32_ComputerSystem | Select-Object TotalPhysicalMemory
$gpu = Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion,VideoProcessor,CurrentHorizontalResolution,CurrentVerticalResolution
$commit = & git -C $repo rev-parse HEAD
$dirty = [bool](& git -C $repo status --porcelain)
$metadata = [ordered]@{
 commit=$commit; dirtyTree=$dirty; configuration='Release'; debuggerAttached='Not automatically verified; run without debugger'
 windows=[Environment]::OSVersion.VersionString; architecture=$env:PROCESSOR_ARCHITECTURE
 cpu=$cpu; systemMemory=$system; gpu=$gpu
 resolution='See video controller data; verify actual per-monitor resolution manually'
 dpiScaling='Record actual per-monitor scaling manually'
 renderMode='Record WPF hardware tier/fallback and screen-recording result manually'
 activeFeatures='Record enabled Appearance sections, media/notification/alarm state manually'
 model='Record selected model, quantization, num_ctx and server ownership manually'
 scenario=$Scenario; sampleMs=$SampleMs; requestedSeconds=$Seconds; budgetBytes=20000000
}
$metadata | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $OutputDirectory 'environment.json') -Encoding UTF8
$instrumentation = Join-Path $repo 'artifacts\benchmark-instrumentation'
dotnet publish (Join-Path $repo 'Benchmarks\Collector\Collector.csproj') -c Release -o $instrumentation
if ($LASTEXITCODE -ne 0) { throw 'Collector publish başarısız.' }
$collectorArgs = @('--exe',$Executable,'--scenario',$Scenario,'--seconds',"$Seconds",'--sample-ms',"$SampleMs",'--out',$OutputDirectory,'--build',"$commit dirty=$dirty",'--task-state',$TaskStatePath)
& dotnet (Join-Path $instrumentation 'Collector.dll') @collectorArgs
if ($LASTEXITCODE -ne 0) { throw "Ölçüm çalıştırılamadı: $LASTEXITCODE" }
