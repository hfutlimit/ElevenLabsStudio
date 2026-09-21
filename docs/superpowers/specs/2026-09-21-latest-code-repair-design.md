# ElevenLabsStudio Latest-Code Repair Design

## Goal

Make the current WPF application safe and usable with both mock data and the
current ElevenLabs API by repairing View/ViewModel integration, preventing
summary data from becoming an editable server baseline, aligning workflow and
conversation payloads with the live contracts, and removing known threading and
configuration faults.

This work preserves the existing Core / Infrastructure / WPF layering and does
not introduce a new application framework or a broad UI redesign.

## Scope

The repair covers the findings confirmed in the 2026-09-21 review:

1. Settings and Pull Agent actions/dialogs do not open through the current
   Caliburn.Micro integration.
2. List Agents summaries are treated as complete editable agents.
3. Workflow nodes use a lossy array representation and omit graph edges and
   unknown server fields.
4. Conversation filtering and transcript loading use outdated assumptions.
5. Agent update events may mutate WPF-bound state on a background thread.
6. `ELEVENLABS_API_KEY` does not bind to `ElevenLabsOptions.ApiKey`.
7. Prompt and first-message child ViewModels retain a construction-time Agent
   baseline after reload or push.
8. Existing local tests do not protect these contracts and the authenticated
   live test remains opt-in.

The work does not add new product features, redesign the visual layout, deploy
the application, use a real API key, or commit changes automatically.

## Design Decisions

### 1. View and action resolution

Keep Caliburn.Micro and the existing MS DI container. Align dialog View type
names and namespaces with Caliburn's default `ViewModels` to `Views` mapping:

- `SettingsViewModel` resolves to `Views.SettingsView`.
- `PullAgentDialogViewModel` resolves to `Views.Agents.PullAgentDialogView`.
- Existing `*TabViewModel` types continue resolving to the newly renamed
  `*TabView` types.

Critical buttons will have explicit, testable Caliburn action declarations
where convention-only wiring has already failed at runtime. The application
composition root will initialize the Caliburn WPF platform services required by
the manual bootstrap path before binding the shell. The resulting behavior must
be verified through the real executable: Settings and Pull Agent dialogs both
open, not merely through direct ViewModel method tests.

### 2. Agent summary versus editable detail

`ListAgentsAsync` remains the source for sidebar rows, but its wire DTO becomes a
dedicated summary DTO matching the official response. Summary fields map only
to values actually returned by that endpoint.

Selecting a sidebar row starts a cancellable `GetAgentAsync(agentId)` request.
The application creates `AgentDetailViewModel` only from that full response. A
stale selection request cannot replace the detail for a newer selection. If the
request fails, no editable detail is shown and the existing dialog abstraction
reports the error. Push is therefore impossible until a full server snapshot
has been obtained.

Mock mode follows the same selection path so the UI behavior is identical in
mock and real modes.

### 3. Lossless workflow handling

The infrastructure DTO represents workflow `nodes` and `edges` as keyed JSON
objects. The domain `Workflow` continues exposing the simplified node list used
by the current editor, while retaining the complete raw workflow document.

When node edits are pushed, the update builder clones the original raw workflow
and changes only fields owned by the current UI: node identity, type, and
display label/name. Existing edges, node-specific configuration, positions,
subgraphs, and unknown future fields are preserved. New nodes receive the
minimal supported object shape; removed nodes are removed only after rejecting
or removing edges that would otherwise reference missing nodes. Invalid raw
workflow JSON blocks workflow push with a user-visible validation error instead
of sending a lossy replacement.

If only prompt, first message, or variables changed, the workflow property is
omitted entirely from PATCH.

### 4. Conversation summary and detail

List requests use `call_start_after_unix`, `call_start_before_unix`, and a page
size clamped to 1 through 100. List DTOs contain summary fields only and do not
pretend to contain transcript turns.

Selecting a conversation starts a cancellable `GetConversationAsync` request.
Only the latest selected conversation may populate `Turns`; clearing selection
clears the transcript. Loading and failure state use the existing busy and
dialog abstractions. Transcript timestamps are calculated relative to the
conversation start time, not relative to the current wall clock.

### 5. UI-thread event delivery

Events that lead to `BindableCollection`, selection, or ViewModel property
changes are published or marshalled onto the WPF UI thread. Background work may
perform HTTP operations, but UI-bound mutations occur only after returning to
the dispatcher context. Event subscriptions remain symmetric with lifecycle
unsubscription.

### 6. Configuration mapping

The documented `ELEVENLABS_API_KEY` environment variable is explicitly mapped
to `ElevenLabs:ApiKey` after JSON configuration is loaded. Environment values
override JSON without being persisted back to `appsettings.json` merely because
the Settings dialog opens.

Existing hierarchical configuration remains available through the normal .NET
double-underscore form. Tests cover both the documented alias and precedence.

### 7. Mutable editor baselines

`SystemPromptTabViewModel` and `FirstMessageTabViewModel` keep a mutable server
baseline. `RefreshFrom` replaces that baseline after deciding whether local
text is dirty. A successful push also refreshes all child baselines from the
returned snapshot. Suggestion analysis always runs against the latest accepted
server snapshot.

### 8. Error behavior

- Detail and transcript requests are cancellable during rapid selection.
- Cancellation caused by a newer selection is silent.
- Authentication, validation, and HTTP failures use `IDialogService` and leave
  the last valid UI state intact.
- A workflow that cannot be updated losslessly is not sent.
- No exception from a fire-and-forget constructor load is allowed to escape
  unobserved; asynchronous loading is initiated from an explicit lifecycle or
  command boundary.

## Testing Strategy

Every behavior change follows red-green-refactor:

- HTTP serialization tests pin the official List Agents, Update Agent workflow,
  List Conversations, and Get Conversation shapes.
- ViewModel tests prove full-detail hydration, stale-selection cancellation,
  transcript detail loading, UI-thread event behavior, and refreshed baselines.
- Configuration tests prove `ELEVENLABS_API_KEY` reaches
  `ElevenLabsOptions.ApiKey` and overrides JSON.
- View-location tests prove the exact Settings and Pull dialog View types can be
  resolved.
- WPF E2E tests launch the Release executable and verify Settings and Pull Agent
  dialogs open through actual button invocation.
- Final verification runs the complete solution tests, Release build, E2E suite,
  package vulnerability scan, and C# leading-indentation scan.

The authenticated ElevenLabs smoke test remains opt-in because this task has no
authorization to use or store a real API key. Local passing results will not be
reported as live-service acceptance.

## Compatibility and Migration

No persisted user data format changes are required. The application remains on
.NET 10, WPF, Caliburn.Micro 5.0.258, Microsoft.Extensions.DependencyInjection,
and the existing JSON settings format. Unknown workflow fields are preserved to
remain forward-compatible with server-side schema additions.

## Acceptance Criteria

- The Release executable opens both Settings and Pull Agent dialogs from their
  visible buttons.
- An Agent detail editor is never constructed from a List Agents summary.
- Workflow round trips retain edges and unknown JSON fields.
- Conversation date filters and page size match the current official endpoint,
  and selecting a row loads its actual transcript detail.
- No background-thread handler mutates WPF-bound collections.
- `ELEVENLABS_API_KEY` populates `ElevenLabsOptions.ApiKey`.
- Reload and successful push update every child editor's server baseline.
- All non-live automated tests and all local UI E2E tests pass.
- No source file outside this repair scope is modified, and no commit is made.
