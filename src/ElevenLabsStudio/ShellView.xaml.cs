using System.Windows;
using System.Windows.Controls;

namespace ElevenLabsStudio;

/// <summary>
/// ShellView code-behind. We deliberately do not put Minimize /
/// Maximize / Close logic on the VM — those are pure Window
/// affordances, not application state. Hooking them here keeps the
/// VM free of UI-framework concerns (per docs/02-mvvm-conventions.md).
/// </summary>
public partial class ShellView : Window
{
    public ShellView()
    {
        InitializeComponent();

        Minimize.Click += (_, _) => WindowState = WindowState.Minimized;
        MaximizeRestore.Click += (_, _) => ToggleMaximized();
        Close.Click += (_, _) => Close();
        StateChanged += (_, _) => UpdateMaximizeIcon();
    }

    private void ToggleMaximized()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void UpdateMaximizeIcon()
    {
        if (MaximizeIcon is null) return;
        MaximizeIcon.Kind = WindowState == WindowState.Maximized
            ? MaterialDesignThemes.Wpf.PackIconKind.WindowRestore
            : MaterialDesignThemes.Wpf.PackIconKind.WindowMaximize;
    }
}