# 01 · 架构与分层

> 详细版见 AgentBoard 项目 19 `ElevenLabs Specs / 01 · 架构与分层`。

## 三层结构

```
ElevenLabsStudio.slnx
├── Directory.Build.props           # 集中版本/警告/langVersion
├── src/
│   ├── ElevenLabsStudio.Core/        # 域模型 + 抽象 + MVVM 基类
│   ├── ElevenLabsStudio.Infrastructure/   # 真实 ElevenLabs HTTP + Polly
│   └── ElevenLabsStudio/             # WPF UI
└── tests/
    ├── ElevenLabsStudio.UnitTests/
    ├── ElevenLabsStudio.IntegrationTests/
    └── ElevenLabsStudio.E2ETests/    # pytest，启动 .exe
```

## 依赖方向（强约束）

- `Core` 不引用 `Infrastructure` / `UI`
- `Infrastructure` 不引用 `UI`
- 测试项目只引用 `Core` + `Infrastructure`（不能引用 WPF UI；WPF target 与 xUnit net10.0 不兼容）

## 选用的设计模式

| 模式       | 落地位置                                              |
| ---------- | ----------------------------------------------------- |
| MVVM       | Caliburn.Micro `Screen` + 命名约定绑定                |
| IoC / DI   | CM5 `BootstrapperBase` + Microsoft.Extensions.DI     |
| Mediator   | CM5 `IEventAggregator` 跨 ViewModel                  |
| Strategy   | `ISuggestionEngine` 多策略可换                       |
| Repository | `IElevenLabsClient` 抽象包住 HTTP 实现                |
| Decorator  | Polly 包 `IElevenLabsClient`（重试 + 熔断 + 日志）  |
| Adapter    | `ElevenLabsHttpClient` 把 wire DTO → Core Domain     |
| Composite  | `ShellViewModel` 组合 Agents / Conversations 子 VM  |