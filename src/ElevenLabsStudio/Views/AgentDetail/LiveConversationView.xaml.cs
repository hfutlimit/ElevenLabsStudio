using System.Windows.Controls;
using ElevenLabsStudio.ViewModels.AgentDetail;

namespace ElevenLabsStudio.Views.AgentDetail;

public partial class LiveConversationView : UserControl
{
	public LiveConversationView()
	{
		InitializeComponent();
	}

	private void OnTranscriptScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (sender is not ScrollViewer viewer) return;
		if (!LiveConversationViewModel.ShouldFollowTranscript(
				e.ExtentHeight, e.VerticalOffset, e.ViewportHeight, e.ExtentHeightChange))
			return;
		viewer.ScrollToEnd();
	}
}
