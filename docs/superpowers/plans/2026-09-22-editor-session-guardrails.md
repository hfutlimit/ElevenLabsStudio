# Editor Session Guardrails Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Agent editing explicit and recoverable by adding dirty-state propagation, navigation/close guards, Push confirmation, Shell busy feedback, and the agreed structural cleanup while preserving future extension points.

**Architecture:** Keep the existing Core / Infrastructure / WPF composition. `AgentDetailViewModel` remains the editor-session boundary, `AgentListViewModel` owns Agent navigation decisions, and `ShellViewModel` owns application-close and status-bar aggregation. Add only the small dialog/settings abstractions required by those boundaries; do not introduce a global coordinator or a Workflow edge editor.

**Tech Stack:** .NET 10, C# 14, WPF, Caliburn.Micro 5.0.258, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging, System.Text.Json, xUnit, FluentAssertions, NSubstitute, pytest, pywinauto.

**Spec:** `docs/superpowers/specs/2026-09-22-editor-session-guardrails-design.md`

## Global Constraints

- Preserve the user's uncommitted English UI changes and the ignored local ElevenLabs configuration.
- Do not commit or push implementation changes unless the user explicitly requests a separate commit/push action.
- For every production behavior change, write the focused failing test first, run it, then implement the smallest passing change.
- Use tabs for the complete leading-indentation prefix of every changed or added `.cs` file.
- Do not place an API key or remote Agent data in source, tests, logs, command output, or committed configuration.
- Keep Core free of WPF/System.Windows dependencies; keep HTTP and file-system details behind abstractions consumed by ViewModels.
- Do not call the live ElevenLabs API for acceptance; deterministic Mock UI runs and the opt-in integration test remain separate.
- Keep the current Workflow scope: nodes can be added/removed, edges are visualized only, and raw JSON remains read-only.

## Review Focus

- Canceling an Agent switch must restore the previous selection and must not dispose the active detail VM; covered by Task 3 navigation tests.
- Closing while a decision dialog is already open must not show a second prompt or re-enter the WPF close loop; covered by Task 5 Shell close tests.
- Declining Push confirmation must not set busy state or call `UpdateAgentAsync`; covered by Task 4 Push tests.
- A late conversation operation must not leave the Shell busy indicator stuck or overwrite a newer selection; covered by Task 5 busy propagation tests.
- A settings-file write failure must leave the live options unchanged and show failure feedback; covered by Task 6 settings-store tests.

---

### Task 1: Centralize editor dirty-state calculation and notification

**Files:**
- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/AgentDetailViewModel.cs`
- Modify: `src/ElevenLabsStudio/Views/AgentDetail/AgentDetailView.xaml`
- Modify: `src/ElevenLabsStudio/ViewModels/Agents/AgentListViewModel.cs`
- Modify: `src/ElevenLabsStudio/Views/Agents/AgentListView.xaml`
- Test: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentDetailViewModelTests.cs`
- Test: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentListViewModelTests.cs`

**Interfaces:**
- Produces `AgentDetailViewModel.BuildPendingUpdate()` (`AgentUpdate?`, public read-only helper) as the single change-calculation path used by `IsDirty` and `Push`.
- Produces `AgentListViewModel.HasUnsavedChanges`, derived from the active detail VM without modifying the Core `AgentSummary` record.

- [ ] **Step 1: Add failing dirty-notification tests**

Add tests that subscribe to `PropertyChanged`, edit `SystemPromptVm.Prompt`, add a variable, and add a Workflow node. Assert that `IsDirty` becomes true and that `nameof(AgentDetailViewModel.IsDirty)` is raised for each edit. Add a test that the pending update reports the same changed fields as `IsDirty`.

```csharp
[Fact]
public void Editing_any_tab_raises_the_aggregate_dirty_notification()
{
	var vm = Build(AgentSnapshot("initial"), Substitute.For<IElevenLabsClient>());
	var raised = new List<string?>();
	vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

	vm.SystemPromptVm.Prompt = "edited";

	vm.IsDirty.Should().BeTrue();
	raised.Should().Contain(nameof(AgentDetailViewModel.IsDirty));
}
```

Add an AgentList test that changes the active detail and expects
`HasUnsavedChanges` plus `PropertyChanged` to update.

- [ ] **Step 2: Run the focused red tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AgentDetailViewModelTests|FullyQualifiedName~AgentListViewModelTests"
```

