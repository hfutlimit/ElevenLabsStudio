using System.Windows;
using System.Windows.Controls;
using Caliburn.Micro;
using ElevenLabsStudio.Services;

namespace ElevenLabsStudio.Views.AgentDetail;

public partial class LiveConversationView : UserControl
{
	public LiveConversationView()
	{
		InitializeComponent();
		Loaded += OnLoaded;
	}

	/// <summary>
	/// Follow the latest line when the transcript grows, but only if the
	/// reader was already there. A thumb dragged up to reread an earlier
	/// turn stays where it was.
	/// </summary>
	internal static bool ShouldFollowTranscript(
		double extentHeight,
		double verticalOffset,
		double viewportHeight,
		double extentHeightChange,
		double slack = 24)
	{
		if (extentHeightChange <= 0) return false;
		var distanceFromBottom = extentHeight - verticalOffset - viewportHeight;
		return distanceFromBottom <= extentHeightChange + slack;
	}

	private void OnTranscriptScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (sender is not ScrollViewer viewer) return;
		if (!ShouldFollowTranscript(e.ExtentHeight, e.VerticalOffset, e.ViewportHeight, e.ExtentHeightChange))
			return;
		viewer.ScrollToEnd();
	}

	private async void OnLoaded(object sender, RoutedEventArgs e)
	{
		try
		{
			var client = (WebViewRealtimeConversationClient)IoC.GetInstance(
				typeof(WebViewRealtimeConversationClient),
				null)!;
			await client.AttachAsync(AudioHost);
		}
		catch
		{
			// The ViewModel owns user-facing error dialogs. The host will
			// report a useful failure when Start is pressed if initialization
			// could not complete (for example, missing WebView2 runtime).
		}
	}

}
