# AGENTS.md

本文面向第一次进入本仓库的 AI Agent。目标是建立可操作的项目心智模型：先找到事实源，再理解运行链路，最后按项目现有方式修改和验证。

> 2026-09-12 交付更新：远端正式名称为 `Furinasd/PROTOCOL-CHAOS`。本次纳入 `Assets/Arts/` 既有资源并保留 GUID，原先“Arts 被忽略”的描述属于历史状态，以当前 `.gitignore`、`docs/asset-manifest.csv` 与 `docs/validation.md` 为准。没有在本轮执行 Unity 编译或运行验证；本地 Phase 2 修订和实验测试未随文档/资源交付提交。

## 1. 适用范围与事实优先级

- **实际 Git 根目录是本目录 `mhyTest/`**。上一级 `../` 是工作区包装层，包含规划文档、历史报告、压缩包和 Windows 构建产物，但不属于当前 Git 仓库。
- 判断当前行为时按以下优先级取证：
  1. 已跟踪的 `Assets/Scripts/`、`Assets/Scenes/MainScene.unity`、Prefab、`ProjectSettings/`；
  2. 场景或 Prefab 中的序列化覆盖值；
  3. 本机存在但被 Git 忽略的资源；
  4. `../*.md` 与 `../Archive/*.md` 历史文档；
  5. `Library/`、生成的解决方案、旧构建包。
- 不要只读代码字段默认值就判断运行数值。该项目大量关键平衡值由 `Assets/Scenes/MainScene.unity` 和 Prefab Inspector 覆盖。
- `Assets/Readme.asset` 只是 Unity URP 空模板说明，不是项目 README。

## 2. 项目概览

这是一个单场景、第三人称动作教程 Demo，核心玩法是“极性空间博弈”：玩家在红/蓝极性之间切换，通过同色吸收、异色弹刀、Dash/Jump 规避、架势击破与处决完成四阶段教学和 Boss 战。

主要技术栈：

- Unity `6000.3.6f1`，项目版本见 `ProjectSettings/ProjectVersion.txt`；
- Universal Render Pipeline `17.3.0`，PC/Mobile 两套 URP Asset 位于 `Assets/Settings/`；
- Input System `1.18.0`、Cinemachine `3.1.6`、uGUI、TextMesh Pro；
- DOTween DLL 与模块源码位于 `Assets/Plugins/Demigiant/DOTween/`；
- Unity Test Framework 已安装，但仓库没有测试程序集或测试用例；
- 没有自定义 `.asmdef`：自研运行时代码进入 `Assembly-CSharp`，Editor 代码进入 `Assembly-CSharp-Editor`，插件进入 firstpass 程序集。

`Packages/manifest.json` 还包含 Unity Skills、Coplay 和 Unity MCP 的 Git URL 依赖。新环境首次打开项目时需要网络完成 Package Manager 解析。

## 3. 目录与事实源地图

- `Assets/Scenes/MainScene.unity`
  - 唯一启用的 Build Scene，也是场景装配和阶段参数的主要事实源。
  - 包含玩家、四阶段敌人模板、流程管理器、镜头、HUD、音频、污染区池和 Game Over UI。
- `Assets/Scripts/Systems/Combat/CombatProtocol.cs`
  - 战斗协议层：`Polarity`、`AttackData`、`IDamageable`、新旧极性枚举桥接。
- `Assets/Scripts/Player/`
  - 输入缓冲、移动、Dash/Jump、极性、生命、能量、受击判定和玩家 VFX。
- `Assets/Scripts/Enemy/`
  - 敌人攻击状态机、代码判定盒、架势/生命、追踪、材质表现及 Tutorial Boss。
- `Assets/Scripts/Systems/Core/`
  - Phase 1~4 流程、场内转场、失败重开和全局 Hitstop。
- `Assets/Scripts/Environment/`
  - 场地边界、污染区数据层、污染区对象池和环境表现。
- `Assets/Scripts/UI/`
  - 战斗 HUD、能量格和任务文本；HUD 混合使用事件驱动、运行时兜底创建和少量低频扫描。
