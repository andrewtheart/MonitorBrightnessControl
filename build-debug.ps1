Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$requiredMajor = 10
$requiredMinor = 0
$requiredPatch = 300

function Test-DotNetSdk {
    try {
        $sdks = & dotnet --list-sdks 2>$null
    } catch {
        return $false
    }
    foreach ($line in $sdks) {
        if ($line -match '^(\d+)\.(\d+)\.(\d+)') {
            $major = [int]$Matches[1]
            $minor = [int]$Matches[2]
            $patch = [int]$Matches[3]
            if ($major -gt $requiredMajor) { return $true }
            if ($major -eq $requiredMajor -and $minor -gt $requiredMinor) { return $true }
            if ($major -eq $requiredMajor -and $minor -eq $requiredMinor -and $patch -ge $requiredPatch) { return $true }
        }
    }
    return $false
}

if (-not (Test-DotNetSdk)) {
    Write-Host ".NET SDK $requiredMajor.$requiredMinor.$requiredPatch or newer is required but was not found." -ForegroundColor Yellow
    $answer = Read-Host "Install it now via winget? (y/n)"
    if ($answer -eq 'y') {
        winget install Microsoft.DotNet.SDK.10
        if ($LASTEXITCODE -ne 0) {
            Write-Host "winget install failed." -ForegroundColor Red
            exit 1
        }
        # Refresh PATH so the new SDK is visible
        $env:PATH = [System.Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' +
                     [System.Environment]::GetEnvironmentVariable('PATH', 'User')
    } else {
        Write-Host "Cannot build without the required SDK." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Building in Debug mode..." -ForegroundColor Cyan
dotnet build "$PSScriptRoot\MonitorBrightness\MonitorBrightness.csproj" -c Debug
exit $LASTEXITCODE
