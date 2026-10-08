using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Shell;
using Caliburn.Micro;

namespace ElevenLabsStudio.Views;

public partial class ShellView : Window
{
	public ShellView()
	{
		InitializeComponent();
		WindowWorkArea.FitToCursorMonitor(this);
		ApplyFrameShadow();

		SidebarToggle.Click += (_, _) => ToggleSidebar();
		Minimize.Click += (_, _) => WindowState = WindowState.Minimized;
		MaximizeRestore.Click += (_, _) => ToggleMaximized();
		CloseWindow.Click += (_, _) => RequestClose();
		StateChanged += (_, _) =>
		{
			UpdateMaximizeIcon();
			KeepMaximizedContentInsideWorkArea();
			ApplyFrameShadow();
		};
		Closing += OnClosing;
	}

	// -- Close guard ------------------------------------------------------
	//
	// Edits live in view models; a draft is only written when the app
	// explicitly asks for it. Without this guard, Alt+F4 / the X button
	// tore the process down with dirty fields still on screen and no
	// draft ever written. All of the decision logic lives in
	// ShellViewModel.TryCloseAsync — code-behind only sequences the
	// async close and swallows the first Closing event.
	private bool _closeApproved;
	private bool _closeCheckInFlight;

	/// <summary>
	/// Close after the guard has already approved this close (or when
	/// there was nothing to guard).
	/// </summary>
	private void RequestClose()
	{
		_closeApproved = true;
		Close();
	}

	private async void OnClosing(object? sender, CancelEventArgs e)
	{
		if (_closeApproved) return;

		// First pass: always swallow. The window is re-closed through
		// RequestClose() only after the view model says yes.
		e.Cancel = true;
		if (_closeCheckInFlight) return;
		_closeCheckInFlight = true;
		try
		{
			if (DataContext is not ViewModels.ShellViewModel shell) return;
			if (await shell.TryCloseAsync())
			{
				RequestClose();
			}
		}
		catch (Exception ex)
		{
			// Never let a guard failure take the app down: the safe
			// outcome is to stay open with the user's edits intact.
			System.Diagnostics.Debug.WriteLine(
				$"Close guard failed; keeping the window open. {ex}");
		}
		finally
		{
			_closeCheckInFlight = false;
		}
	}

	private bool _isSidebarCollapsed;

	private void ToggleSidebar()
	{
		_isSidebarCollapsed = !_isSidebarCollapsed;
		SidebarColumn.Width = new GridLength(_isSidebarCollapsed ? 48 : 260);
		// Force-collapse the host content too — Visibility alone lets the
		// ContentControl keep rendering its content into a 48-px column.
		SidebarHost.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
		CollapsedSidebarRail.Visibility = _isSidebarCollapsed ? Visibility.Visible : Visibility.Collapsed;
		SidebarToggleIcon.Kind = _isSidebarCollapsed
			? MaterialDesignThemes.Wpf.PackIconKind.ChevronRight
			: MaterialDesignThemes.Wpf.PackIconKind.ChevronLeft;

		var action = _isSidebarCollapsed ? "Expand agents sidebar" : "Collapse agents sidebar";
		SidebarToggle.ToolTip = action;
		AutomationProperties.SetName(SidebarToggle, action);
	}

	private void ToggleMaximized()
	{
		WindowState = WindowState == WindowState.Maximized
			? WindowState.Normal
			: WindowState.Maximized;
	}

	private void UpdateMaximizeIcon()
	{
		if (MaximizeIcon is null) return;
		MaximizeIcon.Kind = WindowState == WindowState.Maximized
			? MaterialDesignThemes.Wpf.PackIconKind.WindowRestore
			: MaterialDesignThemes.Wpf.PackIconKind.WindowMaximize;
	}

	/// <summary>
	/// A 1px glass frame is what makes DWM paint the system drop shadow
	/// around this borderless window. Zero while maximized, so the shadow
	/// and that glass edge do not sit inside the work area.
	/// </summary>
	internal static Thickness FrameShadowThickness(WindowState state) =>
		state == WindowState.Maximized ? new Thickness(0) : new Thickness(1);

