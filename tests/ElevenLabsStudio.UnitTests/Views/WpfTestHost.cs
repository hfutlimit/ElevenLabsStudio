using System.Windows;
using System.Windows.Threading;
using Caliburn.Micro;

namespace ElevenLabsStudio.UnitTests.Views;

internal static class WpfTestHost
{
	private static readonly Lazy<Task<Dispatcher>> DispatcherTask = new(StartDispatcher);

	public static async Task RunAsync(System.Action action)
	{
		var dispatcher = await DispatcherTask.Value.WaitAsync(TimeSpan.FromSeconds(10));
		await dispatcher.InvokeAsync(action, DispatcherPriority.Send).Task
			.WaitAsync(TimeSpan.FromSeconds(10));
	}

	private static Task<Dispatcher> StartDispatcher()
	{
		var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
		var thread = new Thread(() =>
		{
			try
			{
				var application = Application.Current as App ?? new App();
				application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
				application.InitializeComponent();
				PlatformProvider.Current = new XamlPlatformProvider();
				ready.SetResult(Dispatcher.CurrentDispatcher);
				Dispatcher.Run();
			}
			catch (Exception ex)
			{
				ready.TrySetException(ex);
			}
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.IsBackground = true;
		thread.Start();
		return ready.Task;
	}
}
