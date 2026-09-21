using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using FluentAssertions;

namespace ElevenLabsStudio.UnitTests.Views;

public sealed class VisualPolishTests
{
	[Fact]
	public async Task Design_system_provides_distinct_layers_and_consistent_control_styles()
	{
		var errors = new ConcurrentQueue<Exception>();
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var thread = new Thread(() =>
		{
			try
			{
				VerifyDesignSystem();
			}
			catch (Exception ex)
			{
				errors.Enqueue(ex);
			}
			finally
			{
				completed.SetResult();
			}
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();

		await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
		errors.Should().BeEmpty();
	}

	private static void VerifyDesignSystem()
	{
		var xamlPath = FindDesignSystemXaml();
		using var stream = File.OpenRead(xamlPath);
		var resources = (ResourceDictionary)XamlReader.Load(stream);

		var canvas = GetBrush(resources, "App.Canvas").Color;
		var surface = GetBrush(resources, "App.Surface").Color;
		var surfaceAlt = GetBrush(resources, "App.SurfaceAlt").Color;
		var borderStrong = GetBrush(resources, "App.BorderStrong").Color;
		var text = GetBrush(resources, "App.Text").Color;
		var textMuted = GetBrush(resources, "App.TextMuted").Color;
		var accent = GetBrush(resources, "App.Accent").Color;

		ColorDistance(canvas, surface).Should().BeGreaterThan(10,
			"the application canvas and cards need visible visual separation");
		ContrastRatio(borderStrong, surface).Should().BeGreaterThanOrEqualTo(3,
			"input boundaries need non-text contrast on white surfaces");
		ContrastRatio(borderStrong, surfaceAlt).Should().BeGreaterThanOrEqualTo(3,
			"input boundaries need non-text contrast on alternate surfaces");
		ContrastRatio(text, surface).Should().BeGreaterThan(7,
			"primary text should retain strong readability");
		ContrastRatio(textMuted, surface).Should().BeGreaterThanOrEqualTo(4.5,
			"muted helper text is still normal-sized readable content");
		ContrastRatio(textMuted, surfaceAlt).Should().BeGreaterThanOrEqualTo(4.5,
			"muted helper text remains readable on alternate surfaces");
		ContrastRatio(accent, Colors.White).Should().BeGreaterThan(4.5,
			"white labels on primary actions need accessible contrast");

		GetStyle(resources, "App.ElevatedCard", typeof(Border));
		GetStyle(resources, "App.SectionCard", typeof(Border));
		var primaryButtonStyle = GetStyle(resources, "App.PrimaryButton", typeof(Button));
		GetStyle(resources, "App.SecondaryButton", typeof(Button));
		GetStyle(resources, "App.IconButton", typeof(Button));
		var tabStyle = GetStyle(resources, "App.TabItem", typeof(TabItem));

		var button = new Button { Style = primaryButtonStyle };
		var tabItem = new TabItem { Style = tabStyle };
		button.FocusVisualStyle.Should().NotBeNull("keyboard users need a visible button focus cue");
		tabItem.FocusVisualStyle.Should().NotBeNull("keyboard users need a visible tab focus cue");
	}

	private static string FindDesignSystemXaml()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			var candidate = Path.Combine(
				directory.FullName,
				"src",
				"ElevenLabsStudio",
				"Themes",
				"DesignSystem.xaml");
			if (File.Exists(candidate)) return candidate;
			directory = directory.Parent;
		}

		throw new FileNotFoundException("Could not locate Themes/DesignSystem.xaml from the test output directory.");
	}

	private static SolidColorBrush GetBrush(ResourceDictionary resources, string key) =>
		resources[key].Should().BeOfType<SolidColorBrush>().Subject;

	private static Style GetStyle(ResourceDictionary resources, string key, Type targetType)
	{
		var style = resources[key].Should().BeOfType<Style>().Subject;
		style.TargetType.Should().Be(targetType);
		return style;
	}

	private static double ColorDistance(Color left, Color right)
	{
		var red = left.R - right.R;
		var green = left.G - right.G;
		var blue = left.B - right.B;
		return Math.Sqrt(red * red + green * green + blue * blue);
	}

	private static double ContrastRatio(Color left, Color right)
	{
		var lighter = Math.Max(RelativeLuminance(left), RelativeLuminance(right));
		var darker = Math.Min(RelativeLuminance(left), RelativeLuminance(right));
		return (lighter + 0.05) / (darker + 0.05);
	}

	private static double RelativeLuminance(Color color)
	{
		static double Channel(byte value)
		{
			var normalized = value / 255d;
			return normalized <= 0.03928
				? normalized / 12.92
				: Math.Pow((normalized + 0.055) / 1.055, 2.4);
		}

		return 0.2126 * Channel(color.R)
			+ 0.7152 * Channel(color.G)
			+ 0.0722 * Channel(color.B);
	}
}
