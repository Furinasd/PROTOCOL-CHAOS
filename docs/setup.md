# Setup · 工程恢复

## 当前分发范围

仓库提供源码、MainScene、项目设置、既有插件以及原先被忽略的 Arts 资源，包括：

- `Assets/Arts/MiSans VF.ttf` 与 `.meta`；`MiSans VF SDF.asset` 与 `.meta`。
- `Assets/Arts/Audio/` 内两首 BGM、六项基础战斗音频及对应 `.meta`。

完整路径、GUID、大小与 SHA-256 清单见 [asset-manifest.csv](asset-manifest.csv)。文件按作者确认纳入，清单不构成第三方资源再授权。此次保留原文件和 `.meta`，不替换字体或音频；是否能在新环境正确导入，仍需 Unity 实际验证。

清单 SHA-256 对应本次纳入前的本地文件字节；文本资源可能受 Git checkout 换行转换影响。跨平台核对文本使用 `git_blob`（Git 规范化内容对象）与 GUID，二进制资源可直接比对 SHA-256。

## 环境

1. Unity Hub 安装 **6000.3.6f1**，含 Windows 构建支持。
2. `git clone https://github.com/Furinasd/PROTOCOL-CHAOS.git`，在 Hub 打开仓库根目录。
3. 等待 UPM 恢复。版本以 `Packages/manifest.json` 和 `packages-lock.json` 为准：URP 17.3.0、Input System 1.18.0、Cinemachine 3.1.6、Test Framework 1.6.0。
4. manifest 保留 Unity Skills、Coplay、Unity MCP 的开发工具 Git 依赖，首次恢复需要网络；这些不是玩法能力的证明。本次未变更依赖或重新生成 lock。
5. 打开 `Assets/Scenes/MainScene.unity`，先检查 Console 与 Missing 引用；核对缺失文件时使用资源清单，不重新生成已有 `.meta`。
6. 清除旧教学进度后再验证从头流程。`QuestFlowManager.ClearProgressSave()` 是现有清档入口，死亡重载本身不清档。

## 编译、测试与构建

以目标 Unity Editor 的脚本编译为准。生成的 `.slnx` / `.csproj` 不入库；本机曾出现失效 Unity Analyzer 路径，因此不能把旧 `dotnet build` 结果当作源码结论。

当前公开快照没有可复用命令行 BuildPipeline 或已提交的测试程序集。使用 Windows Build Profile，检查唯一启用场景为 MainScene。打包后从可执行文件冷启动，按 [validation.md](validation.md) 回归；历史工作区构建包不等于当前版本构建。

本次没有提供新的可玩包下载链接。后续可玩包应在 Releases 标明提交 SHA、平台与已知限制。
