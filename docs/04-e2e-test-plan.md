# 04 · E2E 测试计划

> 详细版见 AgentBoard 项目 19 `ElevenLabs Specs / 04 · E2E 测试计划`。

## 测试金字塔

```
              E2E (pytest)        —— 真实启动 .exe + 窗口/控件
             Integration (xUnit)   —— 真实 ElevenLabs API
            Unit (xUnit)           —— 纯 ViewModel + Core / Mapping
```

## 项目分层

| 项目                                       | 范围                                                          |
| ------------------------------------------ | ------------------------------------------------------------- |
| `tests/ElevenLabsStudio.UnitTests/`        | ViewModel / Domain / Mapping / Suggestion 引擎 / Polly         |
| `tests/ElevenLabsStudio.IntegrationTests/` | 真实 ElevenLabs API（需 `ELEVENLABS_API_KEY`）                |
| `tests/ElevenLabsStudio.E2ETests/`         | pytest + 启动 `ElevenLabsStudio.exe` 验证窗口/控件            |

## 跑法

```powershell
# Unit + Integration（CI 默认）
dotnet test ElevenLabsStudio.slnx

# E2E
pytest -m e2e tests/ElevenLabsStudio.E2ETests/
```

## 已知问题

- Windows 11 上 WPF 高 DPI 渲染时 `AutomationId` 可能重复 → 用 `x:Name` 而非 GUID
- 真实 ElevenLabs API 速率限制：默认 100 RPM，超出会 429
- `.exe` 启动后主窗口不一定立刻 ready → `wait_until_window_visible()` 最多 30s 重试
- 测试需要关闭任何残留 `ElevenLabsStudio.exe` → `taskkill /IM ElevenLabsStudio.exe /F`

## E2E 用例执行规约

新增 E2E 用例必须同步：

1. `tests/ElevenLabsStudio.E2ETests/test_xxx.py` 写入完整用例
2. 用例加 `@pytest.mark.e2e`
3. 本文件表格新增一行
4. 跑一次 `pytest -m e2e` 验证通过
5. commit 信息：`test(e2e): <用例名>`，按用户偏好自动 `git push`