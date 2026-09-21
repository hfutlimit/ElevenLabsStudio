# ElevenLabsStudio Latest-Code Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair the confirmed View/ViewModel wiring, API-contract, cancellation, threading, configuration, and editor-baseline defects without redesigning the application.

**Architecture:** Preserve the Core / Infrastructure / WPF split. Add small domain summary types at the client boundary, keep HTTP JSON-shape logic inside Infrastructure, and make WPF ViewModels consume only complete editable snapshots. All asynchronous UI entry points become explicit, cancellable operations with stale-result guards.

**Tech Stack:** .NET 10, C# 14, WPF, Caliburn.Micro 5.0.258, Microsoft.Extensions.DependencyInjection, System.Text.Json, xUnit, FluentAssertions, NSubstitute, pytest, pywinauto.

**Spec:** `docs/superpowers/specs/2026-09-21-latest-code-repair-design.md`

## Global Constraints

- Preserve the user's already-staged `.gitignore` change; do not modify, unstage, or overwrite it.
- Do not create a commit. Record verification results and leave the working tree for the user to inspect.
- For every production behavior change, first add the focused test, run it, and observe the expected failure before editing production code.
- Use tabs for the complete leading-indentation prefix of every changed or added `.cs` file.
- Do not put a real ElevenLabs API key in source, tests, command output, or configuration. Do not call the live API.
- Treat the authenticated integration test as opt-in and excluded from local acceptance.
- Keep changes inside the files named below unless a compile error demonstrates a directly related generated/project-file adjustment is necessary.
- A passing focused test is not runtime acceptance: both dialogs must also be verified through the built Release executable.

## Review Focus

- A List Agents summary must never be passed to `AgentDetailViewModelFactory.Create`.
- A late agent or conversation response must never overwrite a newer selection.
- Workflow updates must retain edges, node-specific data, and unknown JSON fields; malformed source JSON must prevent a workflow request.
- WPF-bound collections and selection properties must only be mutated on the UI dispatcher.
- The documented `ELEVENLABS_API_KEY` alias must override JSON while standard `ElevenLabs__ApiKey` binding still works.
- Reload and push must replace each editor's accepted server baseline without overwriting dirty local text.
- Constructor-started tasks must be removed; every asynchronous call must have an awaited lifecycle or command boundary.

---

## Task 1: Restore Caliburn WPF platform, View, and action wiring

**Files:**

- Modify: `tests/ElevenLabsStudio.E2ETests/test_smoke.py`
- Create: `tests/ElevenLabsStudio.UnitTests/Views/ViewResolutionTests.cs`
- Modify: `src/ElevenLabsStudio/App.xaml.cs`
- Move and modify: `src/ElevenLabsStudio/Views/Settings/SettingsView.xaml` to `src/ElevenLabsStudio/SettingsView.xaml`
- Move and modify: `src/ElevenLabsStudio/Views/Settings/SettingsView.xaml.cs` to `src/ElevenLabsStudio/SettingsView.xaml.cs`
- Move and modify: `src/ElevenLabsStudio/Views/Agents/PullAgentDialog.xaml` to `src/ElevenLabsStudio/Views/Agents/PullAgentDialogView.xaml`
- Move and modify: `src/ElevenLabsStudio/Views/Agents/PullAgentDialog.xaml.cs` to `src/ElevenLabsStudio/Views/Agents/PullAgentDialogView.xaml.cs`
- Modify: `src/ElevenLabsStudio/ShellView.xaml`
- Modify: `src/ElevenLabsStudio/Views/Agents/AgentListView.xaml`

- [x] **Step 1: Pin the broken executable behavior**

Extend `test_smoke.py` so separate tests launch the Release executable, invoke the visible Settings gear and Pull Agent button, and assert that windows titled `设置` and `按 ID 拉取 Agent` appear. Keep the existing Settings assertion and add the Pull dialog assertion before changing WPF code.

- [x] **Step 2: Add exact View-location tests**

Create `ViewResolutionTests.cs` with assertions that the convention targets exist and are concrete WPF elements:

```csharp
[Theory]
[InlineData(typeof(SettingsViewModel), typeof(SettingsView))]
[InlineData(typeof(PullAgentDialogViewModel), typeof(PullAgentDialogView))]
public void View_type_matches_default_Caliburn_mapping(Type viewModelType, Type expectedViewType)
{
	var transformed = viewModelType.FullName!
		.Replace(".ViewModels.", ".Views.")
		.Replace("ViewModel", "View");

	transformed.Should().Be(expectedViewType.FullName);
	typeof(UIElement).IsAssignableFrom(expectedViewType).Should().BeTrue();
}
```