Expected: the new tests fail because `IsDirty` is not notified by child changes and `HasUnsavedChanges` does not exist.

- [ ] **Step 3: Implement one pending-update calculation**

In `AgentDetailViewModel`, add a public read-only `AgentUpdate? BuildPendingUpdate()` that compares prompt, first message, variables, and Workflow nodes and returns `null` when unchanged. Change `IsDirty` to test that result. Update `Push()` to use the same result instead of repeating the four comparisons.

Subscribe to the four tab ViewModels' `PropertyChanged` events and to the Variables/Workflow collection changes. Each handler raises `IsDirty`; dispose all handlers in `Dispose()`.

- [ ] **Step 4: Add the visible dirty indicators**

Add an `Unsaved changes` badge to the Agent detail header bound to `IsDirty`, and a compact `HasUnsavedChanges` badge in the Agent list header. Keep existing names, bindings, colors, and layout resources intact.

- [ ] **Step 5: Run the focused green tests**

Run the same focused command from Step 2. Expected: all selected tests pass and the existing 78-test baseline remains unchanged outside the new assertions.

### Task 2: Add the unsaved-changes decision contract

**Files:**
- Create: `src/ElevenLabsStudio.Core/Abstractions/UnsavedChangesDecision.cs`
- Modify: `src/ElevenLabsStudio.Core/Abstractions/IDialogService.cs`
- Modify: `src/ElevenLabsStudio/Services/MaterialDialogService.cs`
- Test: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentListViewModelTests.cs`

**Interfaces:**
- Produce `UnsavedChangesDecision.SaveDraft`, `.Discard`, and `.Cancel`.
- Produce `IDialogService.ResolveUnsavedChangesAsync(string title, string message, CancellationToken ct = default)`.

- [ ] **Step 1: Add the enum and dialog call to the failing navigation tests**

Configure the substituted dialog in three tests to return SaveDraft, Discard, and Cancel respectively. The tests should exercise the public selection path and assert the corresponding draft-store/disposal/selection behavior. The project must fail to compile before the enum and method exist.

- [ ] **Step 2: Run the red compile/test check**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj --no-restore --filter FullyQualifiedName~AgentListViewModelTests
```

Expected: compile failure for the missing decision type and dialog method.

- [ ] **Step 3: Implement the UI-neutral contract**

Add the enum in Core and the method to `IDialogService`. In `MaterialDialogService`, map WPF Yes/No/Cancel to SaveDraft/Discard/Cancel through the existing Dispatcher. A missing dispatcher or any unexpected result must return Cancel.

- [ ] **Step 4: Run the contract compile check**

Run the focused command from Step 2. Expected: it compiles and the navigation tests now fail on the existing silent-switch behavior, not on the contract.

### Task 3: Make Agent switching an explicit transaction

**Files:**
- Modify: `src/ElevenLabsStudio/ViewModels/Agents/AgentListViewModel.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentListViewModelTests.cs`

**Interfaces:**
- Consume `IDialogService.ResolveUnsavedChangesAsync` from Task 2.
- Preserve `SelectAgentAsync(AgentSummary?, CancellationToken)` as the async operation used by existing callers; add an internal commit-only helper so the public request path can guard navigation first.
- Produce `AgentListViewModel.ResolveActiveUnsavedChangesAsync(CancellationToken)` returning `Task<bool>` for Shell close and selection callers.

- [ ] **Step 1: Pin Save / Discard / Cancel behavior**

Add three tests with an active dirty detail:

```csharp
[Theory]
[InlineData(UnsavedChangesDecision.SaveDraft, true)]
[InlineData(UnsavedChangesDecision.Discard, true)]
[InlineData(UnsavedChangesDecision.Cancel, false)]
public async Task Switching_agents_respects_the_unsaved_decision(
	UnsavedChangesDecision decision,
	bool shouldSwitch)
{
	// Build two summaries, make the first detail dirty, and return `decision`.
	// Assert the selected Agent and draft-store calls according to `shouldSwitch`.
}
```

The Cancel case must assert that the original `AgentDetailViewModel` is not disposed and that the old `SelectedAgent` is restored.

- [ ] **Step 2: Run the red navigation tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj --no-restore --filter FullyQualifiedName~AgentListViewModelTests
```

Expected: the new tests fail because selection is currently committed before any decision and every dirty switch is saved automatically.

- [ ] **Step 3: Implement the selection transaction**

Keep the requested summary separate from `_selectedAgent` until the decision completes. Save or discard the current draft according to the returned decision; on Cancel, raise `SelectedAgent` so WPF returns to the previous row and leave the current detail alive. Attach/detach the detail `PropertyChanged` handler when the active detail changes, and expose `HasUnsavedChanges` with notifications.

- [ ] **Step 4: Run the green navigation tests**

Run the focused command from Step 2. Expected: all Save/Discard/Cancel cases pass, including the existing stale-response tests.

### Task 4: Confirm Push and expose operation busy scopes

**Files:**
- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/AgentDetailViewModel.cs`
- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/ConversationsTabViewModel.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentDetailViewModelTests.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/ViewModels/ConversationsTabViewModelTests.cs`

**Interfaces:**
- Consume the existing `IDialogService.ConfirmAsync`.
- Produce English busy messages `Loading conversations…`, `Saving changes…`, and `Refreshing agent…` through existing `BusyMessage` bindings.

- [ ] **Step 1: Add failing Push confirmation and busy tests**

Add a test where `ConfirmAsync` returns false and assert `UpdateAgentAsync` was never called and `IsBusy` stayed false. Add a test where it returns true and assert the request is made with the changed fields and the busy message is set during the pending task. Add a conversation test that holds `ListConversationsAsync` pending and observes `IsBusy`/`BusyMessage`.

- [ ] **Step 2: Run the red tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AgentDetailViewModelTests|FullyQualifiedName~ConversationsTabViewModelTests"
```

Expected: the declined Push test fails because the client is currently called without confirmation, and busy-message assertions fail because operations do not set a stable message.

- [ ] **Step 3: Implement confirmation and busy scopes**

After `BuildPendingUpdate()` returns a non-null update, call `ConfirmAsync` with the Agent name and changed field names. Return immediately on false, before `IsBusy = true`. Set and clear the operation message in `try/finally` for Push, Refresh, detail initialization, and conversation loading. Preserve dirty state on failures.

- [ ] **Step 4: Run the green tests**

Run the focused command from Step 2 and verify the client call count, dirty state, and busy transitions.

### Task 5: Aggregate Shell busy state and guard application close

**Files:**
- Modify: `src/ElevenLabsStudio/ViewModels/ShellViewModel.cs`
- Modify: `src/ElevenLabsStudio/ShellView.xaml.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/ViewModels/ShellViewModelTests.cs` (create)
- Modify: `tests/ElevenLabsStudio.UnitTests/Views/ShellActionBindingTests.cs`

**Interfaces:**
- Produce `ShellViewModel.TryCloseAsync(CancellationToken ct = default)` returning `Task<bool>`.
- Consume `AgentListViewModel.HasUnsavedChanges`, `AgentListViewModel.ResolveActiveUnsavedChangesAsync(CancellationToken)`, and the active detail/conversation `PropertyChanged` events.

- [ ] **Step 1: Add close and busy regression tests**

Create `ShellViewModelTests` that builds the real Shell/AgentList composition with substituted client, dialog, clock, windows, and draft store. Assert `TryCloseAsync` returns false for Cancel and true for Save/Discard. Hold a detail or conversation request pending and assert the Shell raises `IsBusy` and `BusyMessage` notifications.

