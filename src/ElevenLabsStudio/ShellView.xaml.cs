using System.Windows;
using Caliburn.Micro;

namespace ElevenLabsStudio;

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

    /// <summary>
    /// Wires a single child <see cref="ContentControl"/> at the centre of
    /// the right pane so we can host any view the ShellViewModel needs.
    /// </summary>
    public object? DetailContent
    {
        get => DetailHost.Content;
        set => DetailHost.Content = value;
    }
}