- `Assets/Scripts/Systems/Camera/`、`Assets/Scripts/SFX&BGM/`
  - FOV/机位与 BGM/SFX/高光音效管理。
- `Assets/Resources/Prefabs/Puddle.prefab`
  - 已跟踪的污染区 Prefab；由 `PuddleManager` 预热和复用。
- `Assets/Editor/MaterialQuickFix.cs`、`Assets/Scripts/Editor/UIAutoFixer.cs`
  - Editor 修复工具。后者带 `[InitializeOnLoad]`，打开项目后可能自动创建 EventSystem、替换 TMP 字体并把场景标脏。
- `ProjectSettings/`、`Packages/`
  - Unity、渲染、输入、Tag/Layer、构建场景和依赖的事实源。

不要手改或提交：`Library/`、`Temp/`、`Logs/`、`UserSettings/`、`obj/`、生成的 `*.csproj`、`*.slnx`。资源序列化模式是 Force Text；修改场景、Prefab 或资源时保留配套 `.meta` 和 GUID。

## 4. 重要的版本库边界问题

`.gitignore` 中的 `Arts/` 规则会匹配并忽略整个 `Assets/Arts/`。当前本机的下列运行关键资源因此**存在但未被 Git 跟踪**：

- `Assets/Arts/Player.prefab`
- `Assets/Arts/Enemy.prefab`
- `Assets/Arts/Ground.prefab`
- `Assets/Arts/QuestUIManager.prefab`
- `Assets/Arts/MiSans VF*`
- `Assets/Arts/Audio/*`

`MainScene.unity` 通过 GUID 直接引用这些资源。新克隆若没有额外拷贝，会出现 Missing Prefab、字体和音频缺失，不能视为可复现工程。

修改这些本地资源前必须先确认交付策略：单纯编辑被忽略文件不会出现在 `git status`，也不会随提交交付。不要用 `git status clean` 证明本机资源与远端一致。

上一级的 `../包/`、`../demo.zip`、`../mhyTest.zip` 是工作区产物，不是源码真源；不要反编译或修改它们来代替源代码修改。

## 5. 核心运行机制

### 5.1 启动与全局对象

`MainScene` 是唯一入口。加载后：

1. `TimeManager`、`CameraController`、`AudioManager`、`CombatFeedbackManager`、`PuddleManager`、`GameOverManager` 在 `Awake` 建立场景内单例。
2. `PCPerformanceMonitor` 通过 `[RuntimeInitializeOnLoadMethod]` 自动创建并 `DontDestroyOnLoad`；F8 切换显示。
3. `QuestFlowManager.Start()` 在 `autoStartOnSceneLoad` 开启时调用 `StartFlow()`。
4. `QuestFlowManager` 从 `PlayerPrefs` 恢复进度，然后用协程依次推进 Phase 1~4。
5. `AudioManager.Start()` 若有绑定则播放 P1 BGM。

这些单例不是跨场景持久化架构；项目当前只有一个游戏场景。添加第二场景前需重新审查生命周期与重复实例销毁行为。

### 5.2 四阶段教程

阶段模板位于 `MainScene` 的禁用父对象 `Template` 下。`QuestFlowManager` 持有这些**场景对象**作为模板，按阶段 `Instantiate`，转场时销毁上一阶段实体。

| 阶段 | 当前场景门槛 | 规则与配置 |
| --- | --- | --- |
| Phase 1 | 同色吸收 3 次 | 开启吸收奖励、关闭闪避奖励；P1 敌人强制蓝色、慢前摇，不生成污染区。 |
| Phase 2 | 完美弹刀 2 次 | 吸收/闪避不充能；每次敌人前摇由 `OnTelegraphStarted` 触发 `CheatFillEnergyForTutorial()`，但弹刀仍真实消耗 3 格。 |
| Phase 3 | 满能 1 次且处决 1 次 | 恢复完整充能来源；启动时清空旧能量；P3 敌人可生成污染区。 |
| Phase 4 | Boss 实体销毁 | Boss 架势击破后按 F 处决；场景配置为需处决 2 次，随后强制进入终幕。 |

阶段进度事件流：