```csharp
[Fact]
public async Task TryCloseAsync_returns_false_when_unsaved_decision_is_cancel()
{
	// Build a dirty active detail and return UnsavedChangesDecision.Cancel.
	// Assert the result is false and the draft is untouched.
}
```

- [ ] **Step 2: Run the red Shell tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj --no-restore --filter FullyQualifiedName~ShellViewModelTests
```

Expected: compile failure for `TryCloseAsync`, followed by notification failures once the test type is available.

- [ ] **Step 3: Implement Shell subscriptions and close guard**

Add attach/detach helpers for the AgentList and active AgentDetail property events. Raise `IsBusy`, `BusyMessage`, `AgentDetail`, and `HasUnsavedChanges` when child state changes. Implement `TryCloseAsync` by delegating to the AgentList decision path.

In `ShellView.xaml.cs`, keep `Close()` as the button action but handle `Closing` with a re-entry flag: cancel the first event, await `TryCloseAsync`, then call `Close()` only when approved. Do not add file/network/business logic to code-behind.

- [ ] **Step 4: Run Shell and binding tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj --no-restore --filter "FullyQualifiedName~ShellViewModelTests|FullyQualifiedName~ShellActionBindingTests"
```

Expected: close decisions and status-bar notifications pass without regressing Settings/Pull Agent action binding.

### Task 6: Inject child loggers and extract settings persistence

