using ElevenLabsStudio.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace ElevenLabsStudio.UnitTests.Configuration;

public sealed class ConfigurationBuilderExtensionsTests
{
	[Fact]
	public void AddElevenLabsApiKeyAlias_maps_documented_environment_name()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ElevenLabs:ApiKey"] = "json-key",
			})
			.AddElevenLabsApiKeyAlias(
				name => name == "ELEVENLABS_API_KEY" ? "environment-key" : null)
			.Build();

		configuration["ElevenLabs:ApiKey"].Should().Be("environment-key");
	}

	[Fact]
	public void AddElevenLabsApiKeyAlias_keeps_existing_value_when_alias_is_empty()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ElevenLabs:ApiKey"] = "json-key",
			})
			.AddElevenLabsApiKeyAlias(_ => " ")
			.Build();

		configuration["ElevenLabs:ApiKey"].Should().Be("json-key");
	}
}
