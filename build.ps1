# PictureTool 一键构建脚本
# 用法: .\build.ps1
# 可选: .\build.ps1 -SkipInstaller  (跳过安装包生成)

param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "=== 1. 发布 Release 版本 ===" -ForegroundColor Cyan
$publishDir = Join-Path $root "publish\win-x64"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

& dotnet publish "$root\src\PictureTool\PictureTool.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { Write-Error "发布失败"; exit 1 }

$exe = Get-Item "$publishDir\PictureTool.exe"
Write-Host "发布完成: $($exe.FullName) ($([math]::Round($exe.Length/1MB,1)) MB)" -ForegroundColor Green

if ($SkipInstaller) {
    Write-Host "已跳过安装包生成。" -ForegroundColor Yellow
    exit 0
}

Write-Host "`n=== 2. 生成安装包 ===" -ForegroundColor Cyan
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    Write-Host "未找到 Inno Setup 6，请从 https://jrsoftware.org/isdl.php 安装。" -ForegroundColor Yellow
    Write-Host "安装后重新运行此脚本即可生成安装包。" -ForegroundColor Yellow
    exit 0
}

& $iscc "$root\installer\PictureTool.iss"
if ($LASTEXITCODE -ne 0) { Write-Error "安装包生成失败"; exit 1 }

$setup = Get-ChildItem "$root\publish\installer\*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "安装包生成完成: $($setup.FullName) ($([math]::Round($setup.Length/1MB,1)) MB)" -ForegroundColor Green
