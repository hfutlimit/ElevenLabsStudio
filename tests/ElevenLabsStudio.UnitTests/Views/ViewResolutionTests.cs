using System.Windows;
using Caliburn.Micro;
using ElevenLabsStudio.ViewModels;
using ElevenLabsStudio.ViewModels.Agents;
using ElevenLabsStudio.Views;
using ElevenLabsStudio.Views.Agents;
using FluentAssertions;

namespace ElevenLabsStudio.UnitTests.Views;

public sealed class ViewResolutionTests
{
	[Theory]
	[InlineData(typeof(SettingsViewModel), typeof(SettingsView))]
	[InlineData(typeof(PullAgentDialogViewModel), typeof(PullAgentDialogView))]
	public void View_type_matches_default_Caliburn_mapping(
		Type viewModelType,
		Type expectedViewType)
	{
		var transformed = viewModelType.FullName!
			.Replace(".ViewModels.", ".Views.")
			.Replace("ViewModel", "View");

		transformed.Should().Be(expectedViewType.FullName);
		typeof(UIElement).IsAssignableFrom(expectedViewType).Should().BeTrue();
	}

	[Theory]
	[InlineData(typeof(SettingsViewModel), typeof(SettingsView))]
	[InlineData(typeof(PullAgentDialogViewModel), typeof(PullAgentDialogView))]
	public void Caliburn_locator_resolves_the_expected_View_type(
		Type viewModelType,
		Type expectedViewType)
	{
		if (!AssemblySource.Instance.Contains(expectedViewType.Assembly))
		{
			AssemblySource.Instance.Add(expectedViewType.Assembly);
		}

		var resolved = ViewLocator.LocateTypeForModelType(viewModelType, null, null);

		resolved.Should().Be(expectedViewType);
	}
}
