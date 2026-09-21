using Microsoft.Extensions.Configuration;

namespace ElevenLabsStudio.Configuration;

public static class ConfigurationBuilderExtensions
{
	public static IConfigurationBuilder AddElevenLabsApiKeyAlias(
		this IConfigurationBuilder builder,
		Func<string, string?> readEnvironment)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(readEnvironment);

		var apiKey = readEnvironment("ELEVENLABS_API_KEY");
		if (!string.IsNullOrWhiteSpace(apiKey))
		{
			builder.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ElevenLabs:ApiKey"] = apiKey,
			});
		}

		return builder;
	}
}
