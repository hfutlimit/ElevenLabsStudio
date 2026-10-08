using System.Windows.Controls;

namespace ElevenLabsStudio.Views.AgentDetail;

/// <summary>
/// No behaviour beyond construction. Row actions are declared in XAML via
/// Caliburn bindings rather than handled here, so the View never reaches
/// into VariablesTabViewModel itself.
/// </summary>
public partial class VariablesTabView : UserControl
{
    public VariablesTabView()
    {
        InitializeComponent();
    }
}