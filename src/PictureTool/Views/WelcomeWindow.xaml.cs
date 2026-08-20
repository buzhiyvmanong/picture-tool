using System.Windows;
using PictureTool.Models;

namespace PictureTool.Views;

public partial class WelcomeWindow : Window
{
    public WelcomeWindow(HotkeySettings hotkeys)
    {
        InitializeComponent();
        CaptureHotkeyRun.Text = hotkeys.CaptureArea.ToString();
        PasteHotkeyRun.Text = hotkeys.PasteImage.ToString();

        var iconUri = Services.AppIconHelper.GetWindowIconUri();
        if (iconUri is not null)
        {
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
