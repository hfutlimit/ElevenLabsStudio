using System.Windows;
using ElevenLabsStudio.Views.AgentDetail;
using ElevenLabsStudio.Views.Agents;
using FluentAssertions;

namespace ElevenLabsStudio.UnitTests.Views;

/// <summary>
/// Guards against the class of bug that shipped silently for a whole
/// review cycle: an invalid attribute value (e.g.
/// <c>VerticalAlignment="Baseline"</c>, which is not a member of the
/// enum) compiles fine but throws a <see cref="XamlParseException"/>
/// the first time the view is constructed at runtime. Type-mapping and
/// binding tests never touch the BAML loader, so they stay green while
/// the real app crashes the moment a selection tries to render the
/// view. Constructing every rebuilt view on a live STA dispatcher is
/// the cheapest way to catch it in CI.
/// </summary>
public sealed class ViewConstructionSmokeTests
{
	[Theory]
	[InlineData(typeof(ShellView))]
	[InlineData(typeof(AgentListView))]
	[InlineData(typeof(AgentDetailView))]
	[InlineData(typeof(AgentDetailPlaceholder))]
	[InlineData(typeof(SystemPromptTabView))]
	[InlineData(typeof(FirstMessageTabView))]
	public async Task View_initializes_without_a_xaml_parse_error(Type viewType)
	{
		Exception? failure = null;
		await WpfTestHost.RunAsync(() =>
		{
			try
			{
				var view = (FrameworkElement)Activator.CreateInstance(viewType)!;
				// Force a measure/arrange so any resource that only
				// resolves during layout (StaticResource in a template)
				// is exercised, not just the top-level BAML. A Window
				// cannot be hosted inside another Window's content, so
				// ShellView is shown directly; everything else is a
				// UserControl wrapped in a throwaway host.
				Window host = view is Window w
					? w
					: new Window { Content = view, Width = 900, Height = 600 };
				host.WindowState = WindowState.Minimized;
				host.Show();
				host.UpdateLayout();
				host.Close();
			}
			catch (Exception ex)
			{
				failure = ex;
			}
		});

		failure.Should().BeNull(
			$"{viewType.Name} must construct and lay out without throwing; " +
			$"this is the runtime-only failure mode the type-mapping tests cannot see. " +
			$"{failure?.GetType().Name}: {failure?.Message}");
	}
}
