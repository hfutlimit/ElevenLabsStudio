using System.Windows;
using System.Windows.Controls;
using Caliburn.Micro;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.ViewModels.AgentDetail;

namespace ElevenLabsStudio.Views.AgentDetail;

public partial class WorkflowTab : UserControl
{
    public WorkflowTab()
    {
        InitializeComponent();
    }

    private void RemoveNode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not WorkflowNode node) return;
        if (DataContext is WorkflowTabViewModel vm)
        {
            vm.RemoveNode(node);
        }
    }
}