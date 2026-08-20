# PictureTool 一键构建脚本
# 用法:
#   .\build.ps1
#   .\build.ps1 -BuildSquirrel -PackageMsix
#   .\build.ps1 -Sign -CertificatePath "cert.pfx" -CertificatePassword "pwd"

param(
    [switch]$BuildSquirrel,
    [switch]$PackageMsix,
    [switch]$Sign,
    [string]$CertificatePath,
    [string]$CertificatePassword,
    [string]$CertificateThumbprint
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

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

Write-Host "=== 发布 PictureTool ===" -ForegroundColor Cyan

$publishDir = Join-Path $root "publish\win-x64"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

& $DotNetExe publish "$root\src\PictureTool\PictureTool.csproj" `
    -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { Write-Error "发布失败"; exit 1 }

$exe = Get-Item "$publishDir\PictureTool.exe"
Write-Host "`n便携 EXE 发布完成: $($exe.FullName) ($([math]::Round($exe.Length/1MB,1)) MB)" -ForegroundColor Green

$signFiles = @($exe.FullName)

if ($BuildSquirrel) {
    & "$root\scripts\build-squirrel.ps1"
    if ($LASTEXITCODE -ne 0) { Write-Error "Squirrel 打包失败"; exit 1 }
    Get-ChildItem "$root\publish\squirrel\releases" -Include *.exe,*.nupkg -Recurse | ForEach-Object {
        $signFiles += $_.FullName
    }
}

if ($PackageMsix) {
    if (-not $env:MSIX_PUBLISHER) { $env:MSIX_PUBLISHER = "CN=Picture Tool" }
    $msixPath = & "$root\scripts\build-msix.ps1" | Select-Object -Last 1
    if ($LASTEXITCODE -ne 0) { Write-Error "MSIX 打包失败"; exit 1 }

    [xml]$project = Get-Content "$root\src\PictureTool\PictureTool.csproj"
    $version = $project.Project.PropertyGroup.Version
    $parts = $version.Split('.')
    while ($parts.Count -lt 4) { $parts += '0' }
    $msixVersion = ($parts[0..3] -join '.')
    $msixFile = "PictureTool_${msixVersion}_x64.msix"
    & "$root\scripts\generate-appinstaller.ps1" -Version $version -MsixFileName $msixFile
    $signFiles += (Resolve-Path $msixPath).Path
}

if ($Sign) {
    & "$root\scripts\sign-artifacts.ps1" `
        -Files $signFiles `
        -CertificatePath $CertificatePath `
        -CertificatePassword $CertificatePassword `
        -CertificateThumbprint $CertificateThumbprint
    if ($LASTEXITCODE -ne 0) { Write-Error "签名失败"; exit 1 }
}

Write-Host "`n产物目录:" -ForegroundColor Cyan
Write-Host "  publish\win-x64\PictureTool.exe"
if ($BuildSquirrel) { Write-Host "  publish\squirrel\releases\" }
if ($PackageMsix) { Write-Host "  publish\msix\" }
Write-Host "`n运行前需安装 .NET 10 桌面运行时: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
