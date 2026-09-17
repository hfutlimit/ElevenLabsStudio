using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Infrastructure.Http.Dto;
using ElevenLabsStudio.Infrastructure.Http.Mapping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static ElevenLabsStudio.Infrastructure.Http.Mapping.Mapping;

namespace ElevenLabsStudio.Infrastructure.Http;

/// <summary>
/// Typed HttpClient implementation of <see cref="IElevenLabsClient"/>.
/// Lives in Infrastructure so it can depend on <c>HttpClient</c>,
/// <c>IOptionsMonitor&lt;ElevenLabsOptions&gt;</c> and the internal DTO
/// types. Callers receive Core Domain records — never DTOs.
/// </summary>
public sealed class ElevenLabsHttpClient : IElevenLabsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ElevenLabsHttpClient> _logger;
    private readonly IOptionsMonitor<ElevenLabsOptions> _options;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public ElevenLabsHttpClient(
        HttpClient http,
        ILogger<ElevenLabsHttpClient> logger,
        IOptionsMonitor<ElevenLabsOptions> options)
    {
        _http = http;
        _logger = logger;
        _options = options;
    }

    private void PrepareHeaders()
    {
        var apiKey = _options.CurrentValue.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ElevenLabsAuthException(
                "ElevenLabs API key is not configured. Set ElevenLabs:ApiKey in appsettings.json or ELEVENLABS_API_KEY environment variable.");
        }

        _http.DefaultRequestHeaders.Remove("xi-api-key");
        _http.DefaultRequestHeaders.Add("xi-api-key", apiKey);
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var baseUrl = _options.CurrentValue.BaseUrl;
        if (!string.IsNullOrWhiteSpace(baseUrl) && _http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        }
    }

    public async Task<IReadOnlyList<Agent>> ListAgentsAsync(CancellationToken ct = default)
    {
        PrepareHeaders();
        var url = "v1/convai/agents";
        _logger.LogInformation("GET {Url}", url);

        var dto = await SendAsync<ElevenLabsAgentListResponseDto>(
            HttpMethod.Get, url, ct);
        return dto.Agents.Select(MapToAgent).ToList();
    }

    public async Task<Agent> GetAgentAsync(string agentId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        PrepareHeaders();
        var url = $"v1/convai/agents/{Uri.EscapeDataString(agentId)}";
        _logger.LogInformation("GET {Url}", url);

        var dto = await SendAsync<ElevenLabsAgentDto>(HttpMethod.Get, url, ct);
        return MapToAgent(dto);
    }

    public async Task<Agent> UpdateAgentAsync(
        string agentId,
        AgentUpdate update,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentNullException.ThrowIfNull(update);

        PrepareHeaders();
        var url = $"v1/convai/agents/{Uri.EscapeDataString(agentId)}";
        _logger.LogInformation("PATCH {Url}", url);

        var payload = new
        {
            conversation_config = new
            {
                agent = new
                {
                    prompt = update.Prompt is null ? null : new { prompt = update.Prompt },
                    first_message = update.FirstMessage,
                },
                tts = update.VoiceId is null ? null : new { voice_id = update.VoiceId },
            },
        };

        var dto = await SendAsync<ElevenLabsAgentDto>(
            HttpMethod.Patch, url, ct, payload);
        return MapToAgent(dto);
    }

    public async Task<IReadOnlyList<ConversationRecord>> ListConversationsAsync(
        string agentId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int pageSize = 100,
        string? cursor = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        PrepareHeaders();

        var query = new List<string>
        {
            $"agent_id={Uri.EscapeDataString(agentId)}",
            $"page_size={Math.Clamp(pageSize, 1, 1000)}",
        };
        if (from is not null) query.Add($"start_time_unix_secs_gte={from.Value.ToUnixTimeSeconds()}");
        if (to is not null) query.Add($"start_time_unix_secs_lte={to.Value.ToUnixTimeSeconds()}");
        if (cursor is not null) query.Add($"cursor={Uri.EscapeDataString(cursor)}");

        var url = $"v1/convai/conversations?{string.Join('&', query)}";
        _logger.LogInformation("GET {Url}", url);

        var dto = await SendAsync<ElevenLabsConversationListResponseDto>(
            HttpMethod.Get, url, ct);
        return dto.Conversations.Select(MapToConversation).ToList();
    }

    public async Task<ConversationRecord> GetConversationAsync(
        string conversationId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        PrepareHeaders();

        var url = $"v1/convai/conversations/{Uri.EscapeDataString(conversationId)}";
        _logger.LogInformation("GET {Url}", url);

        var dto = await SendAsync<ElevenLabsConversationDto>(
            HttpMethod.Get, url, ct);
        return MapToConversation(dto);
    }

    public async Task<IReadOnlyList<Voice>> ListVoicesAsync(CancellationToken ct = default)
    {
        PrepareHeaders();
        var url = "v1/voices";
        _logger.LogInformation("GET {Url}", url);

        var dto = await SendAsync<ElevenLabsVoiceListResponseDto>(
            HttpMethod.Get, url, ct);
        return dto.Voices.Select(MapToVoice).ToList();
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string url,
        CancellationToken ct,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw ElevenLabsExceptionFactory.FromHttp(response.StatusCode, text);
        }

        return JsonSerializer.Deserialize<T>(text, JsonOpts)
            ?? throw new ElevenLabsException(
                $"ElevenLabs returned empty body for {url}",
                httpStatus: (int)response.StatusCode);
    }
}