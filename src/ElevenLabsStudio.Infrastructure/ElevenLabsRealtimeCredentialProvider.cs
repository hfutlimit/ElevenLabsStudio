using System.Text.Json;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;
using Microsoft.Extensions.Options;

namespace ElevenLabsStudio.Infrastructure;

public sealed class ElevenLabsRealtimeCredentialProvider : IRealtimeSessionCredentialProvider
{
	private readonly HttpClient _http;
	private readonly IOptionsMonitor<ElevenLabsOptions> _options;

	public ElevenLabsRealtimeCredentialProvider(
		HttpClient http,
		IOptionsMonitor<ElevenLabsOptions> options)
	{
		_http = http;
		_options = options;
	}

	public async Task<string> GetSignedUrlAsync(
		RealtimeConversationOptions options,
		CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(options.AgentId);

		var current = _options.CurrentValue;
		if (string.IsNullOrWhiteSpace(current.ApiKey))
		{
			throw new ElevenLabsAuthException(
				"ElevenLabs API key is not configured. Set ElevenLabs:ApiKey or ELEVENLABS_API_KEY.");
		}

		var environment = string.IsNullOrWhiteSpace(options.Environment)
			? "production"
			: options.Environment;
		var query = new List<string>
		{
			$"agent_id={Uri.EscapeDataString(options.AgentId)}",
			$"environment={Uri.EscapeDataString(environment)}",
		};
		if (!string.IsNullOrWhiteSpace(options.BranchId))
		{
			query.Add($"branch_id={Uri.EscapeDataString(options.BranchId)}");
		}

		using var request = new HttpRequestMessage(
			HttpMethod.Get,
			$"v1/convai/conversation/get-signed-url?{string.Join('&', query)}");
		request.Headers.Add("xi-api-key", current.ApiKey);
		request.Headers.Accept.ParseAdd("application/json");

		using var response = await _http.SendAsync(request, ct);
		var body = await response.Content.ReadAsStringAsync(ct);
		if (!response.IsSuccessStatusCode)
		{
			throw new ElevenLabsException(
				$"ElevenLabs signed URL request failed: {body}",
				(int)response.StatusCode);
		}

		using var document = JsonDocument.Parse(body);
		if (!document.RootElement.TryGetProperty("signed_url", out var signedUrl)
			|| string.IsNullOrWhiteSpace(signedUrl.GetString()))
		{
			throw new ElevenLabsException(
				"ElevenLabs signed URL response did not contain signed_url.",
				(int)response.StatusCode);
		}

		return signedUrl.GetString()!;
	}
}
