# 界面评审：原型 → 现状 落地差距

日期：2026-09-24
评审对象：`src/ElevenLabsStudio/`（WPF + MaterialDesignThemes 5.3 + Caliburn.Micro）
参照原型：`docs/prototypes/2026-09-24-redesign-shell.html`（commit `f72eec4`）、`docs/prototypes/2026-09-24-redesign-shell-dark.html`（commit `13b6fd2`）

## 结论先说

根因不是配色。今天上午的两份原型已经把当前界面里最不满意的每一点都解了——内容列收窄、层级靠发丝线、同步状态轨、下划线 tab、底部固定操作条。但原型从未落地：之后的 `de425da → 5d586e2` 五个提交都是在旧结构上打补丁，`DesignSystem.xaml` 里没有任何一条来自原型的结构性样式。当前状态是"扁平化做了一半"——tokens 已经换过，骨架还是旧的。

下面的每条问题都对应到具体行号，改法一句话给出。

---

## P0 · 一眼可见

### 1. 内容列拉满整屏

**证据**
- `ShellView.xaml:138-207` — Detail `Border` 用 `Width="*"` 铺满右侧，无 `MaxWidth`。
- `AgentDetailView.xaml:6` — UserControl 只有一个 `Margin="20,16,20,16"`，宽度全靠外层。
- `SystemPromptTabView.xaml:5-10` / `FirstMessageTabView.xaml:5-9` — `RowDefinition Height="*"` + 无宽度上限，等宽正文单行 100+ 字符。
- 1080p 窗口下"363 chars"和"Local suggestions"实测相距 ≈1200px。

**改法**
Detail 面板内所有编辑/正文列加 `MaxWidth="800"` 左对齐，`HorizontalContentAlignment="Left"`。这条一改，其他四条的观感都会立刻改善一半。

### 2. 三层白卡不可分

**证据**
- `DesignSystem.xaml:9-13` 四档表面色：`App.Canvas #F2F4F8`、`App.Surface #FFFFFF`、`App.SurfaceAlt #F7F8FB`、`App.SurfaceHover #EEF1F6`。
- 相邻明度差全部 <3%（YIQ：242/255/247/238），肉眼无法分辨。
- 三层卡（外 `App.ElevatedCard` → 头 `App.SectionCard` → 编辑器 `SurfaceAlt` 底）叠在一起就是"一片白"。

**改法**
层与层的关系靠 **1px `App.Line #E3E6EB`（原型 line）** 描边建立，而不是靠背景明度。合并 `SurfaceAlt`/`SurfaceHover` 为单一 `SurfaceTint`，只用在 hover；inset 面板一律 `Background=App.Surface` + `BorderBrush=App.Line` + `BorderThickness=1`。

### 3. 统计与标题被拉到窗口两端

**证据**
- `SystemPromptTabView.xaml:44-69` — 用 `DockPanel` + `LastChildFill=True`，`PromptLength` dock 到 Right，`Local suggestions` 自然撑到 Left。宽度一大就变成两端对峙。
- `FirstMessageTabView.xaml:43-68` — 同一份代码复制粘贴，同样问题。

**改法**
把 `PromptLength` 从 dock 右端挪到字段标题同一行的紧邻右侧（`StackPanel Orientation=Horizontal`），并在 `Suggestions.Count==0` 时把整行 `Collapsed`。字数是"字段级"信息，不该出现在"区域级"的右端。

### 4. 编辑器没有边界，默认态读作"渲染失败的空面板"

**证据**
- `SystemPromptTabView.xaml:13-26` — `BorderBrush="Transparent"`，只有 `IsKeyboardFocusWithin=True` 时才画 `App.AccentBorder`。
- `FirstMessageTabView.xaml:12-25` — 同一份代码复制。
- 用户截图里的空态就是这个：一片 `SurfaceAlt` 灰底，四边无描边。

**改法**
默认态 `BorderBrush="{StaticResource App.Line}"`；聚焦时才换成 `App.AccentBorder`。同时把 `FirstMessageTabView` 和 `SystemPromptTabView` 的编辑器 Border+TextBox 抽成 `App.InsetEditor` 样式，两处复用，不再复制。

### 5. Tab 视觉体系未统一

**证据**
- `DesignSystem.xaml:199-232` — `App.TabItem` 是"胶囊 + `AccentSoft` 底色 + `AccentBorder` 描边"，跟原型（下划线 + 底部 `1px App.Line` 分隔线）不是一回事。
- 六个 tab（`AgentDetailView.xaml:102-119`）现在像 6 个 chip，选中态像被 hover 的按钮。

