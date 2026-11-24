# 关卡接入全局战役

## 当前 Level 1

运行入口已接到 `Assets/Scenes/Archive/BounceBackSystems/Santorini Gameplay.unity`。它当前通过 `GeometryLevel.GoalReached` 驱动现有完成过渡。 `CampaignLevelHandoff` 等 `CampaignPlayground.TransitionFinished` 回到城市入口后才交给全局选关。Level 1 制作时继续维护原场景自己的出生点、准备与 Release、解谜玩法和失败回退。

现有自动接入依赖 `CampaignPlayground`、其 `city` 引用及 `TransitionFinished`。如果重写 Level 1 的流程，可以保留这个信号，或在协调移除旧过渡控制器后改用下面的通用适配器。一个场景只保留一个负责存档和退出的适配器。

## 独立场景

1. 把关卡场景加入对应 Build Profile。打开 **Campaign City**，选中 `CampaignHub`，在 `destinations` 对应索引填写完整路径，例如 `Assets/Scenes/Level02.unity`。
2. 在关卡根上挂 `CampaignLevelAdapter`。使用 `GeometryLevel` 时把它拖入 `puzzle`，保留正常的 `BallReleased` 和 `GoalReached`。生成球后适配器自动进入准备，关卡原有 Release 控件负责启动球。关闭 `BallSpawn.autoRelease`。
3. 使用其它玩法控制器时，给 `CampaignLevelAdapter` 显式调用 `ReportPrepared()`、`ReportReleased()`、`ReportFailed()` 和 `ReportCompleted()`。失败重置完成后再报告准备。也可以订阅 `PhaseChanged` 更新关卡界面，读取 `Slot` 获取入口索引。索引从 0 开始。
4. 选填 `exitMotion` 和 `completionEffect`，用 `GeometryPose` 指向美术锚点。退出时适配器禁止再次触发，保存完成进度并恢复选关镜头。关卡自定义的失败判定单独调用 `ReportFailed()`。
5. 试玩一次入场、准备、Release、失败重试、完成、暂停返回和重新进入。未加入构建的场景会露出 **Systems preview**，便于区分正式关卡与试玩场。

Level 1 的场景、Prefab、物理和艺术资产属于关卡制作方。全局工作可在 [Campaign City](Assets/Scenes/Archive/BounceBackCampaign/Campaign%20City.unity) 里独立验证。并行合作时避免重新生成别人正在制作的场景。
