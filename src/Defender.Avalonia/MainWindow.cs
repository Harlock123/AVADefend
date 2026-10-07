using Avalonia.Controls;
using Avalonia.Media;

namespace Defender.Avalonia;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Defender (1981 recreation — validation build)";
        Width = 292 * 3;
        Height = 240 * 3;
        Background = Brushes.Black;
        Content = new ValidationView();
    }
}