**改法**
`App.TabItem` 改为：默认透明底 + 底部 2px 透明条；选中 = 文字 `App.Accent` + 底部 2px `App.Accent` 条；`TabControl` 外层加 `Border BorderBrush=App.Line BorderThickness=0,0,0,1`。删掉 `AccentSoft` 底色。

---

## 真实缺陷（不是审美）

### 6. 选中态有两套指示器同时画

**证据**
- `AgentListView.xaml:85-92` — `ControlTemplate` 内的 `SelectionBar`：`Width=3, Height=20, 居中`。
- `AgentListView.xaml:123-129` — `DataTemplate` 里又画了一条：`Width=3, VerticalAlignment=Stretch, Margin=-10,-2,0,-2`。
- 选中时两条同时可见，一条 20 高居中、一条撑满整行，位置错开 10px。

**改法**
删掉 `DataTemplate` 里那条（`AgentListView.xaml:121-129`）。模板条留在 `ControlTemplate` 里，跟 hover/selected 触发器同步。

### 7. 最大化后圆角和外边距没清掉

**证据**
- `ShellView.xaml:31-36` — 根 `Border` 固定 `CornerRadius="14"` + `Margin="8"` + `App.WindowShadow`。
- `ShellView.xaml.cs:100-106` — `UpdateMaximizeIcon()` 只换 `MaximizeIcon.Kind`，从不改 `Border.Margin` / `Border.CornerRadius` / `Border.Effect`。
- 截图里那圈深色描边就是这个。

**改法**
`OnStateChanged` 里同步 `RootBorder.Margin = WindowState==Maximized ? new(0) : new(8)` 和 `CornerRadius` / `Effect`。或者直接把 `RootBorder` 加进 `x:Name` 并在触发器里改。

### 8. `AllowsTransparency=True` 叠 `WindowChrome`

**证据**
- `ShellView.xaml:14-17` — `WindowStyle="None"` + `AllowsTransparency="True"` + `Background="Transparent"`。
- `ShellView.xaml:23-29` — 同时挂 `WindowChrome`，`GlassFrameThickness="0"`。
- 已知长期风险项，仓库里没有对应 issue/roadmap 记录（我上一轮回复里"design-review.md §5.3 / roadmap 第 17 项"是记错来源，请以本节为准）。副作用：DWM 软件合成、拖动帧率下降、任务栏预览边缘发虚。

**改法**
移除 `AllowsTransparency` 与 `Background="Transparent"`；`WindowChrome` 的 `CornerRadius` 交给系统，最大化圆角由 §7 的 `Margin`/`CornerRadius` 触发器处理。阴影改用 `WindowChrome.GlassFrameThickness` + 一层透明 Border 模拟。

---

## P1 · 一致性 / 复用

### 9. 嵌套卡（卡中卡）

**证据**
- `ShellView.xaml:200-207` — 右侧 `DetailBorder` 用 `App.ElevatedCard`（12 圆角 + 阴影 + 1px 描边）。
- `AgentDetailView.xaml:12-14` — 内部 header 又用 `App.SectionCard`（有 `Border=Transparent` + 底色），产生"卡上再叠一层卡"的观感。
- `AgentListView.xaml:152-171` — 折叠态 sidebar 也是 `App.ElevatedCard` 里套 tile。

**改法**
`DetailBorder` 去掉阴影和圆角，只保留 `Border=App.Line` 一层外框；内部 header 完全去卡片化（改成 `Border BorderThickness=0,0,0,1 BorderBrush=App.Line` 一条发丝线，与 §5 的下划线 tab 呼应）。整棵树最多只有一层"卡"。

### 10. IconButton 就地覆盖

**证据**
- `DesignSystem.xaml:179-188` — `App.IconButton` 已经定义了 `BorderBrush=BorderStrong` + `Background=Surface`。
- `ShellView.xaml:184-186` — 又在 `SidebarToggle` 上写 `Background=App.Surface / BorderBrush=App.Border / BorderThickness=1`。
- `AgentDetailView.xaml:66-68, 77-79` — 覆盖两次。

**改法**
统一改回 `Style="{StaticResource App.IconButton}"`，把差异（比如 hover 时是否变色）挪进样式触发器。这类"就地覆盖"是 `de425da` 之后反复出现的原因——每处都在做局部微调，全局就永远收敛不了。

