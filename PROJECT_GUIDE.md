# Bounce Back 项目指南

Bounce Back 是一款 3D 滚球解谜游戏。玩家用活塞推出球，通过坡道、弹跳垫和机关把球送到终点。关卡可以在 Unity 的 Scene 视图里搭建，也可以进入 Play Mode，像搭积木一样飞行、放置和雕刻。

## 打开项目与试玩

试玩完整战役时，打开 [Campaign Boot](Assets/Scenes/Archive/BounceBackCampaign/Campaign%20Boot.unity) 并进入 Play Mode。首次启动进入 [Santorini Gameplay](Assets/Scenes/Archive/BounceBackSystems/Santorini%20Gameplay.unity)。水平拖动两块史莱姆、竖直拖动蓝色反射器，各对准金色轨道标记。松手时机关会吸附到最近的轨道档位。准备好后点击 **Release** 或按 `Space`。准备和滚球时可按住鼠标右键拖动来转动视角，也可用 `Q` / `E` 左右旋转，按 `Home` 恢复默认角度。通关后球会滚上选关道路。点按 `A` / `←` 或 `D` / `→` 选择入口，停稳后按 `E` 或 `Enter`。关卡里按 `Esc` 可暂停并返回选关。

通关第一关后，在选关城市进入第二个入口即可玩 [The Land Without the Sun](Assets/Scenes/Archive/BounceBack/The%20Land%20Without%20the%20Sun.unity)。第二关通过拖动石板、操作机关和调整视角来接通球路。第三、第四个入口目前连接通用试玩。

直接打开 [Campaign City](Assets/Scenes/Archive/BounceBackCampaign/Campaign%20City.unity) 进入的是系统预览，使用独立的试玩进度。正式战役从 Campaign Boot 开始。流程、关卡接口和建模规格见 [全局系统](GLOBAL_SYSTEMS.md)、[关卡接入](LEVEL_INTEGRATION.md) 和 [美术交接](ART_INTEGRATION.md)。

1. 在 Unity Hub 添加本项目的根目录，也就是包含 `Assets`、`Packages` 和 `ProjectSettings` 的目录。用 Unity **6.3 LTS（6000.3.23f1）** 打开。换电脑时按 [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) 安装对应版本。项目使用 URP 和 Input System，目标平台为 WebGL。
2. 打开 [level1](Assets/Scenes/level1.unity) 试用搭建工具。第一次试机关，可以从 **Tools → Geometry World Editor → Level → Open Demo** 打开 [Echo Court](Assets/GeometryWorldDemo/Echo%20Court.unity)，或用 **Tools → Open Geometry Trigger Run** 打开 [Trigger Run](Assets/GeometryWorldDemo/Trigger%20Run.unity)。
3. 进入 Play Mode，在 Game 窗口按 `Space` 推球，按 `R` 将球送回起点或检查点。**Restart Level** 会连同机关状态一起重置。Trigger Run 可以用来验证球经过触发区、转盘让开跑道、球经过弹跳垫进入终点的流程。

## 在 Scene 视图制作关卡

1. 打开 **Tools → Geometry World Editor**。在 **Level → Create Level** 新建关卡，或用 **Duplicate Level** 保存一份现有关卡的副本。
2. 点 **Build in Scene**，从 **Shapes** 或 **Gadgets** 把组件拖入 Scene。松手前会显示放置预览。用 **Surface** 贴到实体表面，用 **Workplane** 选一个面作为工作平面，用 **Grid Snap** 吸附。按 `Esc` 取消，按 `Ctrl+Z` 撤销。
3. 选中物体，在 **Geometry Build** 的 **Size (m)** 填实际宽、高、深。在 **Transform** 调位置和角度。也可以从 [Metric Prefabs](Assets/GeometryWorldLibrary/Prefabs/Starter/Metric) 直接拖形体和机关。资产使用米制基准。
4. 在 **Level → Select Ball and Piston** 选中关卡唯一的一组起点。用 **Snap to Block** 点平台或方块顶面，让球对齐支撑面和出球轴。球在起始状态紧贴推板。选中整组可调球直径、出球速度、活塞行程和回位时间。
5. 放好终点，再用 **Link Action** 把开关或触发区接到移动平台、转盘或弹性物体。想更换终点时，选中对应物体并点 **Use as Goal**。点 **Validate Level** 检查配置，再用 **Save and Playtest** 跑一遍完整球路。

