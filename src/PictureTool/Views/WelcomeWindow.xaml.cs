using System.Windows;
using PictureTool.Models;
using PictureTool.Services;

namespace PictureTool.Views;

public partial class WelcomeWindow : Window
{
    public WelcomeWindow(HotkeySettings hotkeys)
    {
        InitializeComponent();
        CaptureHotkeyRun.Text = hotkeys.CaptureArea.ToString();
        PasteHotkeyRun.Text = hotkeys.PasteImage.ToString();
        AppIconHelper.ApplyWindowIcon(this);
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
