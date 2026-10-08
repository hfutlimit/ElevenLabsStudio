using System.Windows;
using System.Windows.Controls;

namespace ElevenLabsStudio.Views.AgentDetail;

public partial class DynamicVariablesDialogView : UserControl
{
	private const double MaxWidthFraction = 0.8;
	private const double MaxHeightFraction = 0.85;

	public DynamicVariablesDialogView()
	{
		InitializeComponent();
		Loaded += ClampToWorkArea;
	}

	/// <summary>
	/// Keep the dialog inside the desktop work area.
	/// <para>
	/// The view carries its preferred Width/Height so Caliburn's
	/// WindowManager sizes the dialog before it is shown — no resize flash
	/// on a normal screen, because the clamp only acts when the preferred
	/// size would not fit. On a short laptop screen it pulls the window
	/// back inside the work area, which is what stops the Save/Cancel row
	/// from dropping off the bottom.
	/// </para>
	/// </summary>
	private void ClampToWorkArea(object sender, RoutedEventArgs e)
	{
		Loaded -= ClampToWorkArea;
		if (Window.GetWindow(this) is not { } window) return;

		var workArea = SystemParameters.WorkArea;
		var maxWidth = workArea.Width * MaxWidthFraction;
		var maxHeight = workArea.Height * MaxHeightFraction;

		var width = Math.Min(ActualWidth > 0 ? ActualWidth : Width, maxWidth);
		var height = Math.Min(ActualHeight > 0 ? ActualHeight : Height, maxHeight);
		if (Math.Abs(width - ActualWidth) < 0.5 && Math.Abs(height - ActualHeight) < 0.5) return;

		window.SizeToContent = SizeToContent.Manual;
		window.Width = width;
		window.Height = height;
		// Keep the view pinned to whatever the window ends up being, so a
		// manual resize keeps the action row inside the client area.
		Width = double.NaN;
		Height = double.NaN;
		MinWidth = 0;
		MinHeight = 0;
	}
}