`PlayerCombatReceiver` / `PlayerEnergySystem` 事件 → `QuestFlowManager` 计数与保存 → `QuestUIManager` 更新任务文本和反馈。

`phaseTransitionDelay` 在场景中为 3 秒。`ArenaTransitionManager.arenaPlatform` 当前未绑定，因此转场只执行可用的震动、光束和灯光变化，不会实际升降地板。

### 5.3 战斗调用链

核心调用顺序：

`EnemyAttackBrain` 前摇/追踪 → `EnemyHitbox.ActivateHitbox(AttackData)` → `OverlapBoxNonAlloc` 命中 `IDamageable` → `PlayerCombatReceiver.TakeDamage()` → 生命/能量/架势/反馈事件。

`PlayerCombatReceiver` 的判定顺序不能随意调整：

1. 最近 0.3 秒内 Dash 或 Jump：空间规避，默认获得 2 格能量；
2. 判定窗内左键 + 异色/非 Neutral + 能量足够：消耗 3 格弹刀；
3. 攻击与玩家同色：不扣血，默认获得 1 格能量；
4. 其余情况扣血；站在特殊异常核心内时伤害翻倍。

弹刀成功会对来源 `EnemyPosture` 增加架势值。`TakeDamage()` 返回 `true` 后，`EnemyHitbox` 还会再次把敌人架势打满并触发反馈。修改弹刀反馈或架势时必须同时审查这两处，当前存在重复调用反馈的可能。

玩家按 F 后，`PlayerCombatReceiver.TryExecuteEnemy()` 使用 `OverlapSphereNonAlloc` 找附近 `IsBroken` 敌人：

- 普通敌人走 `EnemyPosture.Execute()`，按最大生命百分比扣血并重置架势；
- Tutorial Boss 优先走 `TutorialBoss.TryExecuteFromPlayer()`，达到场景要求次数后进入终幕并销毁。

### 5.4 环境与污染区

- `EnemyAttackBrain` 用 Ground LayerMask 的 `RaycastNonAlloc` 找落点，再向 `PuddleManager` 取池对象。
- `PuddleManager` 维护对象池和 `ActivePuddles`；`ChaosPuddle` 只负责数据与表现，不主动结算玩家状态。
- `PlayerController` 每 0.1 秒轮询活跃污染区，以水平距离判定是否进入；重叠多个污染区时取最大伤害和最低速度倍率，不叠乘。
- 环境伤害通过 `PlayerCombatReceiver.ApplyEnvironmentalDamage()` 直通生命值，跳过极性/弹刀判定。
- 能量流失实际由 `PlayerEnergySystem` 硬编码为在污染区每 3 秒消耗 1 格；`ChaosPuddle.energyDrainPerSecond` 当前没有被读取。
- Ground 定义为 Layer 6；主场景中的 Ground 实例和各阶段敌人的 `groundLayerMask` 都覆盖为 bit 64。复制敌人或地面时必须保持这组联动。

### 5.5 状态、配置与持久化

- 项目没有用于玩法配置的 ScriptableObject 数据层；主要配置来自 C# 序列化字段、`MainScene` 和 Prefab。
- `QuestFlowManager` 使用 `PlayerPrefs` 保存：
  - `QuestFlow_Valid`
  - `QuestFlow_Phase`
  - `QuestFlow_Absorb`
  - `QuestFlow_Parry`
  - `QuestFlow_Energy`
  - `QuestFlow_Execute`
- 完成流程时默认清除存档。回归教程开局时，先调用 `QuestFlowManager.ClearProgressSave()` 或确认这些键已清除，否则会从中间阶段恢复。

## 6. 输入、UI、镜头与音频

### 6.1 实际玩法输入

玩法输入没有消费 `InputSystem_Actions.inputactions` 的 Player Action Map，而是在脚本中直接读取 `Keyboard.current` / `Mouse.current`：

- WASD：移动；
- Space：跳跃；
- Left Shift：Dash；
- 鼠标右键：切换红/蓝极性；
- 鼠标左键：打开弹刀判定窗；
- F：处决；
- R：死亡界面重开；
- F8：性能面板。

