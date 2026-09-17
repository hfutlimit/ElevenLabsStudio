# 03 · 领域与 ElevenLabs API

> 详细版见 AgentBoard 项目 19 `ElevenLabs Specs / 03 · 领域与 ElevenLabs API`。

## Core Domain 速查

```csharp
public sealed record Agent(string AgentId, string Name, string Prompt,
    string FirstMessage, string? VoiceId,
    IReadOnlyList<Variable> Variables, DateTimeOffset UpdatedAt);

public sealed record ConversationRecord(string ConversationId, string AgentId,
    DateTimeOffset StartedAt, DateTimeOffset EndedAt, int DurationMs,
    string Status, IReadOnlyList<TranscriptTurn> Turns);

public sealed record TranscriptTurn(string Speaker, string Text, DateTimeOffset At);

public sealed record Variable(string Name, string? Value, string Type);

public sealed record Voice(string VoiceId, string Name, string? Category, string? PreviewUrl);

public sealed record Suggestion(string FieldName, string CurrentValue,
    string ProposedValue, string Rationale, SuggestionSeverity Severity);

public sealed record AgentUpdate(string? Prompt = null, string? FirstMessage = null,
    string? VoiceId = null, IReadOnlyList<Variable>? Variables = null);
```

## `IElevenLabsClient`

```csharp
Task<IReadOnlyList<Agent>> ListAgentsAsync(CancellationToken ct = default);
Task<Agent> GetAgentAsync(string agentId, CancellationToken ct = default);
Task<Agent> UpdateAgentAsync(string agentId, AgentUpdate update, CancellationToken ct = default);
Task<IReadOnlyList<ConversationRecord>> ListConversationsAsync(
    string agentId, DateTimeOffset? from = null, DateTimeOffset? to = null,
    int pageSize = 100, string? cursor = null, CancellationToken ct = default);
Task<ConversationRecord> GetConversationAsync(string conversationId, CancellationToken ct = default);
Task<IReadOnlyList<Voice>> ListVoicesAsync(CancellationToken ct = default);
```

## "本地建议操作更新 Agent" 流程

1. 远端拉 Agent → 本地编辑提示词 / 首句 / 语音 / 变量
2. `ISuggestionEngine.Analyze(current, proposed)` → `IReadOnlyList<Suggestion>`（本地、可空、网络 0 依赖）
3. UI 展示建议列表 → 用户勾选确认
4. `IElevenLabsClient.UpdateAgentAsync(agentId, update)` 推送
5. 成功后发布 `AgentUpdatedEvent` → 其它 VM 同步

## 配置

```
appsettings.json:
  ElevenLabs:
    ApiKey: ""           # 留空，由 ELEVENLABS_API_KEY 环境变量注入
    BaseUrl: "https://api.elevenlabs.io/"
    Polly:
      RetryCount: 3
      RetryBaseDelayMs: 500
      CircuitBreakerThreshold: 5
```