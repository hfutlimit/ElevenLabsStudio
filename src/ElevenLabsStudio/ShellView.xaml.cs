using System.Windows;

namespace ElevenLabsStudio;

/// <summary>
/// Code-behind for <c>ShellView.xaml</c>. Per project MVVM rules, only
/// calls <c>InitializeComponent()</c>; no logic, no event handlers, no
/// service access.
/// </summary>
public partial class ShellView : Window
{
    public ShellView()
    {
        InitializeComponent();
    }
}