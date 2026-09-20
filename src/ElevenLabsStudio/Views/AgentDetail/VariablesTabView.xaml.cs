using System.Windows;
using System.Windows.Controls;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.ViewModels.AgentDetail;

namespace ElevenLabsStudio.Views.AgentDetail;

public partial class VariablesTabView : UserControl
{
    public VariablesTabView()
    {
        InitializeComponent();
    }

    private void RemoveVariable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Variable variable) return;
        if (DataContext is VariablesTabViewModel vm)
        {
            vm.RemoveVariable(variable);
        }
    }
}