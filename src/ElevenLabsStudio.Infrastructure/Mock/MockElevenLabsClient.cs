using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;

namespace ElevenLabsStudio.Infrastructure.Mock;

/// <summary>
/// Offline stand-in for <see cref="IElevenLabsClient"/>. Returns a fixed
/// cast of agents + conversations so the UI can be exercised end-to-end
/// without an API key. Registered only when
/// <c>ElevenLabsOptions.Mock == true</c>; the real
/// <c>ElevenLabsHttpClient</c> is wired otherwise.
/// </summary>
public sealed class MockElevenLabsClient : IElevenLabsClient
{
    private static readonly Agent SalesAgent = new(
        AgentId: "agent_sales_001",
        Name: "Sales Rep",
        Prompt: "You are a polite sales rep for the AI\u00b7Cloud product line. " +
                "Greet the user, ask one qualifying question at a time, and never " +
                "fabricate pricing \u2014 if you do not know, defer to the human rep.",
        FirstMessage: "Hi! I'm the AI\u00b7Cloud sales assistant. What problem are " +
                       "you trying to solve today?",
        VoiceId: "voice_aria",
        Variables: new[]
        {
            new Variable("region", "CN", "string"),
            new Variable("industry", "saas", "string"),
        },
        Workflow: new Workflow(
            Nodes: new[]
            {
                new WorkflowNode("n1", "greeting", "Greeting"),
                new WorkflowNode("n2", "llm", "Lead Qualification"),
                new WorkflowNode("n3", "tool", "Knowledge Lookup"),
                new WorkflowNode("n4", "end", "Handoff to Human"),
            },
            RawJson: "{\"nodes\":[\"greeting\",\"llm\",\"tool\",\"end\"]}"),
        UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-12));

    private static readonly Agent SupportAgent = new(
        AgentId: "agent_support_002",
        Name: "Support Bot",
        Prompt: "You are the first-line support agent. Be empathetic, ask for " +
                "the customer's plan tier, then walk them through the most " +
                "relevant troubleshooting doc.",
        FirstMessage: "Hi there \u2014 what can I help you fix today?",
        VoiceId: "voice_onyx",
        Variables: new[]
        {
            new Variable("plan", "ok", "string"),
        },
        Workflow: new Workflow(
            Nodes: new[]
            {
                new WorkflowNode("n1", "greeting", "Greeting"),
                new WorkflowNode("n2", "llm", "Triage"),
                new WorkflowNode("n3", "tool", "Doc Search"),
            },
            RawJson: "{\"nodes\":[\"greeting\",\"llm\",\"tool\"]}"),
        UpdatedAt: DateTimeOffset.UtcNow.AddHours(-3));

    private static readonly Agent OnboardingAgent = new(
        AgentId: "agent_onboarding_003",
        Name: "Onboarding Guide",
        Prompt: "Walk the new user through the 3-step onboarding checklist. " +
                "Celebrate small wins.",
        FirstMessage: "Welcome aboard! Ready to set up your workspace?",
        VoiceId: "voice_chloe",
        Variables: Array.Empty<Variable>(),
        Workflow: new Workflow(
            Nodes: new[]
            {
                new WorkflowNode("n1", "greeting", "Welcome"),
                new WorkflowNode("n2", "llm", "Step 1"),
                new WorkflowNode("n3", "llm", "Step 2"),
                new WorkflowNode("n4", "llm", "Step 3"),
            },
            RawJson: "{\"nodes\":[\"greeting\",\"step1\",\"step2\",\"step3\"]}"),
        UpdatedAt: DateTimeOffset.UtcNow.AddDays(-1));

    private static readonly IReadOnlyList<Agent> _agents = new[]
    {
        SalesAgent,
        SupportAgent,
        OnboardingAgent,
    };

    private static readonly IReadOnlyList<ConversationRecord> _salesConversations = new[]
    {
        new ConversationRecord(
            ConversationId: "conv_2026_09_17_001",
            AgentId: "agent_sales_001",
            StartedAt: DateTimeOffset.UtcNow.AddMinutes(-22),
            EndedAt: DateTimeOffset.UtcNow.AddMinutes(-22).AddSeconds(184),
            DurationMs: 184_000,
            Status: "success",
            Turns: new[]
            {
                new TranscriptTurn("agent", "Hi! I'm the AI\u00b7Cloud sales assistant. What problem are you trying to solve today?", DateTimeOffset.UtcNow.AddMinutes(-22).AddSeconds(2)),
                new TranscriptTurn("user", "We need a tool to help our reps triage inbound leads automatically.", DateTimeOffset.UtcNow.AddMinutes(-22).AddSeconds(15)),
                new TranscriptTurn("agent", "Got it. Roughly how many inbound leads per week should I plan for?", DateTimeOffset.UtcNow.AddMinutes(-22).AddSeconds(40)),
                new TranscriptTurn("user", "Around 400.", DateTimeOffset.UtcNow.AddMinutes(-22).AddSeconds(58)),
                new TranscriptTurn("agent", "Perfect. I'll loop in a human rep to walk through pricing for that volume.", DateTimeOffset.UtcNow.AddMinutes(-22).AddSeconds(75)),
            }),
        new ConversationRecord(
            ConversationId: "conv_2026_09_16_009",
            AgentId: "agent_sales_001",
            StartedAt: DateTimeOffset.UtcNow.AddHours(-2),
            EndedAt: DateTimeOffset.UtcNow.AddHours(-2).AddMinutes(3),
            DurationMs: 180_000,
            Status: "success",
            Turns: new[]
            {
                new TranscriptTurn("agent", "Hi! I'm the AI\u00b7Cloud sales assistant. What problem are you trying to solve today?", DateTimeOffset.UtcNow.AddHours(-2)),
                new TranscriptTurn("user", "I just want a quick demo. Can you point me to a video?", DateTimeOffset.UtcNow.AddHours(-2).AddSeconds(20)),
                new TranscriptTurn("agent", "Sure \u2014 the 90-second overview is at example.com/demo.", DateTimeOffset.UtcNow.AddHours(-2).AddSeconds(45)),
            }),
        new ConversationRecord(
            ConversationId: "conv_2026_09_15_004",
            AgentId: "agent_sales_001",
            StartedAt: DateTimeOffset.UtcNow.AddHours(-26),
            EndedAt: DateTimeOffset.UtcNow.AddHours(-26).AddMinutes(5),
            DurationMs: 300_000,
            Status: "failure",
            Turns: new[]
            {
                new TranscriptTurn("agent", "Hi! I'm the AI\u00b7Cloud sales assistant. What problem are you trying to solve today?", DateTimeOffset.UtcNow.AddHours(-26)),
                new TranscriptTurn("user", "Maybe later.", DateTimeOffset.UtcNow.AddHours(-26).AddSeconds(15)),
                new TranscriptTurn("agent", "No problem. Whenever you're ready, the docs are at example.com/docs.", DateTimeOffset.UtcNow.AddHours(-26).AddSeconds(40)),
            }),
    };

    private static readonly IReadOnlyList<ConversationRecord> _supportConversations = new[]
    {
        new ConversationRecord(
            ConversationId: "conv_2026_09_17_004",
            AgentId: "agent_support_002",
            StartedAt: DateTimeOffset.UtcNow.AddMinutes(-44),
            EndedAt: DateTimeOffset.UtcNow.AddMinutes(-44).AddMinutes(2),
            DurationMs: 120_000,
            Status: "success",
            Turns: new[]
            {
                new TranscriptTurn("agent", "Hi there \u2014 what can I help you fix today?", DateTimeOffset.UtcNow.AddMinutes(-44)),
                new TranscriptTurn("user", "My webhook 400 errors when I call too fast.", DateTimeOffset.UtcNow.AddMinutes(-44).AddSeconds(10)),
                new TranscriptTurn("agent", "Got it. There's a per-minute throttle \u2014 docs at example.com/rate-limits.", DateTimeOffset.UtcNow.AddMinutes(-44).AddSeconds(35)),
            }),
    };

    private static readonly IReadOnlyList<ConversationRecord> _onboardingConversations = new[]
    {
        new ConversationRecord(
            ConversationId: "conv_2026_09_16_012",
            AgentId: "agent_onboarding_003",
            StartedAt: DateTimeOffset.UtcNow.AddHours(-1),
            EndedAt: DateTimeOffset.UtcNow.AddHours(-1).AddMinutes(7),
            DurationMs: 420_000,
            Status: "success",
            Turns: new[]
            {
                new TranscriptTurn("agent", "Welcome aboard! Ready to set up your workspace?", DateTimeOffset.UtcNow.AddHours(-1)),
                new TranscriptTurn("user", "Yes, where do I start?", DateTimeOffset.UtcNow.AddHours(-1).AddSeconds(20)),
                new TranscriptTurn("agent", "Step 1: connect your data source. You can pick from the integrations list.", DateTimeOffset.UtcNow.AddHours(-1).AddSeconds(45)),
            }),
    };

    public Task<IReadOnlyList<Agent>> ListAgentsAsync(CancellationToken ct = default) =>
        Task.FromResult(_agents);

    public Task<Agent> GetAgentAsync(string agentId, CancellationToken ct = default)
    {
        var agent = _agents.FirstOrDefault(a => a.AgentId == agentId)
            ?? throw new ElevenLabsStudio.Core.Exceptions.ElevenLabsException(
                $"Mock agent '{agentId}' does not exist.",
                httpStatus: 404);
        return Task.FromResult(agent);
    }

    public Task<Agent> UpdateAgentAsync(string agentId, AgentUpdate update, CancellationToken ct = default)
    {
        var current = _agents.FirstOrDefault(a => a.AgentId == agentId)
            ?? throw new ElevenLabsStudio.Core.Exceptions.ElevenLabsException(
                $"Mock agent '{agentId}' does not exist.",
                httpStatus: 404);

        // Echo the merged state back without mutation. The VM treats
        // the response as the canonical post-update snapshot.
        var newNodes = update.WorkflowNodes ?? current.Workflow.Nodes;
        var newRaw = current.Workflow.RawJson;
        var snapshot = current with
        {
            Prompt = update.Prompt ?? current.Prompt,
            FirstMessage = update.FirstMessage ?? current.FirstMessage,
            VoiceId = update.VoiceId ?? current.VoiceId,
            Variables = update.Variables ?? current.Variables,
            Workflow = new Workflow(newNodes, newRaw),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<ConversationRecord>> ListConversationsAsync(
        string agentId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int pageSize = 100,
        string? cursor = null,
        CancellationToken ct = default)
    {
        IReadOnlyList<ConversationRecord> source = agentId switch
        {
            "agent_sales_001" => _salesConversations,
            "agent_support_002" => _supportConversations,
            "agent_onboarding_003" => _onboardingConversations,
            _ => Array.Empty<ConversationRecord>(),
        };
        return Task.FromResult(source);
    }

    public Task<ConversationRecord> GetConversationAsync(string conversationId, CancellationToken ct = default)
    {
        var all = _salesConversations
            .Concat(_supportConversations)
            .Concat(_onboardingConversations);
        var match = all.FirstOrDefault(c => c.ConversationId == conversationId)
            ?? throw new ElevenLabsStudio.Core.Exceptions.ElevenLabsException(
                $"Mock conversation '{conversationId}' does not exist.",
                httpStatus: 404);
        return Task.FromResult(match);
    }

    public Task<IReadOnlyList<Voice>> ListVoicesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Voice>>(new[]
        {
            new Voice("voice_aria", "Aria", "premade", null),
            new Voice("voice_onyx", "Onyx", "premade", null),
            new Voice("voice_chloe", "Chloe", "premade", null),
        });
}