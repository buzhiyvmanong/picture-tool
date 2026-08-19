param(
    [string]$Scenario = "tray-idle",
    [string]$OutputPath = ".local/memory-baseline/memory-baseline.csv"
)

$ErrorActionPreference = "Stop"

function Convert-ToMb([long]$Bytes) {
    return [Math]::Round($Bytes / 1MB, 2)
}

$processes = @(Get-Process PictureTool -ErrorAction SilentlyContinue)
$tempDir = Join-Path $env:TEMP "PictureTool"
$tempFiles = @()

if (Test-Path $tempDir) {
    $tempFiles = @(Get-ChildItem -Path $tempDir -Filter "*.png" -File -ErrorAction SilentlyContinue)
}

$row = [PSCustomObject]@{
    Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    Scenario = $Scenario
    ProcessCount = $processes.Count
    ProcessIds = ($processes | ForEach-Object { $_.Id }) -join ";"
    WorkingSetMB = Convert-ToMb (($processes | Measure-Object -Property WorkingSet64 -Sum).Sum)
    PrivateMemoryMB = Convert-ToMb (($processes | Measure-Object -Property PrivateMemorySize64 -Sum).Sum)
    PagedMemoryMB = Convert-ToMb (($processes | Measure-Object -Property PagedMemorySize64 -Sum).Sum)
    HandleCount = (($processes | Measure-Object -Property HandleCount -Sum).Sum)
    ThreadCount = (($processes | ForEach-Object { $_.Threads.Count } | Measure-Object -Sum).Sum)
    TempPngCount = $tempFiles.Count
    TempPngMB = Convert-ToMb (($tempFiles | Measure-Object -Property Length -Sum).Sum)
}

$directory = Split-Path -Parent $OutputPath
if ($directory) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

if (Test-Path $OutputPath) {
    $row | Export-Csv -Path $OutputPath -Append -NoTypeInformation -Encoding UTF8
} else {
    $row | Export-Csv -Path $OutputPath -NoTypeInformation -Encoding UTF8
}

$row | Format-List

