using System.Drawing;
using Forms = System.Windows.Forms;

namespace PictureTool.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayService(
        Action showDashboard,
        Action captureArea,
        Action scrollCapture,
        Action pasteImage,
        Action extractClipboardText,
        Action openSettings,
        Action showAllPins,
        Action closeAllPins,
        Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开面板", null, (_, _) => showDashboard());
        menu.Items.Add("截图", null, (_, _) => captureArea());
        menu.Items.Add("滚动截图", null, (_, _) => scrollCapture());
        menu.Items.Add("贴图", null, (_, _) => pasteImage());
        menu.Items.Add("提取剪贴板文字", null, (_, _) => extractClipboardText());
        menu.Items.Add("快捷键设置", null, (_, _) => openSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("显示所有贴图", null, (_, _) => showAllPins());
        menu.Items.Add("关闭所有贴图", null, (_, _) => closeAllPins());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        _icon = new Forms.NotifyIcon
        {
            Icon = AppIconHelper.GetTrayIcon(),
            Text = "Picture Tool",
            ContextMenuStrip = menu,
            Visible = true
        };

        _icon.DoubleClick += (_, _) => showDashboard();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
