using System.Windows;
using System.Windows.Controls;
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

    // -- Detail pane view resolution ------------------------------------
    //
    // The ShellViewModel hands us either null, an
    // AgentDetailPlaceholder, or an AgentDetailViewModel. We resolve
    // the matching View (placeholder for non-AgentDetailViewModel types,
    // or the looked-up View for AgentDetailViewModel) and assign it to
    // DetailHost.Content. This bypasses CM's nested-ContentControl view
    // locate, which we proved empirically does not fire on the right
    // pane in this app.
    private void DetailHost_Loaded(object sender, RoutedEventArgs e)
    {
        ResolveAndAssignContent(sender);
    }

    private void DetailHost_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        ResolveAndAssignContent(sender);
    }

    private void ResolveAndAssignContent(object sender)
    {
        if (sender is not ContentControl host) return;
        var dc = host.DataContext;

        FrameworkElement? view = dc switch
        {
            null => new Views.AgentDetail.AgentDetailPlaceholder(),
            ViewModels.AgentDetail.AgentDetailViewModel vm => ResolveView(vm),
            _ => new TextBlock { Text = $"Unsupported detail VM: {dc.GetType().FullName}" },
        };

        host.Content = view;
    }

    private static FrameworkElement ResolveView(ViewModels.AgentDetail.AgentDetailViewModel vm)
    {
        var viewType = ViewLocator.LocateTypeForModelType(vm.GetType(), null, null);
        if (viewType is null)
        {
            return new TextBlock
            {
                Text = $"Cannot find view for {vm.GetType().FullName}",
            };
        }

        var view = (FrameworkElement)Activator.CreateInstance(viewType)!;
        ViewModelBinder.Bind(vm, view, null);
        return view;
    }
}