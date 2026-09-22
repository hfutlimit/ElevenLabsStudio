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
