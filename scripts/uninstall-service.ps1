#Requires -RunAsAdministrator
<#
  Stops and removes the Windows service and its binaries.
  State and logs in %ProgramData%\RealEstateCrawler are left in place.
#>
param(
    [string]$InstallDir = (Join-Path $env:ProgramFiles 'RealEstateCrawler')
)

$ErrorActionPreference = 'Stop'
$serviceName = 'RealEstateCrawler'

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    & sc.exe delete $serviceName | Out-Null
}

if (Test-Path (Join-Path $InstallDir 'RealEstateCrawler.exe')) {
    Remove-Item -Recurse -Force $InstallDir
}

Write-Host "Removed. State and logs remain in $(Join-Path $env:ProgramData 'RealEstateCrawler')"