**Files:**
- Create: `src/ElevenLabsStudio.Core/Domain/AppSettingsSnapshot.cs`
- Create: `src/ElevenLabsStudio.Core/Abstractions/ISettingsStore.cs`
- Create: `src/ElevenLabsStudio/Services/JsonSettingsStore.cs`
- Modify: `src/ElevenLabsStudio/ViewModels/SettingsViewModel.cs`
- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/AgentDetailViewModel.cs`
- Modify: `src/ElevenLabsStudio/ViewModels/AgentDetail/AgentDetailViewModelFactory.cs`
- Modify: `src/ElevenLabsStudio/App.xaml.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/ViewModels/SettingsViewModelTests.cs`
- Create: `tests/ElevenLabsStudio.UnitTests/Services/JsonSettingsStoreTests.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentDetailViewModelTests.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/ViewModels/AgentListViewModelTests.cs`
- Modify: `tests/ElevenLabsStudio.UnitTests/Views/ShellActionBindingTests.cs`

**Interfaces:**
- `AppSettingsSnapshot(bool Mock, string ApiKey, string BaseUrl)` is the UI-independent settings value.
- `ISettingsStore.SaveAsync(AppSettingsSnapshot snapshot, CancellationToken ct = default)` owns JSON write and configuration reload.
- `AgentDetailViewModel` and `AgentDetailViewModelFactory` consume `ILoggerFactory` for typed child loggers.

- [ ] **Step 1: Add failing settings-store and logger-construction tests**

Move the existing JSON round-trip and missing-file assertions into `JsonSettingsStoreTests`. Add SettingsViewModel tests that substitute `ISettingsStore`, assert the snapshot values passed to it, and assert a store exception leaves the live options untouched. Update factory construction tests to supply `NullLoggerFactory.Instance`.

- [ ] **Step 2: Run the red tests**

Run:

```powershell
dotnet test tests/ElevenLabsStudio.UnitTests/ElevenLabsStudio.UnitTests.csproj --no-restore --filter "FullyQualifiedName~SettingsViewModelTests|FullyQualifiedName~JsonSettingsStoreTests|FullyQualifiedName~AgentDetailViewModelTests"
```

Expected: compile failures for the new store/factory signatures and the moved persistence assertions.

- [ ] **Step 3: Implement `JsonSettingsStore`**

Move the current JSON tree read/replace/write logic into `JsonSettingsStore`, preserving unrelated configuration sections and honoring the existing optional path override for tests. Call `IConfigurationRoot.Reload()` only after a successful write. Pass the cancellation token to file operations.

- [ ] **Step 4: Simplify `SettingsViewModel`**

Inject `ISettingsStore`, remove direct `JsonNode`, `File`, and `IConfigurationRoot` dependencies, create an `AppSettingsSnapshot` from the bound values, and keep the current error/status/dialog behavior. Register `ISettingsStore` in `App.BuildServiceProvider`.

- [ ] **Step 5: Inject typed child loggers**

Pass `ILoggerFactory` from `AgentDetailViewModelFactory` into `AgentDetailViewModel`; construct child tabs with `CreateLogger<T>()`. Update all direct test constructors to use `NullLoggerFactory.Instance` rather than embedding `NullLogger<T>` inside production code.

- [ ] **Step 6: Run the green structural tests**

Run the focused command from Step 2, then the full unit suite. Expected: settings round-trip behavior and all existing Agent detail/factory tests pass.

### Task 7: Align Workflow copy with its actual capability

**Files:**
- Modify: `src/ElevenLabsStudio/Views/AgentDetail/WorkflowTabView.xaml`
- Modify: `tests/ElevenLabsStudio.E2ETests/test_smoke.py`

**Interfaces:**
- No domain/API interface changes. The current add/remove node commands and read-only edge/raw JSON behavior remain unchanged.

- [ ] **Step 1: Add a UI smoke assertion for capability wording**

Create a deterministic Workflow smoke test in `tests/ElevenLabsStudio.E2ETests/test_smoke.py` that selects the first Mock Agent, opens the Workflow tab, asserts the board exposes the text `Editable nodes; connections are read-only.`, and still exposes the Add node action.

- [ ] **Step 2: Run the red UI check**

Run the Workflow smoke test against the Release executable with `ElevenLabs__Mock=true` and a whitespace `ElevenLabs__TestAgentId` so the local ignored target-agent config cannot change the test dataset. Expected: the old `READ-ONLY GRAPH` wording fails the new assertion.

- [ ] **Step 3: Update only the copy**

Replace the read-only banner with concise English text that states nodes are editable, connections are visualized only, and Push applies the changes. Keep the Raw JSON label read-only and leave all bindings/events untouched.

- [ ] **Step 4: Run the green UI check**

Run the targeted Workflow smoke test and confirm the Add/Remove node interactions still work.

### Task 8: Full verification and handoff

**Files:**
- Verify only; no new production files.

- [ ] **Step 1: Run the full unit/integration test command**

```powershell
dotnet test ElevenLabsStudio.slnx --no-restore /p:UseSharedCompilation=false /nodeReuse:false
```

Expected: all unit tests pass; the opt-in live API test remains skipped unless explicitly enabled.

- [ ] **Step 2: Build Release**

```powershell
dotnet build src/ElevenLabsStudio/ElevenLabsStudio.csproj -c Release --no-restore /p:UseSharedCompilation=false /nodeReuse:false
```

Expected: zero warnings and zero errors.

- [ ] **Step 3: Run deterministic UI smoke tests**

```powershell
$env:ElevenLabs__Mock = 'true'
$env:ElevenLabs__TestAgentId = ' '
pytest -m e2e tests/ElevenLabsStudio.E2ETests/
$exit = $LASTEXITCODE
Remove-Item Env:ElevenLabs__Mock,Env:ElevenLabs__TestAgentId -ErrorAction SilentlyContinue
exit $exit
```

Expected: all UI smoke tests pass without modifying the ignored local settings file or calling the live API.

- [ ] **Step 4: Run static hygiene checks**

```powershell
git diff --check
rg -n --glob '!**/bin/**' --glob '!**/obj/**' "MessageBox\.Show|NullLogger<" src/ElevenLabsStudio src/ElevenLabsStudio.Core
```

Expected: no new `MessageBox.Show` outside `MaterialDialogService` and no production child `NullLogger` construction. Report any pre-existing match separately.

- [ ] **Step 5: Review the final diff**

Confirm only the agreed implementation files and the existing English localization changes are modified. Leave implementation changes uncommitted until the user explicitly requests commit/push.