For `SettingsViewModel`, place `SettingsView` in namespace `ElevenLabsStudio.Views` even though its physical XAML file sits at the project root; this yields `ElevenLabsStudio.Views.SettingsView`, the exact default mapping.

- [x] **Step 3: Run the red checks**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj -c Release --no-restore --filter FullyQualifiedName~ViewResolutionTests
pytest -m e2e tests/ElevenLabsStudio.E2ETests/test_smoke.py -q
```

Expected: the unit project fails to compile because the renamed View types do not exist; the Settings E2E test times out. Record both failures.

- [x] **Step 4: Initialize Caliburn before binding the shell**

In `App.OnStartup`, before `BuildServiceProvider` or `ViewModelBinder.Bind`, install the WPF platform pieces used by `BootstrapperBase`:

```csharp
PlatformProvider.Current = new XamlPlatformProvider();
AssemblySourceCache.Install();
```

After building the provider, connect Caliburn's service locator to MS DI:

```csharp
IoC.GetInstance = (service, key) => _serviceProvider.GetRequiredService(service);
IoC.GetAllInstances = service => _serviceProvider.GetServices(service);
IoC.BuildUp = instance => instance;
```

Keep the existing assembly registration. Remove the custom `ViewLocator.GetOrCreateViewType` override only after the locator tests and executable prove the MS DI-backed locator works; otherwise retain it as a narrow activation fallback.

- [x] **Step 5: Rename the Views and make critical actions explicit**

Update XAML `x:Class`, code-behind namespace/class names, and generated constructor references to the exact types from Step 2. On the Settings gear, Pull Agent, dialog Save/Cancel, and dialog Confirm/Cancel controls, use explicit declarations such as:

```xml
cal:Message.Attach="[Event Click] = [Action OpenSettingsAsync()]"
```

Use the actual matching method name from each ViewModel. Do not remove meaningful `x:Name` values that tests or UI automation use.

- [x] **Step 6: Run focused green checks and executable proof**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj -c Release --no-restore --filter FullyQualifiedName~ViewResolutionTests
dotnet build src/ElevenLabsStudio/ElevenLabsStudio.csproj -c Release --no-restore
pytest -m e2e tests/ElevenLabsStudio.E2ETests/test_smoke.py -q
```

Expected: View-location tests pass and both dialogs open from the real process. If either dialog still fails, stop and inspect binding/dispatcher logs before proceeding; do not add unrelated workarounds.

## Task 2: Separate Agent summaries from editable detail snapshots

**Files:**

- Create: `src/ElevenLabsStudio.Core/Domain/AgentSummary.cs`
- Modify: `src/ElevenLabsStudio.Core/Abstractions/IElevenLabsClient.cs`
- Modify: `src/ElevenLabsStudio.Infrastructure/RuntimeClient.cs`
- Modify: `src/ElevenLabsStudio.Infrastructure/Mock/MockElevenLabsClient.cs`
- Modify: `src/ElevenLabsStudio.Infrastructure/Http/Dto/ElevenLabsDto.cs`
- Modify: `src/ElevenLabsStudio.Infrastructure/Http/Mapping/Mapping.cs`
- Modify: `src/ElevenLabsStudio.Infrastructure/Http/ElevenLabsHttpClient.cs`
- Modify: `src/ElevenLabsStudio/ViewModels/Agents/AgentListViewModel.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/Integration/ElevenLabsHttpClientIntegrationTests.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/Mock/MockElevenLabsClientTests.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/Infrastructure/RuntimeClientTests.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentListViewModelTests.cs`

- [x] **Step 1: Add contract and selection tests**

Replace the unrealistic List Agents fixture with summary-only JSON containing `agent_id`, `name`, top-level `voice_id`, `tags`, and `created_at_unix_secs`. Assert that `ListAgentsAsync` returns `AgentSummary` values and does not require `conversation_config`.

Add ViewModel tests proving:

- initial list loading does not call the detail factory until `GetAgentAsync` completes;
- `GetAgentAsync` receives the selected summary ID;
- the factory receives the full `Agent` returned by `GetAgentAsync`;
- a failed detail fetch clears the editable detail and reports an error;
- when selection B completes before selection A, A's later completion cannot replace B;
- selecting null cancels the in-flight request and clears the detail.

