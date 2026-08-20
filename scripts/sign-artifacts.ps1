param(
    [Parameter(Mandatory = $true)]
    [string[]]$Files,
    [string]$CertificatePath,
    [string]$CertificatePassword,
    [string]$CertificateThumbprint
)

$ErrorActionPreference = "Stop"

function Find-SignTool {
    $candidate = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $candidate) {
        throw "signtool.exe not found. Install Windows SDK."
    }
    return $candidate.FullName
}

if (-not $CertificatePath -and -not $CertificateThumbprint) {
    Write-Host "No signing certificate configured. Skipping signing." -ForegroundColor Yellow
    return
}

$signtool = Find-SignTool
$signArgs = @("sign", "/fd", "SHA256", "/tr", "http://timestamp.digicert.com", "/td", "SHA256")

if ($CertificatePath) {
    if ($CertificatePassword) {
        $signArgs += @("/f", $CertificatePath, "/p", $CertificatePassword)
    } else {
        $signArgs += @("/f", $CertificatePath)
    }
} else {
    $signArgs += @("/sha1", $CertificateThumbprint)
}

foreach ($file in $Files) {
    if (-not (Test-Path $file)) {
        Write-Warning "Skip missing file: $file"
        continue
    }

    Write-Host "Signing $file" -ForegroundColor Cyan
    & $signtool @signArgs $file
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $file" }

    & $signtool verify /pa $file
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $file" }
}

Write-Host "Signing completed." -ForegroundColor Green