`Assets/InputSystem_Actions.inputactions` 当前主要由 `InputSystemUIInputModule` 使用，其通用 Player Map 与上述完整战斗输入并不一致。改键时要同步审查 `PlayerController`、`PlayerCombatReceiver`、`PlayerPolarity`、`GameOverManager`、`PCPerformanceMonitor`、任务 UI 文案和 Input Actions，不能只改一个位置。

Player Settings 的 `activeInputHandler` 为 1，即 New Input System。`MainScene` 的 EventSystem 已使用 `InputSystemUIInputModule`。

### 6.2 UI 与表现

- `QuestUIManager` 通过被忽略的 `Assets/Arts/QuestUIManager.prefab` 装配，数据引用为空时会在 `Awake` 查找流程和能量系统。
- `EnergyHUDController` 未显式绑定能量格时会从子节点抓取全部 `Image`。
- `CombatHUDManager` 没有显式放在主场景；首次访问 `Instance` 时可创建空对象，再从场景查找玩家、Boss、Canvas 和执行提示。其引用刷新和可见性逻辑不等同于完整血条实现。
- Hitstop 期间需要继续播放的 Tween 使用 `.SetUpdate(true)` 或独立更新时间；新表现应沿用此约定。
- `CameraController` 的 Dash FOV 已接线，但 `EnterBattleMode()` / `ExitBattleMode()` 当前没有调用方，且场景中 `freeLookCam` 与 `battleCam` 指向同一台 Cinemachine Camera，不能假设已有真实机位切换。
- `AudioManager` 运行时创建 BGM、普通 SFX、高光 SFX 三个 AudioSource。当前本机只绑定了两首 BGM 和玩家基础战斗音，Boss/环境/边界音效槽仍为空，且已绑定音频位于被忽略的 `Assets/Arts/Audio/`。

## 7. 代码与架构约定

- 类和公开成员使用 PascalCase，私有字段多用 camelCase；当前代码不使用 namespace。
- 运行时代码按领域放入 `Player/`、`Enemy/`、`Environment/`、`Systems/`、`UI/`、`SFX&BGM/`，不要重新堆回单一 `Systems` 目录。
- 战斗数据契约统一放在 `CombatProtocol.cs`。新增攻击字段时同时修改数据生产者、`IDamageable` 消费者和相关 Prefab/场景序列化配置。
- 现有架构是“场景单例 + C# 事件 + Inspector 引用 + 查找兜底”的混合形态。新增功能优先用序列化引用、注册或事件，不要在热路径新增 `Find*` / `GetComponent*`。
- 高频物理使用 `NonAlloc` API 和预分配缓冲；缓冲满时已有警告。扩容前先收紧 LayerMask。
- 频繁变化的 Renderer 属性优先 `MaterialPropertyBlock` / `sharedMaterial`；不要在对象池或高频路径反复访问 `.material`。
- 污染区与伤害漂字已有对象池；不要把它们退回高频 Instantiate/Destroy。
- 事件订阅通常放在 `OnEnable`，解绑放在 `OnDisable` / `OnDestroy`。改动静态事件时尤其要检查退订，避免重载场景后重复回调。
- 中文 `Header`、`Tooltip`、日志和长注释是现有风格；保留能解释业务约束的注释，避免继续堆叠过期阶段标签。

## 8. 历史兼容逻辑：不要误删

以下代码看似多余，但文件内已明确标记为兼容桥或迁移残留。删除前必须先全仓检索引用并验证场景/Prefab：

- `PolarityColor` 与 `PolarityBridge`：旧玩家极性接口到新战斗协议的桥接；
- `PlayerController.RegisterPuddle()` / `UnregisterPuddle()`：旧触发器架构的 Obsolete 空桩；
- `PlayerController.HandleDash()`：输入缓冲重构后的空壳；
- `BoundaryVisual`：旧场景引用桥接；
- `EnemyWorldHUD.ForceSyncNow()` / `TriggerAnomalyCoreParryPulse()`：旧头顶 HUD 兼容空实现；
- `PolarityPlayerController`：为旧 `ChaosPuddle` 编译关系保留的占位类；
- `CombatHUDManager.ForceRefreshBossUI()`、`AnimateFill()`、`EnforcePlayerHudTopLeft()`：当前为空，不要把调用存在误判为功能已经实现。

