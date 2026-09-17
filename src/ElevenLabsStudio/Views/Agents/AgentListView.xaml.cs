using System.Windows.Controls;

namespace ElevenLabsStudio.Views.Agents;

/// <summary>
/// Per MVVM rules, code-behind only initialises the component. All
/// interaction lives in <c>AgentListViewModel</c>.
/// </summary>
public partial class AgentListView : UserControl
{
    public AgentListView()
    {
        InitializeComponent();
    }
}