# ElevenLabsStudio

[![build & test](https://github.com/hfutlimit/ElevenLabsStudio/actions/workflows/build.yml/badge.svg)](https://github.com/hfutlimit/ElevenLabsStudio/actions/workflows/build.yml)

WPF 桌面工具：本地建议 + 推送更新 ElevenLabs Conversational AI Agent，
查询 conversation records，做端到端自检。

## 技术栈

- .NET 10 (`net10.0-windows`) / WPF
- Caliburn.Micro 5.0（MVVM + IoC + EventAggregator + Conductor）
- MaterialDesignThemes 5.3
- Microsoft.Extensions.*（HttpClient + Logging + Configuration）
- Polly 7.x（HTTP 重试 + 熔断）
- Python 3.14 + pytest（E2E 启动 .exe）

## 分层

```
src/
  ElevenLabsStudio.Core/             # 域模型 + 抽象 + 建议引擎 + MVVM 基类
  ElevenLabsStudio.Infrastructure/   # 真实 ElevenLabs HTTP + Polly + Mapping
  ElevenLabsStudio/                  # WPF UI（Views + ViewModels + Bootstrapper）
tests/
  ElevenLabsStudio.UnitTests/        # 纯业务
  ElevenLabsStudio.IntegrationTests/ # 真实 ElevenLabs API
  ElevenLabsStudio.E2ETests/         # pytest 启动 .exe
docs/                                # 与 AgentBoard `ElevenLabs Specs/` 同步的本地副本
```

## 关键原则（MVVM 强约束）

- View **不写业务逻辑**（`*.xaml.cs` 只 `InitializeComponent()`）
- ViewModel **不引用 WPF**（`using System.Windows.*` 禁止）
- 服务接口在 `Core/Abstractions/`，实现在 `Infrastructure/`
- ViewModel ctor 仅注入抽象，便于 Mock 单测
- 跨 ViewModel 通信走 `IEventAggregator`

详细见 `docs/02-mvvm-conventions.md`，权威版同步在 AgentBoard 项目 19 `ElevenLabs Specs / 02`。

## 快速开始

### 配置 API Key

```
# 选项 A：环境变量（推荐，不入 git）
$env:ELEVENLABS_API_KEY = "your_key_here"

# 选项 B：写入 src/ElevenLabsStudio/appsettings.json
{
  "ElevenLabs": {
    "ApiKey": "your_key_here"
  }
}
```

### 构建 & 运行

```powershell
dotnet build ElevenLabsStudio.slnx -c Debug
dotnet run --project src/ElevenLabsStudio
```

### 跑测试

```powershell
# 单元 + 集成
dotnet test ElevenLabsStudio.slnx

# 单独跑 E2E
pytest -m e2e tests/ElevenLabsStudio.E2ETests/
```

## Status

| 阶段       | 状态     | 备注                                      |
| ---------- | -------- | ----------------------------------------- |
| 项目骨架   | ✅ 已完成 | src/ 三层 + tests/ 三套 + Bootstrapper  |
| Core 域模型 | ✅ 已完成 | Agent / Conversation / Suggestion / etc. |
| Infrastructure | ✅ 已完成 | HTTP client + Polly + Mapping |
| UI 骨架     | ✅ 已完成 | Shell + Agents + Conversations + MaterialDesign |
| 单元测试    | ✅ 已完成 | HeuristicSuggestionEngine + AgentMapping |
| 集成测试    | 🟡 占位  | 需 `ELEVENLABS_API_KEY` 启用 |
| E2E 测试    | ✅ smoke | pytest 启动 .exe + 退出清洁 |
| 真实功能    | ⬜ 未启动 | 待后续迭代补 Agent 编辑 / 推送 / 详情面板 |

## 文档链

| 主题               | 本地                          | AgentBoard 权威版                                  |
| ------------------ | ----------------------------- | -------------------------------------------------- |
| 架构与分层         | `docs/01-architecture.md`     | 项目 19 `ElevenLabs Specs / 01`                   |
| MVVM 规范          | `docs/02-mvvm-conventions.md` | 项目 19 `ElevenLabs Specs / 02`                   |
| 领域与 API         | `docs/03-domain-and-api.md`   | 项目 19 `ElevenLabs Specs / 03`                   |
| E2E 测试计划       | `docs/04-e2e-test-plan.md`    | 项目 19 `ElevenLabs Specs / 04`                   |

## 开发约定

- 每次改完 `git status` / `git add` / `git commit -m "<type>(<scope>): <desc>"` / `git push origin main` 自动完成
- commit type 走 conventional commits：`feat` / `fix` / `docs` / `style` / `refactor` / `test` / `chore`
- 凭据不入库，git remote: `hfutlimit/ElevenLabsStudio`
- 工作分支默认 `main`，直推