	private void ApplyFrameShadow()
	{
		var thickness = FrameShadowThickness(WindowState);
		var chrome = WindowChrome.GetWindowChrome(this);
		if (chrome is not null && !chrome.IsFrozen && chrome.GlassFrameThickness == thickness)
		{
			return;
		}

		WindowChrome.SetWindowChrome(this, new WindowChrome
		{
			CaptionHeight = 56,
			ResizeBorderThickness = new Thickness(6),
			CornerRadius = new CornerRadius(0),
			GlassFrameThickness = thickness,
			UseAeroCaptionButtons = false,
		});
	}

	/// <summary>
	/// Keep the window content inside the taskbar when maximized.
	/// <para>
	/// A <c>WindowStyle="None"</c> window driven by <see cref="WindowChrome"/>
	/// is sized to the full <em>monitor</em> rectangle when maximized, not to
	/// the work area. On a 1920x1080 screen with a 48px taskbar the window is
	/// therefore 48px taller than anything visible, and the bottom of the
	/// content — which is the 56px sticky action footer — sits behind the
	/// taskbar. The composer and the Save/Push buttons simply vanish at the
	/// bottom edge, which reads as "the input box disappeared when I
	/// maximized". Insetting the root frame by exactly the monitor/work-area
	/// difference puts the footer back on screen.
	/// </para>
	/// <para>
	/// <see cref="SystemParameters.WorkArea"/> describes the primary monitor,
	/// so this keeps the content visible for the common single-monitor (and
	/// maximize-on-primary) case. A window maximized onto a secondary monitor
	/// would need a per-monitor work-area query.
	/// </para>
	/// </summary>
	private void KeepMaximizedContentInsideWorkArea()
	{
		if (WindowState != WindowState.Maximized)
		{
			RootFrame.Margin = new Thickness(0);
			return;
		}

		var workArea = SystemParameters.WorkArea;
		var screen = new Rect(
			0, 0,
			SystemParameters.PrimaryScreenWidth,
			SystemParameters.PrimaryScreenHeight);
		RootFrame.Margin = new Thickness(
			Math.Max(0, workArea.Left - screen.Left),
			Math.Max(0, workArea.Top - screen.Top),
			Math.Max(0, screen.Right - workArea.Right),
			Math.Max(0, screen.Bottom - workArea.Bottom));
	}

	// -- Detail pane view resolution ------------------------------------
	//
	// The ShellViewModel hands us either null, an
	// AgentDetailPlaceholder, or an AgentDetailViewModel. We resolve
	// the matching View (placeholder for non-AgentDetailViewModel types,
	// or the looked-up View for AgentDetailViewModel) and assign it to
	// DetailHost.Content. This bypasses CM's nested-ContentControl view
	// locate, which we proved empirically does not fire on the right
	// pane in this app.
	private void DetailHost_Loaded(object sender, RoutedEventArgs e)
	{
		ResolveAndAssignContent(sender);
	}

	private void DetailHost_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		ResolveAndAssignContent(sender);
	}

	private void ResolveAndAssignContent(object sender)
	{
		if (sender is not ContentControl host) return;
		var dc = host.DataContext;

		FrameworkElement? view = dc switch
		{
			null => new Views.AgentDetail.AgentDetailPlaceholder(),
			ViewModels.AgentDetail.AgentDetailViewModel vm => ResolveView(vm),
			_ => new TextBlock { Text = $"Unsupported detail VM: {dc.GetType().FullName}" },
		};

		host.Content = view;
	}

	private static FrameworkElement ResolveView(ViewModels.AgentDetail.AgentDetailViewModel vm)
	{
		var viewType = ViewLocator.LocateTypeForModelType(vm.GetType(), null, null);
		if (viewType is null)
		{
			return new TextBlock
			{
				Text = $"Cannot find view for {vm.GetType().FullName}",
			};
		}

		var view = (FrameworkElement)Activator.CreateInstance(viewType)!;
		ViewModelBinder.Bind(vm, view, null);
		return view;
	}
}