## 9. 开发、运行、构建与自动化

### 9.1 本地运行

1. 使用 Unity Hub / Editor `6000.3.6f1` 打开本 Git 根目录。
2. 等待 `Packages/manifest.json` 依赖恢复和脚本编译完成。
3. 打开 `Assets/Scenes/MainScene.unity`。
4. 确认 Console 无编译错误和 Missing Script/Prefab，再进入 Play Mode。

若新克隆缺少 `Assets/Arts/`，先解决版本化/资源交付问题；不要用临时替代资源掩盖 Missing GUID 后继续声称通过验证。

### 9.2 编译、测试、Lint

- 仓库没有 CI workflow、构建脚本、lint 或格式化配置。
- 仓库没有 EditMode/PlayMode 测试；`com.unity.test-framework` 只代表能力已安装。
- `*.csproj` 和 `mhyTest.slnx` 是 Unity 生成且被忽略的本机文件，不是版本库事实源。
- 辅助编译命令是：

  ```powershell
  dotnet build .\mhyTest.slnx -nologo
  ```

  但当前本机该命令会因生成的 `Assembly-CSharp-firstpass.csproj` 引用了不存在的 `Microsoft.Unity.Analyzers.dll` 路径而失败。先让 Unity 重新生成工程文件，再把 `dotnet build` 当辅助检查；最终仍以目标 Unity Editor 的脚本编译和 Console 为准。

### 9.3 Windows 构建

- 已提交的 `Assets/Settings/Build Profiles/Windows.asset` 是非 Development、非 Profiler 连接的 Windows Profile。
- `ProjectSettings/EditorBuildSettings.asset` 只启用 `Assets/Scenes/MainScene.unity`。
- 仓库没有可复用的命令行 BuildPipeline；使用 Unity Build Profiles 构建。
- 上一级 `../包/` 是历史本机构建，不证明当前源码仍能成功构建。

## 10. 按改动类型验证

### C# 与架构改动

- 在 Unity `6000.3.6f1` 中触发完整重新编译，Console 必须无 Error。
- 检查 `OnEnable` / `OnDisable` 对称、单例重复实例、静态事件退订和场景重载后的 `Time.timeScale`。
- Unity 重新生成工程文件后，可补跑 `dotnet build`；不要把当前生成工程的 Analyzer 路径故障误报为源码编译错误。

### 战斗与输入改动

- 清除 QuestFlow PlayerPrefs，从 Phase 1 开始走到 Completed。
- 至少覆盖：同色吸收、异色受伤、能量不足弹刀、成功弹刀、Dash/Jump 近失、架势击破、普通处决、Boss 两次处决、死亡后 R/按钮重开。
- 检查每次反馈是否被 `PlayerCombatReceiver` 和 `EnemyHitbox` 重复触发。
- 改键后核对任务文字、EventSystem、鼠标锁定/解锁与死亡 UI。

### 场景、Prefab 与资源改动

- 检查 `MainScene`、Build Settings、Prefab 实例覆盖和 `.meta` GUID。
- 专门确认四个模板仍在禁用的 `Template` 下，`QuestFlowManager` 的 SpawnRoot/Prefab 引用均非 Missing。
- 若修改 `Assets/Arts/`，先确认这些文件已被正确纳入交付；仅本机验证不够。
- UI 需至少在 1920×1080 基准和一个非 16:9 分辨率检查锚点、字体和执行提示。

### 污染区与性能改动

- 验证 Ground Layer 6、敌人 `groundLayerMask = 64`、对象池预热/回收、多个污染区重叠和特殊核心判定。
- 使用 F8 面板与 Unity Profiler 检查 FPS、帧时间、Managed Memory 和 GC Alloc。
- 对攻击判定、漂字、污染区和材质动画做持续触发；确认 NonAlloc 缓冲不溢出、池不无限增长、Tween/MPB 在 `OnDisable` 清理。

### 构建验收

