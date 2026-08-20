param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$MsixFileName,
    [string]$Repository = "buzhiyvmanong/picture-tool",
    [string]$Publisher = $(if ($env:MSIX_PUBLISHER) { $env:MSIX_PUBLISHER } else { "CN=Picture Tool" }),
    [string]$OutputPath = "$PSScriptRoot\..\publish\msix\PictureTool.appinstaller"
)

$ErrorActionPreference = "Stop"

function ConvertTo-MsixVersion {
    param([string]$InputVersion)
    $parts = $InputVersion.TrimStart('v').Split('.')
    while ($parts.Count -lt 4) { $parts += '0' }
    return ($parts[0..3] -join '.')
}

$tag = if ($Version.StartsWith('v')) { $Version } else { "v$Version" }
$msixVersion = ConvertTo-MsixVersion -Version $Version
$baseUri = "https://github.com/$Repository/releases/download/$tag"
$appInstallerUri = "$baseUri/PictureTool.appinstaller"
$msixUri = "$baseUri/$MsixFileName"

$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<AppInstaller
  Uri="$appInstallerUri"
  Version="$msixVersion"
  xmlns="http://schemas.microsoft.com/appx/appinstaller/2017/2">
  <MainPackage
    Name="Buzhiyvmanong.PictureTool"
    Publisher="$([System.Security.SecurityElement]::Escape($Publisher))"
    Version="$msixVersion"
    Uri="$msixUri"
    ProcessorArchitecture="x64" />
  <UpdateSettings>
    <OnLaunch HoursBetweenUpdateChecks="24" ShowPrompt="true" />
  </UpdateSettings>
</AppInstaller>
"@

$directory = Split-Path $OutputPath -Parent
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

Set-Content -Path $OutputPath -Value $xml -Encoding UTF8
Write-Host "AppInstaller created: $OutputPath" -ForegroundColor Green
