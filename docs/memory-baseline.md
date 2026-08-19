# 内存基线

## 目标

先记录当前原型的内存表现，再决定是否优化。

重点看三个场景：

1. 只常驻托盘
2. 打开一个标注窗口
3. 同时贴 3 张图

## 测量命令

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\measure-memory.ps1 -Scenario tray-idle
powershell -ExecutionPolicy Bypass -File .\scripts\measure-memory.ps1 -Scenario annotation-open
powershell -ExecutionPolicy Bypass -File .\scripts\measure-memory.ps1 -Scenario three-pins
```

结果会追加写入：

```text
.local/memory-baseline/memory-baseline.csv
```

## 当前基线

检查日期：2026-08-18

已测场景：

- `tray-idle`

当前观测值：

- 进程数：1
- 工作集：172.45MB
- 私有内存：106.11MB
- 句柄数：947
- 线程数：10
- 临时 PNG 文件：31 个，约 1.36MB

解释：

- 对 WPF 原型来说可以接受
- 对低内存截图工具来说仍偏高
- 后续应优先降低常驻状态的窗口和图片资源占用

## 后续判断标准

短期目标：

- 常驻托盘私有内存低于 90MB
- 截图和标注窗口关闭后内存能明显回落
- 贴图关闭后对应图片资源能释放

中期目标：

- 常驻托盘私有内存低于 70MB
- 多贴图场景增长可控
- 临时图片可自动清理

## 下一次测量步骤

### 标注窗口

1. 运行区域截图
2. 选取一块中等大小区域
3. 保持标注窗口打开
4. 执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\measure-memory.ps1 -Scenario annotation-open
```

### 三张贴图

1. 连续创建 3 张贴图
2. 保持 3 张贴图都在屏幕上
3. 执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\measure-memory.ps1 -Scenario three-pins
```

## 临时图片清理策略

- 临时图片统一写入 `%TEMP%\PictureTool`。
- 启动时只清理 6 小时以前的 PNG 残留，避免误删仍在使用的当次图片。
- 截图遮罩关闭时释放整屏截图引用。
- 标注窗口关闭时释放图片引用、清空标注笔迹，并删除它打开的临时图片。
- 贴图窗口关闭时释放图片引用，并删除贴图对应的临时图片。
