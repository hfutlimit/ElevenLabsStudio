using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ElevenLabsStudio.Views.AgentDetail;

public partial class FirstMessageTab : UserControl
{
    private const int FirstMessageWarn = 500;

    public FirstMessageTab()
    {
        InitializeComponent();
        FirstMessage.TextChanged += (_, _) => UpdateLength();
    }

    public void UpdateLength()
    {
        if (FirstMessageLength is null) return;
        var len = FirstMessage.Text?.Length ?? 0;
        FirstMessageLength.Text = len.ToString("N0") + " chars";
        FirstMessageLength.Foreground = (Brush)TryFindResource(len switch
        {
            0 => "App.TextMuted",
            > FirstMessageWarn => "App.Danger",
            > (int)(FirstMessageWarn * 0.9) => "App.Accent",
            _ => "App.TextSecondary",
        });
    }
}