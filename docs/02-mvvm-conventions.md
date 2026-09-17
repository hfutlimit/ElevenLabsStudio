# 02 · MVVM 规范

> 详细版见 AgentBoard 项目 19 `ElevenLabs Specs / 02 · MVVM 规范`。

## 硬约束

### View (`*.xaml` / `*.xaml.cs`)
- **禁止**任何业务逻辑（if / foreach / LINQ / await / HttpClient 全部不要）
- **禁止**在 code-behind 里 new ViewModel（用 CM view-model-first 绑定）
- code-behind 只能做 4 件事：`InitializeComponent()`、附加行为、`BehaviorBase<T>` 转发、`#if DEBUG` 调试钩子

### ViewModel (`*ViewModel.cs`)
- **禁止** `using System.Windows.*` / `using System.Windows.Controls.*`
- **禁止** `MessageBox.Show` / `OpenFileDialog` — 用 `IDialogService`
- ctor 只接受抽象接口，便于注入 Mock

### Service (`Core/Abstractions/` + `Infrastructure/`)
- 接口在 `Core/Abstractions/`，实现在 `Infrastructure/`
- 返回领域模型，不返回 ElevenLabs DTO
- 每个方法必须有 `CancellationToken` 参数
- 抛 `ElevenLabsException` / `ElevenLabsRateLimitException`，不抛 `HttpRequestException` 给 UI

## 命名

| 文件                              | 作用                          |
| --------------------------------- | ----------------------------- |
| `Views/ShellView.xaml`            | 顶级 Conductor 容器           |
| `ViewModels/ShellViewModel.cs`    | 持有子 VM 的 Conductor        |
| `Views/Agents/AgentListView.xaml` | 子页                          |
| `ViewModels/Agents/AgentListViewModel.cs` | 子页 VM               |

## 异步加载模式

```csharp
public bool IsBusy { get; private set; }
public string BusyMessage { get; private set; }
public BindableCollection<Agent> Agents { get; } = new();

public async Task LoadAsync()
{
    IsBusy = true;
    BusyMessage = "正在加载 Agent…";
    try { var items = await _client.ListAgentsAsync(); Agents.Clear(); Agents.AddRange(items); }
    finally { IsBusy = false; NotifyOfPropertyChange(() => BusyMessage); }
}
```