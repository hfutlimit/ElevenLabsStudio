# Editor Session Guardrails Design

**Status:** Draft for review
**Date:** 2026-09-22
**Scope:** ElevenLabs Studio desktop editor

## 1. Context

The editor already has a clear Shell → Agent list → Agent detail composition,
an `IDraftStore`, a reusable `IDialogService`, and per-tab ViewModels. The
remaining weaknesses are state propagation and interaction boundaries:

- `AgentDetailViewModel.IsDirty` is computed, but tab changes do not reliably
  notify the parent or the Shell.
- Agent switching saves an in-memory draft without asking the user.
- The close button bypasses the ViewModel and closes the WPF window directly.
- Push updates the remote Agent without a confirmation step.
- `ShellViewModel.IsBusy` computes child state but is not notified when the
  active detail or conversation ViewModel changes.
- Child editors are constructed with `NullLogger` instances.
- Settings persistence is implemented inside `SettingsViewModel`, mixing file
  I/O with interaction state.
- Workflow nodes can be added and removed, while edges and raw JSON remain
  read-only; the current banner does not describe that accurately.

The existing uncommitted English UI work is outside this design and must be
preserved.

## 2. Goals

1. Make unsaved state visible and predictable.
2. Require an explicit decision before switching Agent or closing the app.
3. Require confirmation before a remote Push and guarantee that a declined
   confirmation makes no network call.
4. Make Shell busy feedback observable for list, detail, and conversation
   operations.
5. Keep responsibilities aligned with the current Core / Infrastructure / UI
   layering and make the new behavior unit-testable.
6. Leave stable extension points for persistent drafts, keyboard commands, and
   richer Workflow editing.

## 3. Non-goals

- Persisting drafts across process restarts. `IDraftStore` remains process-local
  in this phase.
- Editing Workflow edges or the raw Workflow JSON.
- Replacing Caliburn.Micro or fully decoupling `ScreenBase` from Core.
- Introducing a global `EditorSessionCoordinator`.
- Adding a new localization framework or changing remote Agent content.
- Replacing the system MessageBox implementation in this phase.

## 4. Responsibility boundaries

### 4.1 Agent detail session

`AgentDetailViewModel` remains the single editor-session boundary for the four
tabs. It will:

- build one pending `AgentUpdate` from the current tab values;
- derive `IsDirty` from whether that update contains a change;
- subscribe to tab property and collection changes and notify
  `IsDirty`/`BusyMessage` consumers;
- expose a single draft save/discard surface;
- own Push confirmation and the remote update transaction;
- aggregate conversation loading state for the Shell.

The pending-update calculation is shared by `IsDirty` and `Push`, preventing
the dirty indicator and the actual request from drifting apart.

### 4.2 Agent navigation

`AgentListViewModel` owns the navigation transaction. A requested selection is
not committed until the current detail session has been resolved:

```text
request selection
  ├─ no dirty session → commit selection
  └─ dirty session → resolve Save / Discard / Cancel
       ├─ Save → SaveDraft, commit selection
       ├─ Discard → DiscardDraft, commit selection
       └─ Cancel → keep the previous selection
```

The ViewModel must detach from the old detail's events before disposing it and
attach to the new detail after creation. A `HasUnsavedChanges` property is
exposed for the Agent list header/indicator without adding UI state to the
Core `AgentSummary` record.

### 4.3 Application close

`ShellViewModel` exposes an asynchronous close guard. It delegates the active
Agent decision to `AgentListViewModel`, then returns whether the window may
close.

`ShellView` keeps only the WPF lifecycle bridge:

- the caption button calls `Close()`;
- the `Closing` event cancels the first close attempt, awaits the ViewModel
  guard, and re-enters `Close()` only after approval;
- a private re-entry flag prevents a second prompt.

The event handler is the only `async void` boundary; all decision logic stays
in testable ViewModels/services.

### 4.4 Dialog contract

`IDialogService.ConfirmAsync` is retained for two-button Push confirmation.
Add a UI-neutral `UnsavedChangesDecision` enum with `SaveDraft`, `Discard`,
and `Cancel`, plus one dialog method that returns that decision. The WPF
implementation maps Yes / No / Cancel to the enum and remains the only place
that knows about `MessageBoxResult`.