详细的建模、2D 绘图转 3D、机关连线和音频设置见 [Geometry World Editor 使用指南](GEOMETRY_WORLD_EDITOR.md)。

## 飞行搭建、背包和像素雕刻

先把关卡保存成 Unity 场景。进入 Play Mode 后点击 Game 窗口，按 `B` 或点 **Build** 进入第一人称搭建。再按 `B` 返回原来的试玩镜头。

| 操作 | 按键 |
| --- | --- |
| 飞行和转向 | `WASD` 移动，鼠标转向，`Space` 上升，左 `Ctrl` 下降，左 `Shift` 加速 |
| 打开或关闭背包 | `E`。先点击快捷栏格子，再点背包里的物品，把它装进这一格 |
| 切换九格快捷栏 | `1–9` 或鼠标滚轮 |
| 放置、拆除和旋转 | 瞄准实体面，绿色预览表示可以放置。右键放置，左键拆除搭建过的物品，`R` 旋转 90° |
| 雕刻普通方块 | `V` 进入或退出雕刻。普通 **Block** 有 4 × 4 × 4 个小格，左键挖一格，右键补一格，碰撞体随之更新 |
| 暂停飞行和保存搭建 | `Esc` 释放鼠标。**Resume Flight** 继续飞，**Save Blocks & Exit** 退出 Play Mode 并把更改写回场景 |

背包有 **All / Shapes / Gadgets** 三个分类，共 16 种物品。

- **形体**：Block 方块、Platform 平台、Ramp 斜坡、Column 立柱、Cylinder 圆柱、Sphere 球体。
- **机关**：Slime 史莱姆、Bounce Pad 弹跳垫、Goal 终点、Switch 开关、Mover 移动平台、Turntable 转盘、Trigger 触发区、Checkpoint 检查点、Reset 重置区、Anchor 空间锚点。

史莱姆是绿色弹性方块。可在 Inspector 用 **Bounce Ball** 和 **Restitution** 调整碰撞反弹。需要联动的机关放好后，回到 Scene 视图再接线。起点的球和活塞通过关卡的起点工具管理，背包里的 **Sphere** 是搭建用形体。

**Save Blocks & Exit** 是 Unity Editor 内的制作功能。直接停止 Play Mode 会丢弃本次搭建。已有未保存的场景改动时，返回 Edit Mode 后确认场景内容并手动保存。整次搭建可在 Edit Mode 撤销。

## 目录与主要入口

| 位置 | 用途 |
| --- | --- |
| [Assets/GeometryWorld/Editor](Assets/GeometryWorld/Editor) | 3D 关卡窗口、Scene 拖放、参数面板和 Play Mode 搭建回写 |
| [Assets/GeometryWorld/Runtime](Assets/GeometryWorld/Runtime) | 球、活塞、机关、第一人称建造、背包及雕刻逻辑 |
| [Build Catalog.asset](Assets/_Project/Data/Resources/BounceBack/Build%20Catalog.asset) | 背包物品 ID、界面名称、分类及 Prefab 引用。已有搭建会用 ID 找回对应物品 |
| [Assets/GeometryWorld/Tests](Assets/GeometryWorld/Tests) | EditMode 与 PlayMode 测试 |
| [Assets/GeometryWorldLibrary](Assets/GeometryWorldLibrary) | 可复用的模型、材质、Starter Prefab |
| [Assets/GeometryLevelEditor](Assets/GeometryLevelEditor) | 原有 2D 节点、连线和绘图工具 |
| [ArtSource](ArtSource) | 可编辑的 Blender 源文件，含 [Architectural Piston.blend](ArtSource/Architectural%20Piston.blend) |

新素材、手绘 UI 和第三方资产的存放位置见 [资产目录指南](ASSET_STRUCTURE.md)。

## 重构前盘点