- [x] **Step 2: Run the red tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~ElevenLabsHttpClientIntegrationTests|FullyQualifiedName~AgentListViewModelTests|FullyQualifiedName~MockElevenLabsClientTests|FullyQualifiedName~RuntimeClientTests"
```

Expected: summary deserialization and detail hydration tests fail because `ListAgentsAsync` still returns full `Agent` instances and selection constructs detail directly.

- [x] **Step 3: Introduce the summary boundary**

Add the domain type and change the client contract:

```csharp
public sealed record AgentSummary(
	string AgentId,
	string Name,
	string? VoiceId,
	DateTimeOffset? CreatedAt);

Task<IReadOnlyList<AgentSummary>> ListAgentsAsync(CancellationToken ct = default);
```

Add `ElevenLabsAgentSummaryDto`; change only the list-response collection to this DTO and add `MapToAgentSummary`. Keep `GetAgentAsync` mapped through the full `ElevenLabsAgentDto`. Update Runtime and Mock clients so mock mode returns summaries but `GetAgentAsync` still returns complete snapshots.

- [x] **Step 4: Make detail hydration cancellable and stale-safe**

Change `AgentListViewModel.Agents` and `SelectedAgent` to `AgentSummary`. Add a selection generation plus `CancellationTokenSource`:

```csharp
private CancellationTokenSource? _selectionCts;
private long _selectionGeneration;

internal async Task SelectAgentAsync(AgentSummary? summary)
```

The method must save the previous dirty draft, cancel/dispose the prior request, clear detail when selection is null, await `GetAgentAsync`, compare both generation and selected ID, and only then call `_detailFactory.Create(fullAgent)`. Catch only selection-owned `OperationCanceledException` silently; route service failures through `IDialogService` and leave `AgentDetail` null.

The property setter may start one guarded wrapper because WPF selection binding is synchronous, but that wrapper must catch and log every exception. Tests must call and await `SelectAgentAsync` directly. `LoadAsync` must await auto-selection rather than relying on the property setter.

When a pulled full agent is added to the sidebar, convert it to `AgentSummary`; on `AgentUpdatedEvent`, update the matching summary fields without rebuilding detail from the summary.

- [x] **Step 5: Run focused green checks**

Run the command from Step 2. Expected: all selected tests pass, including the out-of-order completion test.

## Task 3: Preserve complete workflow JSON during updates

**Files:**

- Modify: `src/ElevenLabsStudio.Core/Domain/AgentUpdate.cs`
- Modify: `src/ElevenLabsStudio.Infrastructure/Http/Dto/ElevenLabsDto.cs`
- Modify: `src/ElevenLabsStudio.Infrastructure/Http/Mapping/Mapping.cs`
- Create: `src/ElevenLabsStudio.Infrastructure/Http/Mapping/WorkflowJsonUpdater.cs`
- Modify: `src/ElevenLabsStudio.Infrastructure/Http/ElevenLabsHttpClient.cs`
- Modify: `src/ElevenLabsStudio.Infrastructure/Mock/MockElevenLabsClient.cs`
- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/AgentDetailViewModel.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/Mapping/AgentMappingTests.cs`
- Create: `tests/ElevenLabsStudio.UnitTests/Mapping/WorkflowJsonUpdaterTests.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/Integration/ElevenLabsHttpClientIntegrationTests.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/Mock/MockElevenLabsClientTests.cs`

- [x] **Step 1: Pin object-keyed workflow parsing and lossless output**

Use a full-agent fixture shaped as:

```json
{
  "workflow": {
    "nodes": {
      "start": { "type": "start", "label": "Greeting", "position": { "x": 10, "y": 20 }, "future": true },
      "answer": { "type": "conversation", "label": "Answer" }
    },
    "edges": {
      "edge-1": { "source": "start", "target": "answer", "condition": "always" }
    },
    "future_graph_field": { "keep": 1 }
  }
}
```

Assert mapping exposes both simplified nodes and byte-equivalent semantic raw JSON. Add updater tests showing a renamed node changes only its owned label/name field, while `position`, `future`, `edges`, edge metadata, and `future_graph_field` remain. Add tests that malformed raw JSON and a removed node still referenced by an edge throw `WorkflowUpdateException` before HTTP dispatch.

Update the wire PATCH assertion so `workflow.nodes` and `workflow.edges` are JSON objects, not a nodes array.