### 4.5 Busy state

No global coordinator is added. Busy state is propagated through the existing
composition:

- list operations set `AgentListViewModel.IsBusy` and `BusyMessage`;
- detail Push/Refresh/initialization set their own busy scope and message;
- detail forwards conversation busy changes;
- Shell subscribes to list and active-detail `PropertyChanged` events and
  explicitly raises `IsBusy` and `BusyMessage` when a child changes;
- all subscriptions are detached on detail replacement and disposal.

Operation messages use the current English UI vocabulary, for example
`Loading agents…`, `Loading conversations…`, `Saving changes…`, and
`Refreshing agent…`.

## 5. Code-quality changes

### 5.1 Logger injection

`AgentDetailViewModelFactory` receives `ILoggerFactory` and creates typed
loggers for System Prompt, First Message, Variables, Workflow, and
Conversations ViewModels. Tests use `NullLoggerFactory.Instance` at the
composition boundary; production code no longer hard-codes `NullLogger<T>` in
child construction.

### 5.2 Settings persistence boundary

Introduce `ISettingsStore` in the abstraction layer with a small
`AppSettingsSnapshot` value rather than exposing `ElevenLabsOptions` to the UI
ViewModel. The implementation owns JSON file access, configuration reload,
and cancellation. `SettingsViewModel` keeps only validation, status state, and
dialog interaction. API keys are never logged or included in exception text
created by the store.

### 5.3 Workflow wording

The Workflow view will describe the actual capability:

- nodes are editable through add/remove actions;
- connections are visualized but not edited;
- raw JSON is read-only;
- changes are applied through the existing Push action.

Full edge editing is a later feature with its own API and interaction design.

## 6. Push flow

1. Build the shared pending update.
2. If there are no changes, show the existing informational dialog and stop.
3. Ask `ConfirmAsync` for explicit approval, including the Agent name and the
   fields that will change.
4. If declined, return without setting busy state or calling the client.
5. Set the busy scope, call `UpdateAgentAsync`, apply the acknowledged snapshot,
   discard the stale draft, publish `AgentUpdatedEvent`, and show success.
6. On failure, keep the dirty state and show the existing error path.

## 7. Test contract

### Unit tests

- A prompt, first-message, variable, or Workflow edit raises the aggregate
  dirty notification.
- A clean session switches without prompting.
- Save decision stores a draft before switching.
- Discard decision removes the draft and switches.
- Cancel decision preserves the previous selection and does not dispose its
  detail ViewModel.
- Close guard returns true for a clean session and for Save/Discard, false for
  Cancel.
- Declined Push confirmation never calls `UpdateAgentAsync`.
- Push confirmation includes the selected Agent and changed fields.
- Shell raises busy bindings when the active detail or conversation operation
  changes.
- SettingsStore success reloads configuration; write failure leaves runtime
  options untouched and reports failure.
- Workflow copy and node add/remove behavior remain consistent.

### Build and UI checks

- Full `dotnet test ElevenLabsStudio.slnx --no-restore`.
- Release build with zero warnings/errors.
- UI smoke tests for the English labels, unsaved marker, Push confirmation,
  and close guard under deterministic Mock configuration.
- Live API validation remains separate and is not inferred from Mock UI tests.

## 8. Future extension points

- Replace `MemoryDraftStore` with a persisted implementation without changing
  navigation or close guards.
- Bind `Ctrl+S` to the existing Push command after the command surface is
  standardized.
- Add an edge-editing layer to `WorkflowTabViewModel` without changing Shell
  state handling.
- Move `ScreenBase` and Caliburn-specific types out of Core in a separate
  dependency-boundary refactor.

## 9. Acceptance criteria

The phase is complete when the editor never silently loses a dirty session on
Agent switch or application close, no declined Push reaches the API, Shell
busy feedback updates for all supported operations, the Workflow copy matches
its actual capabilities, and the new behavior is covered by focused unit tests
plus deterministic UI smoke coverage. Existing English localization changes
and local configuration remain intact.
