# WPF 实现方案

## 选型结论

当前产品路线确定为：

- 第一实现：WPF
- 备选现代化：WinUI / Windows App SDK
- 不作为正式路线：Electron

微软文档里 WinUI 3 是新的 Windows 桌面 UI 推荐框架，但它依赖 Windows App SDK；WPF 对截图工具这种系统窗口控制型应用更直接。因此当前先走 WPF，后续如果需要更现代的控件外观，再评估局部引入 Windows App SDK。

## 版本建议

项目暂定目标框架：

- `net10.0-windows`

原因：

- .NET 10 是当前 LTS
- WPF 仍然是 .NET Windows 桌面应用的一等支持方向
- 目标只面向 Windows，适合直接使用 Windows 桌面能力

如果机器或团队暂时只装了 .NET 8，也可以把 `TargetFramework` 改成 `net8.0-windows`，主体架构不用变。

## 模块划分

```text
src/PictureTool
  App.xaml
  Infrastructure
    AppCoordinator.cs
    FileCleanup.cs
  Models
    AnnotationCommand.cs
    ScreenshotFrame.cs
  Services
    BitmapLoader.cs
    ClipboardImageService.cs
    HotkeyService.cs
    ScreenshotService.cs
    SettingsService.cs
    TrayService.cs
  Views
    MainWindow.xaml
    CaptureOverlayWindow.xaml
    AnnotationWindow.xaml
    PinWindow.xaml
    SettingsWindow.xaml
```

## 核心流程

### 主窗口生命周期

主窗口右上角关闭按钮只隐藏到托盘，不销毁窗口对象。

原因：

- WPF 窗口一旦 `Close()` 后不能再次 `Show()`
- 截图工具需要托盘常驻
- 真正退出只通过托盘菜单的“退出”触发
- 启动后默认隐藏主窗口，只保留托盘和全局快捷键

### 区域截图

1. 全局快捷键触发
2. 截取虚拟屏幕到临时 PNG
3. 打开全屏透明遮罩窗口
4. 用户拖选区域
5. 裁剪临时图片
6. 打开标注窗口
7. 删除未使用的全屏临时图

### 粘贴图片

1. 从剪贴板读取图片
2. 写入临时 PNG
3. 打开标注窗口

### 快捷键设置

默认快捷键：

- 区域截图：`Ctrl + Shift + A`
- 粘贴图片：`Ctrl + Shift + V`

用户可以在主窗口或托盘菜单打开“快捷键设置”。

配置保存到：

```text
%APPDATA%\PictureTool\settings.json
```

注册快捷键时会检查：

- 是否包含 `Ctrl`、`Alt`、`Shift` 或 `Win`
- 是否有非修饰键
- 两个功能是否重复
- 是否被系统或其他应用占用

### 标注

当前骨架先支持：

- 画笔
- 矩形
- 橡皮擦
- 撤销
- 清空标注
- 复制
- 保存
- 贴到屏幕

后续补齐：

- 箭头
- 文本
- 编号
- 马赛克
- 重做
- 裁剪

标注窗口使用图片的 WPF 显示尺寸布置画布，避免高 DPI 截图在标注区出现右侧或底部空白。

标注层使用 `InkCanvas` 笔迹集合：

- 画笔生成可编辑笔迹
- 矩形会转成闭合笔迹
- 橡皮擦使用按点擦除，只擦掉经过的笔迹片段，不删除整条标注
- 撤销通过恢复上一份笔迹集合实现

### 贴图

贴图窗口是一个轻量无边框置顶窗口：

- `Topmost=true`
- `ShowInTaskbar=false`
- `WindowStyle=None`
- `AllowsTransparency=true`
- 按图片的 WPF 自然显示尺寸初始化，避免高 DPI 图片只显示左上角
- 图片区域可直接拖动移动
- 大图会等比缩小到屏幕工作区内
- 贴图窗口不支持自由拉伸，避免比例失真和内容裁切
- 从标注窗口贴图时按源图片像素和 DPI 渲染，避免高 DPI 截图变糊
- 贴图、复制、保存使用离屏合成，不对正在显示的标注 UI 调用 `Measure` / `Arrange`，避免内容偏移
- 托盘菜单支持显示所有贴图和关闭所有贴图

后续补齐：

- 透明度
- 鼠标穿透
- 右键菜单
- 边缘缩放体验优化
- 双击回到标注编辑

## 低内存原则

- 常驻只保留主窗口、托盘和快捷键
- 大图不转 base64
- 截图文件放临时目录
- 标注尽量保存矢量命令，不保存每一步图片快照
- 贴图窗口关闭时释放 `BitmapImage`
- 滚动截图后续必须做分段拼接和及时释放
