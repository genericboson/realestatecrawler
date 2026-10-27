#Requires -RunAsAdministrator
<#
  Publishes the crawler and registers it as an auto-start Windows service.
  Running it again updates the binaries and restarts the service.
  State and logs live in %ProgramData%\RealEstateCrawler and are kept.
#>
param(
    [string]$InstallDir = (Join-Path $env:ProgramFiles 'RealEstateCrawler')
)

$ErrorActionPreference = 'Stop'
$serviceName = 'RealEstateCrawler'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\RealEstateCrawler\RealEstateCrawler.csproj'
$localSettings = Join-Path $repoRoot 'src\RealEstateCrawler\appsettings.Local.json'

if (-not (Test-Path $localSettings)) {
    throw "Missing $localSettings. Copy appsettings.Local.example.json to it and fill in the phone number and SOLAPI keys."
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -ne 'Stopped') {
    Write-Host "Stopping $serviceName ..."
    Stop-Service -Name $serviceName -Force
    $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}

Write-Host "Publishing to $InstallDir ..."
& dotnet publish $project -c Release -o $InstallDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

$exe = Join-Path $InstallDir 'RealEstateCrawler.exe'
if (-not $service) {
    New-Service -Name $serviceName -BinaryPathName "`"$exe`"" -DisplayName 'RealEstate Crawler' `
        -Description 'Hourly Dabang/Zigbang monthly-rent crawler that texts new listings' `
        -StartupType Automatic | Out-Null
}

# Restart the process if it ever dies
& sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

Start-Service -Name $serviceName
Get-Service -Name $serviceName | Format-Table -AutoSize Name, Status, StartType
Write-Host "Logs: $(Join-Path $env:ProgramData 'RealEstateCrawler\logs')"
