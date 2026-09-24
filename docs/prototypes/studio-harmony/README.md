# Studio Harmony — 现有界面元素布局原型

直接双击 `index.html` 打开。无需安装或联网。当前入口已替换上一版，主界面截图为 `preview-light.png`。

本次仅根据当前 XAML 中已有的元素调整布局、颜色、间距和层级。沿用现有英文标签，避免把文案重构混入布局讨论。暖白、灰绿、细分隔线构成两栏布局，左侧列表，右侧编辑；页头放 Dry run / Reload，底部固定 Save draft / Push to server。

## 元素与当前源码对应

| 原型区域 | 当前来源 | 保留元素 |
| --- | --- | --- |
| 窗口标题栏 | `ShellView.xaml` | 应用名称、API 地址、Settings、窗口按钮、折叠侧栏 |
| Agent 列表 | `Views/Agents/AgentListView.xaml` | 数量、按 ID 导入、名称、ID、选中态、连接状态 |
| Agent 页头与页脚 | `Views/AgentDetail/AgentDetailView.xaml` | 名称、ID、同步状态、服务器时间、Voice ID、Dry run、Reload、Save draft、Push |
| 系统指令、开场白 | `SystemPromptTabView.xaml`、`FirstMessageTabView.xaml` | 字段标题、字符数、多行编辑框、本地建议的字段名／严重程度／说明 |
| 工作流 | `WorkflowTabView.xaml` | 画布、节点增删与名称编辑、节点／连线检查器、只读连线、只读 Raw JSON |
| 变量 | `VariablesTabView.xaml` | Name、Value、Type 的编辑表格，添加与删除 |
| 会话记录 | `ConversationsTabView.xaml` | 最近七天列表、刷新、状态／时间／ID、Transcript |
| 实时对话 | `LiveConversationView.xaml` | Start／End、状态与时间、Session、Branch ID、Environment、场景、动态变量、音量条、静音、Transcript、文本发送 |
| 设置 | `SettingsView.xaml` | API Key、离线 Mock 开关、Save／Cancel |

## 本次移除

上一版额外增加的声音试听球、音色预览、声音／模型配置面板、测试 Agent 快捷入口、统计卡片、个人工作区与用户资料、搜索框、外观切换、应用建议按钮、开场白气泡预览和独立撤销按钮均已移除。只展示 Voice ID，不提供声音测试。Dry run 的含义是重新计算本地建议。

## 原型行为边界

- 所有 Agent 与会话内容为展示数据；按钮只演示本地界面状态，未连接 API。页头明确标记 Layout prototype。
- 实时对话只复现已有控件，Start 演示连接状态，Send 只在 Transcript 中显示所输入的文字，不生成回复、不采集麦克风、不播放声音。音量条保持为零。
- Save draft 保留在页面会话内，不作浏览器持久化；刷新恢复初始样例。切换 Agent 有未保存内容时显示保存／放弃／取消。
- Reload、Push、Import、Settings 均为本地演示。设置中仅可填写示例密钥，不持久化、不发送。
- 工作流编辑作用于本地节点，Raw JSON 始终展示服务器示例快照。
- 窗口最小化／最大化／关闭符号为桌面窗口外观示意，浏览器内不控制系统窗口。
- 所有改动仅限本目录，没有修改 WPF 主程序。

## 验证

使用 Chromium 检查六个页面、Agent 切换、未保存提醒、Dry run、Reload、保存／推送、工作流节点编辑、变量编辑、会话切换、实时对话控件和设置弹窗。桌面以 1440×900、1360×840、1280×800、980×700 检查，窄屏以 390×844 检查。
