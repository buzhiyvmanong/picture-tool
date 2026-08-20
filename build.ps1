# PictureTool 一键构建脚本
# 用法: .\build.ps1
# 签名: .\build.ps1 -Sign -CertificatePath "cert.pfx" -CertificatePassword "pwd"

param(
    [switch]$Sign,
    [string]$CertificatePath,
    [string]$CertificatePassword,
    [string]$CertificateThumbprint
)

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

if ($Sign) {
    $signtool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1

    if (-not $signtool) {
        Write-Error "未找到 signtool.exe，请安装 Windows SDK。"
    }

    $signArgs = @("sign", "/fd", "SHA256", "/tr", "http://timestamp.digicert.com", "/td", "SHA256")
    if ($CertificatePath) {
        if ($CertificatePassword) {
            $signArgs += @("/f", $CertificatePath, "/p", $CertificatePassword)
        } else {
            $signArgs += @("/f", $CertificatePath)
        }
    } elseif ($CertificateThumbprint) {
        $signArgs += @("/sha1", $CertificateThumbprint)
    } else {
        Write-Error "签名需要 -CertificatePath 或 -CertificateThumbprint"
    }

    & $signtool.FullName @signArgs $exe.FullName
    if ($LASTEXITCODE -ne 0) { Write-Error "签名失败"; exit 1 }
    Write-Host "签名完成。" -ForegroundColor Green
}

Write-Host "运行前需安装 .NET 10 桌面运行时: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
