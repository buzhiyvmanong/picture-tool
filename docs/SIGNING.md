# 代码签名

Windows SmartScreen 会对未签名的 exe 显示「未知发布者」警告。购买代码签名证书后，可在本地或 CI 发布时签名。

## 前置条件

1. 购买 **Authenticode** 代码签名证书（如 DigiCert、Sectigo）
2. 安装 Windows SDK，确保 `signtool.exe` 与 `makeappx.exe` 可用
3. MSIX 的 `Publisher` 必须与证书主题一致

## 本地签名发布

```powershell
# 便携 EXE
.\build.ps1 -Sign -CertificatePath "C:\certs\picturetool.pfx" -CertificatePassword "your-password"

# 同时构建 Squirrel + MSIX 并签名
.\build.ps1 -BuildSquirrel -PackageMsix -Sign -CertificatePath "C:\certs\picturetool.pfx" -CertificatePassword "your-password"

# 使用证书存储中的 thumbprint
.\build.ps1 -Sign -CertificateThumbprint "ABCDEF1234567890..."
```

## GitHub Actions 自动签名

在仓库 **Settings → Secrets and variables → Actions** 中配置：

| Secret | 说明 |
|--------|------|
| `SIGNING_CERT_BASE64` | PFX 文件的 Base64 编码 |
| `SIGNING_CERT_PASSWORD` | PFX 密码 |
| `MSIX_PUBLISHER` | MSIX Publisher，需与证书主题一致，例如 `CN=Your Company Name` |

推送 `v*` 标签后，Release 工作流会：

1. 构建并测试
2. 产出便携 EXE、Squirrel 包（`RELEASES` + `nupkg` + Setup）、MSIX、AppInstaller
3. 使用 Secrets 中的证书签名 EXE / MSIX / Squirrel 工件
4. 上传到 GitHub Release

### 生成 Base64 证书

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\certs\picturetool.pfx")) | Set-Clipboard
```

### 验证签名

CI 会在签名后执行 `signtool verify /pa`。本地可手动验证：

```powershell
& "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe" verify /pa publish\win-x64\PictureTool.exe
```

## MSIX Publisher 注意事项

- `Package.appxmanifest` 中的 `Publisher` 由 `MSIX_PUBLISHER` 注入
- 若 Publisher 与签名证书不匹配，MSIX 无法安装或无法升级
- 更换证书时需同步更新 `MSIX_PUBLISHER`，并保持 Identity Name 不变

## 常见问题

| 问题 | 处理方式 |
|------|----------|
| SmartScreen 仍警告 | 新证书需时间积累信誉；确保使用时间戳服务器 |
| MSIX 安装失败 | 检查 Publisher 是否与证书 CN 一致 |
| Squirrel 更新失败 | 确认 Release 中包含 `RELEASES` 与对应 `nupkg` |
| AppInstaller 不更新 | 确认 `.appinstaller` 与 `.msix` 在同一 Release 且 URI 正确 |