当前正式路径是 **Campaign Boot → Santorini Gameplay → Campaign City → The Land Without the Sun → Campaign City**。现有构建还包括旧入口 **Geometric Menu**。旧版 **Santorini**、**Floating City**、**Prototype Trial**、搭建沙盒 **level1** 和机关示例仍在项目里，正式试玩以 Build Profiles 和 `CampaignHub.destinations` 为准。

| 模块 | 当前职责与依赖 |
| --- | --- |
| 球、活塞与机关 | `GeometryLevel` 管出球、重试、终点、部分玩家输入和 HUD。第一关用 `BallSpawn`、`BallBody`、`PistonLauncher` 和 `ElasticGeometry`，两块史莱姆和升降反射器可拖动。第二关复用球和关卡生命周期，由 `SunlessCourtyard` 单独处理石板、连接、镜头和出球，配置允许直接放球 |
| 选关与通关 | 第一关用 `CampaignPlayground` 执行自己的完成演出，再经 `CampaignLevelHandoff` 进入城市。第二关由 `CampaignLevelAdapter` 接入 `CampaignHub`。旧场景还挂有 `CampaignLevelExit`。同一关的完成事件应由一个流程负责写入进度 |
| 转场与提示 | `CampaignScenes` 发起换场，`CampaignSceneParticles` 负责跨场景碎片聚散。已有建筑折叠由 `GeometryPose` 播放。两关共用 `PlatformHints` 的「?」、模型弹跳和 3D 箭头，各自决定哪些机关可以提示 |
| 3D 和运行时搭建 | `GeometryWorldWindow`、`SceneConstruction`、`LevelAuthoring` 提供 Unity Scene 视图拖放与机关配置。运行时飞行、背包、放置、雕刻分别由 `BuildFlight`、`BuildInventory`、`RuntimeBlockBuilder`、`SculptedBlock` 实现，`RuntimeBuildSave` 负责编辑器回写。正式前两关关闭了运行时搭建，适合在沙盒验证这些工具 |
| 存档 | `CampaignProgress` 同时保存正式和预览进度，`CampaignSettings` 保存音量、画质和操作设置。选关入口索引已用于存档，背包已有放置数据引用 Build Catalog 的物品 ID |

### 3D 资产与引用

- [ArtSource](ArtSource) 有 14 个 Blender 源文件。[场景美术包](Assets/Scenes/Archive/BounceBack/Art) 存放导入后的 FBX、材质、Prefab 及美术预览场景。[GeometryWorldLibrary](Assets/GeometryWorldLibrary) 存放可复用模型、关卡素材和米制 Prefab。
- Unity 的递归场景依赖显示，第一关引用 24 个 FBX 和 27 个 Prefab，选关城市引用 19 个 FBX 和 18 个 Prefab。第二关当前的形体和机关组装在场景内，场景依赖中没有外部 FBX 或 Prefab。迁移时需要保留第二关场景自身的层级、枢轴、碰撞体和机关绑定。
- 重构前保留 Blender 源文件和 Unity 导入资产的对应关系。移动或调整现有资产时应保留 `.meta`，检查 Prefab、材质、碰撞体和 Transform 引用。场景依赖盘点不覆盖所有动态加载的 Resources、编辑器工具和样例。

### 后续重构顺序

1. 先固定可回退的版本基线，核对工作区的 Unity Version Control 待提交内容。当前还有改动和新增文件，备份范围应包括场景、代码、美术和对应的 `.meta`。基准试玩包括两关准备、出球与重试、第一关解锁第二关、第二关完成返回选关，以及一次搭建保存回写。
2. 先收拢战役对关卡的接口。把入场、准备、出球、重试、通关和返回统一成一条调用链，先在第二关的适配器上验证，再接第一关的完成演出。
3. 再从 `GeometryLevel` 和 `SunlessCourtyard` 中拆出共用输入、HUD 和提示管理。关卡规则、机关参数、模型层级和镜头构图继续由场景和关卡脚本维护。
4. 最后整理两套搭建入口之间可共用的物品定义、单位、吸附和碰撞检查。用现有沙盒验证预览、放置、拆除、雕刻和场景回写，再决定是否调整资产目录。