### 11. 建议卡空态仍占位

**证据**
- `SystemPromptTabView.xaml:71-81` — 用 `DataTrigger Suggestions.Count==0 → Collapsed` 处理。
- 但当只有 1 条建议时，`Height="144"`（`:83`）会撑出一个高得离谱的空框。

**改法**
`ListBox` 去掉固定高度，改 `MaxHeight="180"` + `VerticalScrollBarVisibility="Auto"`；整块 `Border` 在 `Count==0` 时 Collapsed（保留 §3 里的字段级 stat 行同样 Collapsed）。

---

## P2 · 能力缺失（不只是样式）

### 12. 看不出本地改动与服务端是否一致

**证据**
- 界面里唯一暗示同步的元素是 `Push changes` 按钮（`AgentDetailView.xaml:84-90`）——只有点了之后才知道差异。
- 头部 `Agent.UpdatedAt` badge（`:48-54`）是"上次服务端更新时间"，不是"我改了什么"。
- 原型左边缘同步轨 + 头部状态点（`redesign-shell.html` 里的 `.rail` + `.dot`）是最亮的一笔。

**改法**
在 `AgentDetailViewModel` 增加 `SyncState` 枚举（Synced / Diverged / Pushing / Error）+ `ChangedFields: IReadOnlySet<string>`。UI 侧：
1. 头部 `Agent.Name` 右侧加一颗 6px 状态点，颜色 `App.BorderStrong`（同步）/ `App.Accent`（有改动）/ `App.Danger`（推送失败）。
2. 每个字段卡（System Prompt / First Message / …）tab header 左侧一条 3px 竖条，字段 dirty 时才画。
3. Push 按钮文案跟着 `SyncState` 变：`Push 3 changes` / `Saved` / `Retry`。

### 13. 建议严重级别缺少扫描锚点

**证据**
- `SystemPromptTabView.xaml:104-118` — `Severity` 只影响 `PackIcon.Kind`（Information / LightbulbOutline / AlertOutline），`Warning` 是唯一有色的（`App.Danger`）。
- 一屏 5 条建议时，眼睛抓不到"哪条最要紧"。

**改法**
`App.SectionCard` 上加左侧 3px 语义色条（Info=`App.BorderStrong`，Suggestion=`App.Accent`，Warning=`App.Warning`），色条只在语义下使用，与 §12 的"字段 dirty 状态条"共用同一视觉语言。

---

## Token 收敛表

现状 12 档表面/描边色，原型 5 档。建议一次改到底，避免后续再"就地覆盖"：

| 现状 token (`DesignSystem.xaml:9-16`) | 现值 | 处理 | 去向 |
|---|---|---|---|
| `App.Canvas` | `#F2F4F8` | 保留、微调 | `#F4F5F7`（原型 canvas） |
| `App.Surface` | `#FFFFFF` | 保留 | 不变 |
| `App.SurfaceAlt` | `#F7F8FB` | **删除** | 与 Canvas 明度差不足；inset 改用 `Surface + 1px Line` |
| `App.SurfaceHover` | `#EEF1F6` | 保留（仅 hover） | 不变 |
| `App.SurfacePressed` | `#E5E9F1` | 保留（仅 pressed） | 不变 |
| `App.Border` | `#D6DCE6` | **改名 + 收亮** | `App.Line #E3E6EB` |
| `App.BorderEmphasis` | `#AEB8C7` | **删除** | 用 `App.Line` + 描边粗细变化区分 |
| `App.BorderStrong` | `#7B879A` | 保留（仅用于 IconButton 描边、状态点 idle） | 不变 |
| `App.Text` | `#182033` | 保留 | 不变 |
| `App.TextSecondary` | `#566176` | 保留 | 不变 |
| `App.TextMuted` | `#667085` | **删除**（与 Secondary ΔY < 5） | 统一到 Secondary |
| `App.AccentSoft` | `#EEEDFF` | **收缩到仅在同步状态点 + Selection 底色** | 保留但限制用途 |
| `App.AccentBorder` | `#C9C6FF` | **删除** | 用 `App.Accent` + 透明度 |

圆角档：现状"panel 12 / control 8 / badge 6 / selection 2 / window 14"（`DesignSystem.xaml:31-32` 注释），保留即可，但 §9 会把 Detail 外层从 12 降到 8，跟 inset 编辑器同档。

阴影：`App.CardShadow` 只保留给 Popover / Dialog 用，`App.ElevatedCard` 上的阴影删（§9）；`App.WindowShadow` 保留（配合 §8 移除 `AllowsTransparency` 后重做）。

