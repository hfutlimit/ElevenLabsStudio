using System.Windows;

namespace ElevenLabsStudio;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		// Application resources are deferred, so the bootstrapper is not
		// constructed until something asks for it. Resolve it here, before
		// Run() raises Startup, so it can hook the event.
		_ = Resources["bootstrapper"];
	}
}