## 制作分工与模型交接

- **Codex 负责主要制作**，包括关卡与编辑器、运行时建造、活塞和球的物理、机关、Prefab 组装，以及 Unity 内的集成和试玩验证。
- **Kimi 在 DSH 里协作建模**。适合交给它的任务包括建筑外壳、道具、装饰件，以及网格、UV 和材质槽整理。需要活动的机关由 Codex 指定哪些部件分开、如何对齐，再在 Unity 里接上动作和碰撞。
- **发模型任务时**，写明用途、宽高深（米）、贴地面或安装面、枢轴、正向、活动部件和材质要求。沿用项目的低多边形几何建筑风格，优先用清楚的轮廓、台阶和块面表达造型。例如，活塞的塔楼外壳需要封闭后壁，推杆和推板需要独立网格。
- **收模型时**，把可编辑的 `.blend` 源文件放进 [ArtSource](ArtSource)，同时交付 Unity 使用的 FBX 和所需贴图。Codex 在 Unity 里核对米制尺寸、枢轴、朝向和材质槽，再装配到已有 Prefab，检查网格吸附、碰撞和实玩表现。替换现有模型时保留原来的 `.meta`。

## 换电脑与 AI 接手

1. 先读 [AGENTS.md](AGENTS.md)。从项目根目录检查 Unity CLI，再确认返回的项目路径和活动场景。看到 `isDirty: true` 时先读取现场状态。在隔离副本中做需要重开场景、进入 Play Mode 或占用项目锁的验证。

   ```powershell
   $project = (Get-Location).Path
   unity --version
   unity status
   unity command scene list_open --project-path "$project" --format json
   ```

2. **Unity MCP** 用 CLI 自带的 `unity mcp`，通过 `--project-path` 固定到当前工作区。让 Codex 加载配置后，再用 `scene` 工具的 `list_open` 核对场景、`isActive` 和 `isDirty`。若 CLI 无法连接，在 Unity 检查 **Window → Pipeline → Start Server**。
3. **Blender MCP** 处理复杂网格、UV 和动画。先用 `get_scene_info` 确认连到正确的 Blender 文件。导出 FBX 后到 Unity 检查尺寸、朝向、枢轴和材质。更新已有模型时保留 `.meta`，维持场景与 Prefab 引用。
4. 扩展关卡工具时复用现有的 **Geometry Build**、`LevelAuthoring` 和玩法组件。优先保证拖出即预览、落点明确、常用参数可调，以及保存重开后仍可玩。保留原有 2D 绘图入口和关卡引用。

工具安装和 MCP 注册命令见 [制作工具与 AI 接手流程](GEOMETRY_WORLD_EDITOR.md#制作工具与-ai-接手流程)。要把工作交给台式机上的 Codex，可以复制 [CODEX_HANDOFF.md](CODEX_HANDOFF.md) 中的提示词。

## 测试、提交和 WebGL

- **测试**：修改关卡工具或玩法后，在 Unity Test Runner 分别运行 `BounceBack.GeometryWorld.Tests` 的 EditMode 和 PlayMode 测试。也可在有 `Assets`、`Packages`、`ProjectSettings` 的隔离项目副本里执行下面的命令，查看 XML 中的通过、失败和跳过数。

  ```powershell
  $testProject = (Get-Location).Path
  unity test "$testProject" --mode EditMode --filter BounceBack.GeometryWorld.Tests -o "$env:TEMP/bounceback-editmode.xml"
  unity test "$testProject" --mode PlayMode --filter BounceBack.GeometryWorld.Tests -o "$env:TEMP/bounceback-playmode.xml"
  ```

- **提交**：在 Unity Version Control 的 **Pending Changes** 核对本次场景、Prefab、脚本和相应的 `.meta`，选中本次改动后执行 **Check in**。
- **WebGL**：安装与当前 Unity 版本对应的 Web Build Support。在 **Build Profiles** 选择 Web，核对场景列表，将要发布的关卡加入构建，再运行 **Build and Run** 验证浏览器里的操作和通关流程。
