param(
    [string]$ProjectPath = "$PSScriptRoot\..\src\PictureTool\PictureTool.csproj",
    [string]$ManifestTemplate = "$PSScriptRoot\..\src\PictureTool.Package\Package.appxmanifest",
    [string]$IconPath = "$PSScriptRoot\..\src\PictureTool\Assets\app.ico",
    [string]$OutputDir = "$PSScriptRoot\..\publish\msix",
    [string]$Publisher = $(if ($env:MSIX_PUBLISHER) { $env:MSIX_PUBLISHER } else { "CN=Picture Tool" })
)

$ErrorActionPreference = "Stop"

function Get-DotNetExe {
    if ($env:DOTNET_EXE -and (Test-Path $env:DOTNET_EXE)) {
        return $env:DOTNET_EXE
    }

    $found = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($found) {
        return $found.Source
    }

    return "C:\Program Files\dotnet\dotnet.exe"
}

$DotNetExe = Get-DotNetExe

function Get-ProjectVersion {
    param([string]$Path)
    [xml]$project = Get-Content $Path
    return $project.Project.PropertyGroup.Version
}

function ConvertTo-MsixVersion {
    param([string]$Version)
    $parts = $Version.Split('.')
    while ($parts.Count -lt 4) { $parts += '0' }
    return ($parts[0..3] -join '.')
}

function New-LogoAssets {
    param(
        [string]$SourceIcon,
        [string]$AssetsDir
    )

    if (-not (Test-Path $AssetsDir)) {
        New-Item -ItemType Directory -Path $AssetsDir | Out-Null
    }

    Add-Type -AssemblyName System.Drawing
    $icon = [System.Drawing.Icon]::new($SourceIcon)
    $sizes = @{
        "StoreLogo.png" = 50
        "Square44x44Logo.png" = 44
        "Square150x150Logo.png" = 150
    }

    foreach ($entry in $sizes.GetEnumerator()) {
        $bitmap = New-Object System.Drawing.Bitmap $entry.Value, $entry.Value
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.Clear([System.Drawing.Color]::FromArgb(255, 37, 99, 235))
        $iconSize = [Math]::Min($entry.Value - 8, 64)
        $x = [int](($entry.Value - $iconSize) / 2)
        $y = [int](($entry.Value - $iconSize) / 2)
        $graphics.DrawIcon($icon, (New-Object System.Drawing.Rectangle $x, $y, $iconSize, $iconSize))
        $graphics.Dispose()
        $target = Join-Path $AssetsDir $entry.Key
        $bitmap.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
    }

    $icon.Dispose()
}

function Find-MakeAppx {
    $candidate = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $candidate) {
        throw "makeappx.exe not found. Install Windows SDK."
    }
    return $candidate.FullName
}

$version = Get-ProjectVersion -Path $ProjectPath
$msixVersion = ConvertTo-MsixVersion -Version $version
$layoutDir = Join-Path $OutputDir "layout"
$assetsDir = Join-Path $layoutDir "Assets"
$msixName = "PictureTool_${msixVersion}_x64.msix"
$msixPath = Join-Path $OutputDir $msixName

if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Path $layoutDir, $assetsDir | Out-Null

Write-Host "Publishing app layout..." -ForegroundColor Cyan
& $DotNetExe publish $ProjectPath `
    -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=false `
    -o $layoutDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "Generating MSIX assets..." -ForegroundColor Cyan
New-LogoAssets -SourceIcon $IconPath -AssetsDir $assetsDir

$manifest = Get-Content $ManifestTemplate -Raw
$manifest = $manifest.Replace("__PUBLISHER__", [System.Security.SecurityElement]::Escape($Publisher))
$manifest = $manifest.Replace("__VERSION__", $msixVersion)
Set-Content -Path (Join-Path $layoutDir "AppxManifest.xml") -Value $manifest -Encoding UTF8

Write-Host "Creating MSIX package..." -ForegroundColor Cyan
$makeappx = Find-MakeAppx
& $makeappx pack /d $layoutDir /p $msixPath /o /l
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed" }

Write-Host "MSIX created: $msixPath" -ForegroundColor Green
Write-Output $msixPath
