using System.Windows;
using System.Windows.Controls;
using Caliburn.Micro;
using ElevenLabsStudio.Services;
using ElevenLabsStudio.ViewModels.AgentDetail;

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

	private void RemoveDynamicVariable_Click(object sender, RoutedEventArgs e)
	{
		if (sender is not Button button || button.Tag is not DynamicVariableEntry variable) return;
		if (DataContext is LiveConversationViewModel vm)
		{
			vm.RemoveDynamicVariable(variable);
		}
	}
}