- [x] **Step 2: Run the red tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~AgentMappingTests|FullyQualifiedName~WorkflowJsonUpdaterTests|FullyQualifiedName~ElevenLabsHttpClientIntegrationTests|FullyQualifiedName~MockElevenLabsClientTests"
```

Expected: object-keyed nodes cannot map and the current PATCH drops edges/unknown fields.

- [x] **Step 3: Carry a workflow edit with its original raw document**

Change the update record from `WorkflowNodes` to `Workflow`:

```csharp
public sealed record AgentUpdate(
	string? Prompt = null,
	string? FirstMessage = null,
	string? VoiceId = null,
	IReadOnlyList<Variable>? Variables = null,
	Workflow? Workflow = null);
```

Update `IsEmpty` and callers. In `AgentDetailViewModel`, include `new Workflow(WorkflowVm.Nodes.ToList(), Agent.Workflow.RawJson)` only when node edits differ from the accepted baseline. Non-workflow edits must leave the property null.

- [x] **Step 4: Parse and update workflow through `JsonNode`**

Deserialize the workflow property as `JsonObject` or clone the `JsonElement`; derive domain nodes by enumerating the `nodes` object's properties, using the property key as the canonical node ID. Serialize the complete workflow object into `Workflow.RawJson`.

Implement:

```csharp
internal static JsonObject Apply(Workflow edit)
```

It must parse and deep-clone `RawJson`, require object-valued `nodes` and `edges`, update only node ID/type/display label fields, preserve unowned fields, add a minimal object for a new node, remove deleted nodes only when no edge refers to them, and throw `WorkflowUpdateException` otherwise. Put the exception in Infrastructure or reuse a Core validation exception only if it has no JSON dependency.

Have `ElevenLabsHttpClient.UpdateAgentAsync` call the updater before creating/sending the request so invalid workflow state makes zero HTTP calls. The mock client must exercise the same logical edit behavior and return a snapshot with the preserved raw document.

- [x] **Step 5: Run focused green checks**

Run the command from Step 2. Expected: all mapping, updater, mock, and HTTP wire tests pass.

## Task 4: Use current conversation list/detail contracts and stale-safe selection

**Files:**

- Modify: `src/ElevenLabsStudio.Infrastructure/Http/Dto/ElevenLabsDto.cs`
- Modify: `src/ElevenLabsStudio.Infrastructure/Http/Mapping/Mapping.cs`
- Modify: `src/ElevenLabsStudio.Infrastructure/Http/ElevenLabsHttpClient.cs`
- Modify: `src/ElevenLabsStudio.Infrastructure/Mock/MockElevenLabsClient.cs`
- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/ConversationsTabViewModel.cs`
- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/AgentDetailViewModel.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/Integration/ElevenLabsHttpClientIntegrationTests.cs`
- Create: `tests/ElevenLabsStudio.UnitTests/ViewModels/ConversationsTabViewModelTests.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/Mock/MockElevenLabsClientTests.cs`

- [x] **Step 1: Add list/detail wire tests**

Make the list fixture omit `transcript` and assert returned summaries have empty `Turns`. Capture the request URI and assert exact names `call_start_after_unix` and `call_start_before_unix`; assert a requested page size above 100 is emitted as `page_size=100`.

Add a Get Conversation fixture with transcript turn `time_in_call_secs` values and assert turn timestamps equal `StartedAt + offset`, independent of `DateTimeOffset.UtcNow`.

- [x] **Step 2: Add ViewModel race and error tests**

Create tests proving:

- construction performs no client call;
- `LoadAsync` populates only conversation summaries;
- selecting a row calls `GetConversationAsync` and then fills `Turns`;
- clearing selection clears turns and cancels the request;
- selecting B after A prevents A's late response from replacing B's transcript;
- selection-owned cancellation is silent, while an HTTP error is shown and the last valid transcript remains.

- [x] **Step 3: Run the red tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~ConversationsTabViewModelTests|FullyQualifiedName~ElevenLabsHttpClientIntegrationTests|FullyQualifiedName~MockElevenLabsClientTests"
```

Expected: URI assertions, summary behavior, relative timestamps, and selection hydration fail.

- [x] **Step 4: Split DTOs and mappings**

Create `ElevenLabsConversationSummaryDto` for list responses and `ElevenLabsConversationDetailDto` for the detail endpoint. Implement `MapToConversationSummary` with empty turns and `MapToConversationDetail` with:

```csharp
Timestamp: startedAt.AddSeconds(turn.TimeInCallSeconds ?? 0)
```

Clamp page size with `Math.Clamp(pageSize, 1, 100)` and rename query parameters to the current API names.

- [x] **Step 5: Remove constructor loading and hydrate selection explicitly**

Delete `_ = ReloadAsync()` from `ConversationsTabViewModel`. Rename or retain `ReloadAsync` as the explicit list load method and call it from the owning `AgentDetailViewModel` lifecycle using an awaited override such as `OnActivateAsync(CancellationToken)`; do not start an unobserved task from `OnViewLoaded`.

Add a conversation selection CTS/generation and an awaitable `SelectConversationAsync(ConversationRecord? summary)`. The synchronous setter may invoke one fully guarded wrapper for binding, while tests call the awaitable method. Replace turns only after confirming generation and ID still match. Keep the previous valid transcript on non-cancellation failure.

In mock mode, return copies with `Turns = Array.Empty<TranscriptTurn>()` from list calls and full records from detail calls.

- [x] **Step 6: Run focused green checks**

Run the command from Step 3. Expected: all selected tests pass.

## Task 5: Marshal event-driven state changes to the UI thread

**Files:**

- Modify: `src/ElevenLabsStudio/ViewModels/Agents/AgentListViewModel.cs`
- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/AgentDetailViewModel.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentListViewModelTests.cs`
- Create: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentDetailViewModelTests.cs`

- [x] **Step 1: Add event marshalling tests**

Install a recording `IPlatformProvider` in the test fixture or assert the event aggregator's marshal delegate. Prove that delivering `AgentUpdatedEvent` from a worker thread schedules `HandleAsync` on the UI provider before it changes `Agents`, `SelectedAgent`, `Agent`, or child ViewModels.

- [x] **Step 2: Run the red tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~AgentListViewModelTests|FullyQualifiedName~AgentDetailViewModelTests"
```

Expected: the event handler runs on the publisher thread because both subscribers currently use `SubscribeOnPublishedThread`.

- [x] **Step 3: Use Caliburn's UI-thread contract**

Change both subscriptions to:

```csharp
_events.SubscribeOnUIThread(this);
```

Change UI-state event publications to `PublishOnUIThreadAsync`. HTTP work remains asynchronous, but collection/property changes after awaits must execute on the WPF dispatcher captured by `XamlPlatformProvider`. Keep existing symmetric unsubscription and extend it to the detail ViewModel lifecycle if it currently subscribes multiple times.

- [x] **Step 4: Run focused green checks**

Run the command from Step 2. Expected: both event marshalling suites pass without cross-thread collection access.

## Task 6: Map the documented API-key environment variable

**Files:**

- Create: `src/ElevenLabsStudio/Configuration/ElevenLabsConfiguration.cs`
- Modify: `src/ElevenLabsStudio/App.xaml.cs`
- Create: `tests/ElevenLabsStudio.UnitTests/Configuration/ElevenLabsConfigurationTests.cs`

- [x] **Step 1: Add alias and precedence tests**

Test a configuration builder with JSON-like in-memory value `ElevenLabs:ApiKey=json-key` and an injected environment reader returning `env-key` for `ELEVENLABS_API_KEY`. Assert the bound option is `env-key`. Add a second test where the alias is absent and standard hierarchical configuration still supplies `ElevenLabs:ApiKey`.

Use an injected `Func<string, string?>` so tests do not mutate process-global environment variables in parallel.

- [x] **Step 2: Run the red tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj -c Release --no-restore --filter FullyQualifiedName~ElevenLabsConfigurationTests
```

Expected: the helper does not exist and the documented alias is not mapped.

- [x] **Step 3: Add a narrow configuration helper**

Implement a helper that builds normal JSON/environment providers first, then overlays only the documented alias:

```csharp
public static IConfigurationBuilder AddElevenLabsApiKeyAlias(
	this IConfigurationBuilder builder,
	Func<string, string?> readEnvironment)
```

If `ELEVENLABS_API_KEY` is non-empty, add `ElevenLabs:ApiKey` through an in-memory collection last. In `App`, call normal `.AddEnvironmentVariables()` so `ElevenLabs__ApiKey` continues to work, then call the alias helper. Do not write the alias value back to `appsettings.json` during startup or Settings initialization.

- [x] **Step 4: Run focused green checks**

