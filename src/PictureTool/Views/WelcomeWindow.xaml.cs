using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PictureTool.Models;
using PictureTool.Services;

namespace PictureTool.Views;

public partial class WelcomeWindow : Window
{
    private readonly bool _isReplayMode;
    private int _pageIndex;

    public WelcomeWindow(HotkeySettings hotkeys, bool isReplayMode = false)
    {
        _isReplayMode = isReplayMode;
        InitializeComponent();
        CaptureHotkeyText.Text = hotkeys.CaptureArea.ToString();
        PasteHotkeyText.Text = hotkeys.PasteImage.ToString();
        AppIconHelper.ApplyWindowIcon(this);

        if (_isReplayMode)
        {
            Title = "使用指引";
            HeaderTitleText.Text = "使用指引";
            HeaderSubtitleText.Text = "Picture Tool 功能与操作说明";
            SkipButton.Visibility = Visibility.Collapsed;
            DontShowAgainCheckBox.Visibility = Visibility.Collapsed;
        }

        ShowPage(0);
    }

    public bool DontShowAgain => DontShowAgainCheckBox.IsChecked == true;

    private void ShowPage(int index)
    {
        _pageIndex = index;
        PageWelcome.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageQuickStart.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageFeatures.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        PageTips.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;

        BackButton.Visibility = index > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = index == 3 ? (_isReplayMode ? "关闭" : "开始使用") : "下一步";
        SkipButton.Visibility = _isReplayMode || index == 3 ? Visibility.Collapsed : Visibility.Visible;

        UpdateDots(index);
    }

    private void UpdateDots(int activeIndex)
    {
        SetDot(Dot0, activeIndex == 0);
        SetDot(Dot1, activeIndex == 1);
        SetDot(Dot2, activeIndex == 2);
        SetDot(Dot3, activeIndex == 3);
    }

    private static void SetDot(Ellipse dot, bool active)
    {
        dot.Fill = new SolidColorBrush(active
            ? System.Windows.Media.Color.FromRgb(0x25, 0x63, 0xEB)
            : System.Windows.Media.Color.FromRgb(0xD0, 0xD5, 0xDD));
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_pageIndex > 0)
        {
            ShowPage(_pageIndex - 1);
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_pageIndex < 3)
        {
            ShowPage(_pageIndex + 1);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
