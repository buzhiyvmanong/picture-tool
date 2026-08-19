using System.Windows;
using System.Windows.Input;
using WpfClipboard = System.Windows.Clipboard;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PictureTool.Views;

public partial class OcrResultWindow : Window
{
    public OcrResultWindow(string text)
    {
        InitializeComponent();
        ResultTextBox.Text = text;
        ResultTextBox.SelectAll();
        ResultTextBox.Focus();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        CopyText();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OcrResultWindow_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void CopyText()
    {
        if (!string.IsNullOrEmpty(ResultTextBox.Text))
        {
            WpfClipboard.SetText(ResultTextBox.Text);
        }
    }
}
