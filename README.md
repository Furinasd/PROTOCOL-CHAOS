# mhyTest

一个基于 **Unity 6** 的第三人称战斗教学 Demo，核心玩法围绕“**极性切换**、**同色吸收**、**异色弹刀**、**能量与处决循环**”展开。

## 项目概览

- 引擎版本：`6000.3.6f1`
- 主要场景：`Assets/Scenes/MainScene.unity`
- 核心流程：`QuestFlowManager` 管理四阶段教学（Phase1~Phase4）并衔接 Boss 战

## 核心玩法

1. **同色吸收（Phase1）**  
   通过切换到与来袭攻击同色的极性进行吸收，获得能量，不受伤害。

2. **异色弹刀（Phase2）**  
   在窗口期内对异色攻击执行弹刀，消耗能量并对敌人造成躯干反制。

3. **能量循环与处决（Phase3）**  
   通过闪避/吸收等行为积攒能量，压低敌方躯干后执行处决。

4. **Boss 终幕（Phase4）**  
   进入教学 Boss 多段循环与终幕演出。

## 操作说明（默认）

- `W / A / S / D`：移动
- `Space`：跳跃
- `Left Shift`：冲刺（Dash）
- `鼠标右键`：切换极性
- `鼠标左键`：格挡 / 弹刀输入
- `F`：处决
- `R`：死亡后快速重开

## 目录结构（核心）

```text
Assets/
  Scenes/
    MainScene.unity
  Scripts/
    Player/                # 玩家移动、战斗受击、能量系统、极性控制
    Enemy/                 # 敌人攻击、躯干系统、Boss 行为
    Systems/Core/          # 任务流程、转场、GameOver、时间控制
    Systems/Combat/        # 战斗协议与反馈系统
    UI/                    # 战斗 HUD、任务文本与提示
    Environment/           # 污染区与场地环境机制
```

## 运行方式

1. 使用 Unity Hub 安装并选择 **Unity 6000.3.6f1**。
2. 打开项目根目录（本仓库目录）
3. 打开场景：`Assets/Scenes/MainScene.unity`
4. 点击 Play 运行。

## 依赖说明

- 项目使用了 UPM 依赖（见 `Packages/manifest.json`），包括：
  - `com.unity.inputsystem`
  - `com.unity.cinemachine`
  - `com.unity.render-pipelines.universal`
  - `com.unity.test-framework`
- 另包含 DOTween 插件目录：`Assets/Plugins/Demigiant/DOTween`

## 测试说明

当前仓库未提供可直接在命令行执行的测试/构建脚本（无 `package.json`、无 `gradlew`）。  
建议在 Unity Editor 中通过 Play 模式对教学流程与战斗回路进行验证。