---

## 落地路线（选一）

**路线 A · 最小修复（3 个文件，纯样式，风险低）**
只做 P0 五条：`ShellView.xaml`（去阴影、去 ElevatedCard）、`DesignSystem.xaml`（token 收敛 + TabItem 改下划线）、`SystemPromptTabView.xaml` + `FirstMessageTabView.xaml`（抽 `App.InsetEditor`、加 `MaxWidth`）。修 §6 选中条重叠 和 §7 最大化圆角顺手带进去。约 200 行 diff。

**路线 B · 按原型重建（推荐）**
`ShellView` + `AgentDetailView` + `AgentListView` 三个骨架文件按 `2026-09-24-redesign-shell.html` 重写，`DesignSystem.xaml` 一次性按上表收敛。边际成本比 A 高不了多少（原型已经是施工图），一次性把宽度、层级、状态信号、信息密度全部归位，之后不需要再第 6、7 个 `fix(ui)`。§12 的 `SyncState` 可以放到 B 之后作为独立一小步。

---

## 落地记录（Route B，同日）

已按路线 B 重建。构建通过，`dotnet test tests/ElevenLabsStudio.UnitTests` 121/121 全绿。

**改到的文件**
- `Themes/DesignSystem.xaml`：新增原型 token 组 `App.Line / App.LineSoft / App.Frame / App.Ink2 / App.Ink3 / App.Signal / App.SignalWash / App.SignalHover`；`App.ElevatedCard` 去阴影改为 1px 描边；`App.SectionCard` 变哑；`App.TabItem` 重写为下划线式（保留 `FocusVisualStyle`，兼容 `VisualPolishTests`）；新增 `App.InsetEditor` / `App.Eyebrow` / `App.Mono`。**旧 token 键全部保留**（`App.SurfaceAlt / BorderEmphasis / TextMuted / AccentBorder / CardShadow / WindowShadow` 等）——`VisualPolishTests` 按键读取，且旧视图仍在引用；本次不迁移，见"未做"。
- `ShellView.xaml`：删掉 `AllowsTransparency="True"` + `Background="Transparent"` + `Margin="8"` + `CornerRadius="14"` + `App.WindowShadow`；窗口本身成为 Canvas，`BorderThickness=1 App.Frame` 一根外框。§7 的最大化深色边、§8 的 `AllowsTransparency` 副作用同时消失。`x:Name` 全部保留（`SidebarColumn / SidebarHost / CollapsedSidebarRail / SidebarToggle / SidebarToggleIcon / MaximizeIcon / OpenSettings / Minimize / MaximizeRestore / CloseWindow / DetailHost`），`ShellView.xaml.cs` 无需改动。
- `Views/Agents/AgentListView.xaml`：根 `Border ElevatedCard` → 平铺 `Grid`（head/list/foot 三段）。行模板只留一个 `SelectionBar`（2×20 Signal，IsSelected 时 Height 0→20），删掉 `DataTemplate` 里重复的 `Margin=-10,-2,0,-2` accent 条 —— §6 修好。Dot + 名称 + AgentId 双行；空态文本 `No agents yet...`，`TextWrapping=Wrap` 满足 `ActualWidth ≤ 254`。`ImportAgent` 保持 `App.IconButton / Height=28 / Width=28 / Plus 14 / ToolTip="Import agent by ID" / HorizontalAlignment=Right`，独立成行避免与 Shell 的 `SidebarToggle` 顶角相撞。
- `Views/AgentDetail/AgentDetailView.xaml`：三行 Grid = Header / TabControl / 粘性 Footer。左列 3px `SyncRail`（`DataTrigger IsDirty` 切 `App.Line` ↔ `App.Signal`）；标题 20px + AgentId 副行；meta 行同步状态 pip + `Server version {0:yyyy-MM-dd HH:mm}` + `voice: {VoiceId}`；`TabControl` 用自定义 Template 把 tab 条与内容区分开（tab 条下面一根发丝线，内容区 `Padding=26,22,26,26`）；`SystemPrompt / FirstMessage` 两个文本 tab 里 `ContentControl MaxWidth=800 HorizontalAlignment=Left` —— §1 修好；`Push` 从头卡移到底部粘性 footer，新增 `SaveDraft` 按钮；顶卡彻底拿掉（§9 修好）。
- `ViewModels/AgentDetail/AgentDetailViewModel.cs`：`IsDirty` 之前只在 `ApplyServerSnapshot` 里通知，同步轨对打字完全无反应。加了对四个子 VM 的 `PropertyChanged` 与两个可编辑集合的 `CollectionChanged` 订阅，统一转发 `NotifyOfPropertyChange(nameof(IsDirty))`；`Dispose` 里对称退订。