Run the command from Step 2. Expected: alias, precedence, and fallback tests pass.

## Task 7: Refresh mutable editor baselines after reload and push

**Files:**

- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/SystemPromptTabViewModel.cs`
- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/FirstMessageTabViewModel.cs`
- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/AgentDetailViewModel.cs`
- Create: `tests/ElevenLabsStudio.UnitTests/ViewModels/EditorBaselineTests.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentDetailViewModelTests.cs`

- [x] **Step 1: Add baseline regression tests**

Test two successive `RefreshFrom` calls:

1. an untouched editor adopts server snapshot B;
2. after B becomes the accepted baseline, a local edit is preserved when snapshot C arrives;
3. suggestion analysis receives snapshot B/C rather than constructor snapshot A;
4. successful push refreshes prompt, first message, variables, workflow, and the aggregate `IsDirty` flag from the returned snapshot.

- [x] **Step 2: Run the red tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~EditorBaselineTests|FullyQualifiedName~AgentDetailViewModelTests"
```

Expected: the second refresh and suggestion assertions fail because both text editors retain constructor-time `_agent`.

- [x] **Step 3: Replace construction-time baselines**

Make the prompt and first-message `_agent` fields mutable. In `RefreshFrom`, first calculate whether the current text is dirty relative to the old baseline, then assign the new baseline, then replace displayed text only when it was clean. Suggestion commands must analyze against the new field.

Extract one `ApplyServerSnapshot(Agent snapshot)` method in `AgentDetailViewModel` that updates `Agent`, calls every child `RefreshFrom`, updates property notifications, and is used by reload, successful push, and the matching event handler. Do not duplicate partial refresh behavior across those paths.

- [x] **Step 4: Run focused green checks**

Run the command from Step 2. Expected: all baseline and detail tests pass.

## Task 8: Remove remaining unobserved loads and complete verification

**Files:**

- Modify: `src/ElevenLabsStudio/ViewModels/Agents/AgentListViewModel.cs`
- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/AgentDetailViewModel.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentListViewModelTests.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentDetailViewModelTests.cs`

- [x] **Step 1: Add lifecycle tests**

Assert constructing `AgentListViewModel` performs no client call. Exercise its explicit activation/load boundary and assert one list request. Do the same for conversation loading through `AgentDetailViewModel`. Assert activation cancellation reaches the client token.

- [x] **Step 2: Run the red tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~AgentListViewModelTests|FullyQualifiedName~AgentDetailViewModelTests"
```

Expected: the Agent list client is called during construction until the constructor launch is removed.

- [x] **Step 3: Move initial loading to awaited lifecycle methods**

Remove `_ = LoadAsync()` from the Agent list constructor. Use `OnActivateAsync(CancellationToken)` or an explicit parent-awaited `InitializeAsync(CancellationToken)` exactly once. Pass the lifecycle token through list, auto-selection, and event publication calls. Ensure repeated activation does not duplicate data or subscriptions.

- [x] **Step 4: Run all automated checks**

Run from the repository root:

```powershell
dotnet test ElevenLabsStudio.slnx -c Release --no-restore --nologo
dotnet build ElevenLabsStudio.slnx -c Release --no-restore --nologo
pytest -m e2e tests/ElevenLabsStudio.E2ETests/ -q
dotnet list ElevenLabsStudio.slnx package --vulnerable --include-transitive
```

Expected: all non-live .NET tests pass, the authenticated test remains explicitly skipped, the Release build passes, all three E2E tests pass, and no vulnerable packages are reported.

- [x] **Step 5: Scan C# indentation and patch hygiene**

Run:

```powershell
$changedCs = git status --short | ForEach-Object { $_.Substring(3) } | Where-Object { $_ -like '*.cs' }
foreach ($file in $changedCs) {
	$bad = Select-String -Path $file -Pattern '^( +|\t+ +)\S'
	if ($bad) { $bad }
}
git diff --check
git status --short
```

Expected: no changed C# line has spaces or tab-plus-space in its leading indentation prefix; `git diff --check` is silent; `.gitignore` remains staged and untouched; implementation/spec/plan files are uncommitted.

- [x] **Step 6: Inspect the final diff against scope**

Review `git diff --stat`, `git diff`, and `git diff --cached -- .gitignore`. Confirm every production change is protected by a test, there are no real credentials, no unrelated source files changed, and no commit was created. Report unit/build/E2E/vulnerability evidence separately from the skipped live-service acceptance.
