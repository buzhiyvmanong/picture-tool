# 当前开发环境

检查日期：2026-08-18

## 已具备

- Node.js：已安装
- npm：已安装
- winget：已安装
- .NET SDK：10.0.400，已安装
- Microsoft.WindowsDesktop.App Runtime：10.0.11，已安装

## 暂未具备

- Rust / Cargo：未安装
- C++ 编译器：未安装

## 已尝试

尝试将 .NET SDK 安装到项目本地 `.tools/dotnet`，避免系统级安装。

结果：

- `Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1` 失败
- `curl.exe -L https://dot.net/v1/dotnet-install.ps1` 失败
- 错误表现为连接关闭或连接重置

当前已经可以执行：

```powershell
dotnet build .\src\PictureTool\PictureTool.csproj
dotnet run --project .\src\PictureTool\PictureTool.csproj
```

## 当前验证

已完成：

```powershell
dotnet build .\src\PictureTool\PictureTool.csproj
```

结果：

- 编译成功
- 0 个错误
- 0 个警告
- 已启动 `PictureTool.exe` 做基础运行检查

## 下一步

下一步可以开始手动验证桌面能力：

- 主窗口是否正常显示
- 托盘菜单是否正常显示
- `Ctrl + Shift + A` 是否能触发区域截图
- `Ctrl + Shift + V` 是否能读取剪贴板图片
- 快捷键设置是否能保存并重新生效
