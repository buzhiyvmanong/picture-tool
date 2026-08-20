# 代码签名

Windows SmartScreen 会对未签名的 exe 显示「未知发布者」警告。购买代码签名证书后，可在发布时签名。

## 前置条件

1. 购买 **Authenticode** 代码签名证书（如 DigiCert、Sectigo）
2. 安装 Windows SDK，确保 `signtool.exe` 可用（通常在 `C:\Program Files (x86)\Windows Kits\10\bin\<version>\x64\signtool.exe`）

## 本地签名发布

```powershell
# 使用 PFX 证书文件
.\build.ps1 -Sign -CertificatePath "C:\certs\picturetool.pfx" -CertificatePassword "your-password"

# 使用证书存储中的 thumbprint
.\build.ps1 -Sign -CertificateThumbprint "ABCDEF1234567890..."
```

## GitHub Actions 签名（可选）

在仓库 Secrets 中配置：

- `SIGNING_CERT_BASE64` — PFX 文件的 Base64
- `SIGNING_CERT_PASSWORD` — PFX 密码

然后在 `release.yml` 的 Publish 步骤后增加 Sign 步骤（需自行启用）。