**故意没做（Route B 范围外）**
- ~~`SystemPromptTabView.xaml` / `FirstMessageTabView.xaml` 里的**统计 dock 到窗口右端**（§3）、**编辑器 `BorderBrush=Transparent` 默认无边界**（§4）、**建议卡 `Height=144` 空态占位**（§11）、**Severity 只有图标没有色锚**（§13）~~ → **同日跟进 PR 已完成**，见下。
- 旧 token（`SurfaceAlt / BorderEmphasis / TextMuted / AccentBorder / CardShadow / WindowShadow`）在 `SettingsView` / `WorkflowTabView` / `VariablesTabView` / `LiveConversationView` / `ConversationsTabView` / `AgentDetailPlaceholder` 等处仍有引用，没做机械替换。它们现在在视觉上与新 token 兼容（旧值都保留），下一次单独一次 pass 收敛。

---

## 落地记录（跟进 PR 1，同日）

**改到的文件**
- `Views/AgentDetail/SystemPromptTabView.xaml`：3 行 Grid = 字段标签行 / 编辑器 / 建议区。
  - §3：`PromptLength` 与 "System prompt" 同一 Grid 行，Grid.Column=1（右端），不再 dock 到窗口右缘。
  - §4：`Border Style=App.InsetEditor`（App.InsetEditor 上加了 `IsKeyboardFocusWithin` 触发器，聚焦时 BorderBrush→Signal），默认态就有 1px 描边。
  - §11：`Suggestions.Count==0` 时整个建议区 StackPanel Collapsed；`MaxHeight=240` 取代固定 `Height=144`，1 条也不会撑出空框。
  - §13：每条建议卡左缘加 3px `SeverityBar`（Info=`BorderEmphasis`、Suggestion=`Signal`、Warning=`Warning`、Error=`Danger`）。
- `Views/AgentDetail/FirstMessageTabView.xaml`：同一份结构 + 同一份建议模板。顺带把这里之前"显示 Suggestions.Count 但从没渲染 Suggestions 列表"的漏洞补上。
- `Themes/DesignSystem.xaml`：`App.InsetEditor` 加了 `IsKeyboardFocusWithin` Trigger，两个 tab view 都不再需要各自写 DataTrigger。
- `tests/ElevenLabsStudio.UnitTests/Views/WorkspaceLayoutTests.cs`：把 `First_message_meta_row_sits_below_the_editor_not_at_the_bottom` 换成 `First_message_counter_shares_a_row_with_the_field_label`。旧测试**锁死了修复前的错误结构**（DockPanel LastChildFill + Row1=Auto 的 meta 行）；新测试断言 (a) 计数器与字段标签在同一个父 Panel 内、(b) 计数器所在 Grid.Row < 编辑器所在 Border 的 Grid.Row。

**验证**
- `dotnet build ElevenLabsStudio.slnx` → 0 errors, 6 warnings（都是既有的 nullability / unused-event 噪音，不是新引入）。
- `dotnet test tests/ElevenLabsStudio.UnitTests` → **121/121 全绿**，`First_message_editor_is_the_only_visible_message_copy`（要求 `x:Name="FirstMessage"` 唯一）也过。

**剩下的**
1. 旧 token 引用清扫（`SurfaceAlt / BorderEmphasis / TextMuted / AccentBorder / CardShadow / WindowShadow` 在 16 个文件里，含 `SettingsView` / `WorkflowTabView` / `VariablesTabView` / `LiveConversationView` / `ConversationsTabView` / `AgentDetailPlaceholder` / 两个 tab 的 code-behind），一次 pass 完成后从 `DesignSystem.xaml` 删干净，同步更新 `VisualPolishTests.VerifyDesignSystem` 让它读新 key。
2. 建议模板在两个 tab 里重复了一遍——可以抽成 `App.SuggestionItem` DataTemplate resource；这条留到 token 清扫那个 PR 一起做。
3. Route B 完成后跑一次真实 exe，`pytest -m e2e tests/ElevenLabsStudio.E2ETests/` 验证 Workflow 内层 TabControl 的 `control_type="TabItem"` 仍能被 pywinauto 找到。
