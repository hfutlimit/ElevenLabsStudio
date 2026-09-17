using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ElevenLabsStudio.Views.AgentDetail;

public partial class SystemPromptTab : UserControl
{
    private const int ElevenLabsPromptWarn = 8000;

    public SystemPromptTab()
    {
        InitializeComponent();
        Prompt.TextChanged += (_, _) => UpdateLength();
    }

    /// <summary>Re-evaluate the prompt length label from the ViewModel
    /// after a data binding or external change. Called by
    /// <see cref="OnPromptChanged"/> via the TextChanged event, but
    /// also exposed so the parent VM can trigger a re-read after it
    /// programmatically replaces the text.</summary>
    public void UpdateLength()
    {
        if (PromptLength is null) return;
        var len = Prompt.Text?.Length ?? 0;
        PromptLength.Text = len.ToString("N0") + " chars";
        PromptLength.Foreground = (Brush)TryFindResource(len switch
        {
            0 => "App.TextMuted",
            > ElevenLabsPromptWarn => "App.Danger",
            > (int)(ElevenLabsPromptWarn * 0.9) => "App.Accent",
            _ => "App.TextSecondary",
        });
    }
}