- 用 Windows Build Profile 生成新构建并从可执行文件冷启动。
- 验证无 Missing Asset/Shader、MainScene 可进、完整 Phase 流程、音频绑定、死亡重开和退出后再次启动的 PlayerPrefs 行为。
- Editor Play Mode、`dotnet build`、历史 `../包/` 都不能单独替代 Player 构建验收。

## 11. 已确认的风险与技术债

1. **仓库不可独立复现**：`Assets/Arts/` 被忽略，但主场景依赖其中的 Player、Enemy、Ground、Quest UI、字体和音频。
2. **辅助编译入口不稳定**：当前生成的 csproj 含失效的 VS Code Unity Analyzer 绝对路径。
3. **缺少自动化验证**：没有测试程序集、CI、lint、格式化或命令行构建脚本。
4. **Phase 2 配置有隐藏流程要求**：场景中的 `Enemy_P2.currentPosture` 初始为 100/100。第一次成功弹刀会使其进入 Stunned；要完成第二次弹刀，玩家可能必须先按 F 处决使其恢复，但 Phase 2 文案未说明这一点。
5. **部分演出未完整接线**：`ArenaTransitionManager.arenaPlatform`、Tutorial Boss 的 `wireframeMaterial` / `finalExplosionSfx` 均为空；`PlayerController.visualRoot` 在本地 Player Prefab 中也为空。
6. **特殊污染区“全局净化”未接线**：`PuddleManager.TriggerGlobalPurify()` 没有调用方。当前异常核心弹刀实现的是双倍架势伤害、额外能量和反馈，不会清场；历史文档中的“弹刀净化全场”不是当前代码事实。
7. **未使用或名存实亡的字段/API**：`ChaosPuddle.energyDrainPerSecond`、`EnemyAttackBrain.chaosPuddlePrefab` 未参与当前结算；`CameraController.EnterBattleMode/ExitBattleMode` 无调用方。
8. **时间控制是覆盖式而非栈式**：`TimeManager.DoHitstop()` 会 `StopAllCoroutines()`，结束后固定把 `Time.timeScale` 恢复到 1；新增慢动作/暂停系统前需解决相互覆盖。
9. **文档可能落后于代码**：例如 `../audio_requirements.md` 记录“零音频”，但本机已有被忽略音频；旧验证报告中的 Phase 目标也与当前场景值不同。历史文档只能提供意图，不能覆盖场景和源码。

## 12. Agent 高效工作建议

- 改任务流程：先看 `QuestFlowManager.cs`，再看 `MainScene` 的序列化目标值和 `QuestUIManager.BuildPhaseText()`。
- 改战斗规则：从 `CombatProtocol.cs` → `EnemyAttackBrain` / `EnemyHitbox` → `PlayerCombatReceiver` → `EnemyPosture` 顺调用链阅读。
- 改玩家手感：同时检查 `PlayerController` 状态机、输入缓冲、`PlayerCombatReceiver` 的时间窗，以及 `CameraController` / DOTween 的 unscaled 行为。
- 改污染区：同时检查 `EnemyAttackBrain` 落点、`PuddleManager` 池、`ChaosPuddle` 数据和 `PlayerController.PollEnvironmentalConditions()`；不要把触发器当结算真源。
- 改 HUD：先确认组件是否真实装配。`CombatHUDManager.Instance` 会自动创建空对象，调用不报错不代表视觉已显示。
- 改默认值：先搜索同名场景 `propertyPath` 和 Prefab YAML；Inspector 覆盖会让代码默认值看似“无效”。
- 修改前后都运行 `git status --short --branch --untracked-files=all`，但同时用 `git check-ignore -v <path>` 检查关键资源是否被忽略。
- 保持改动小而连贯，不要顺手重构第三方 DOTween、TMP 模板资源或无关历史代码。

## 13. 文件操作安全规则

- 禁止批量删除文件或目录。
- 不要使用 `del /s`、`rd /s`、`rmdir /s`、`Remove-Item -Recurse`、`rm -rf`。
- 删除时只能一次处理一个已确认的明确文件路径，例如：

  ```powershell
  Remove-Item "C:\path\to\file.txt"
  ```

- 如果任务需要批量删除，停止操作并让用户手动处理。
