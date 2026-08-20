# PictureTool 一键构建脚本
# 用法: .\build.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "=== 发布 PictureTool ===" -ForegroundColor Cyan
$publishDir = Join-Path $root "publish\win-x64"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

& dotnet publish "$root\src\PictureTool\PictureTool.csproj" `
    -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { Write-Error "发布失败"; exit 1 }

$exe = Get-Item "$publishDir\PictureTool.exe"
Write-Host "`n发布完成: $($exe.FullName) ($([math]::Round($exe.Length/1MB,1)) MB)" -ForegroundColor Green
Write-Host "运行前需安装 .NET 10 桌面运行时: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
