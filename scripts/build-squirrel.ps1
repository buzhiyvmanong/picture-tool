param(
    [string]$ProjectPath = "$PSScriptRoot\..\src\PictureTool\PictureTool.csproj",
    [string]$OutputDir = "$PSScriptRoot\..\publish\squirrel",
    [string]$ToolsDir = "$PSScriptRoot\..\.tools\squirrel"
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

function Get-SquirrelExe {
    param([string]$DestinationDir)

    $squirrelExe = Join-Path $DestinationDir "Squirrel.exe"
    if (Test-Path $squirrelExe) {
        return $squirrelExe
    }

    New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
    $zipUrl = "https://github.com/clowd/Clowd.Squirrel/releases/download/2.11.1/SquirrelTools-2.11.1.zip"
    $zipPath = Join-Path $DestinationDir "SquirrelTools.zip"

    Write-Host "Downloading Squirrel tools from GitHub..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath
    Expand-Archive -Path $zipPath -DestinationPath $DestinationDir -Force

    if (-not (Test-Path $squirrelExe)) {
        $found = Get-ChildItem $DestinationDir -Recurse -Filter Squirrel.exe | Select-Object -First 1
        if (-not $found) {
            throw "Squirrel.exe not found after extracting SquirrelTools."
        }
        return $found.FullName
    }

    return $squirrelExe
}

function Get-ProjectVersion {
    param([string]$Path)
    [xml]$project = Get-Content $Path
    return $project.Project.PropertyGroup.Version
}

$DotNetExe = Get-DotNetExe
$version = Get-ProjectVersion -Path $ProjectPath
$appDir = Join-Path $OutputDir "app"
$releaseDir = Join-Path $OutputDir "releases"
$iconPath = Join-Path $PSScriptRoot "..\src\PictureTool\Assets\app.ico"

if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Path $appDir, $releaseDir | Out-Null

Write-Host "Publishing Squirrel app layout..." -ForegroundColor Cyan
& $DotNetExe build $ProjectPath -c Release -r win-x64 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

& $DotNetExe publish $ProjectPath `
    -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=false `
    -o $appDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Sidecar marker required by Clowd.Squirrel when validating Squirrel-aware binaries.
New-Item -ItemType File -Path (Join-Path $appDir "PictureTool.exe.squirrel") -Force | Out-Null

$squirrelExe = Get-SquirrelExe -DestinationDir $ToolsDir

Write-Host "Packing Squirrel release..." -ForegroundColor Cyan
$packArgs = @(
    "pack",
    "--packId", "PictureTool",
    "--packVersion", $version,
    "--packDir", $appDir,
    "--releaseDir", $releaseDir,
    "--mainExe", "PictureTool.exe",
    "--packAuthors", "Picture Tool",
    "--allowUnaware"
)

if (Test-Path $iconPath) {
    $packArgs += @("--icon", $iconPath)
}

& $squirrelExe @packArgs
if ($LASTEXITCODE -ne 0) { throw "squirrel pack failed" }

Write-Host "Squirrel release created in $releaseDir" -ForegroundColor Green
Get-ChildItem $releaseDir | Format-Table Name, Length
