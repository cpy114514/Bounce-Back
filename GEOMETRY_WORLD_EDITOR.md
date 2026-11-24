# Geometry World Editor 使用指南

适用于本项目的 Unity 6.3、URP 和 Input System。打开 **Tools → Geometry World Editor**。窗口可以停靠在 Scene 或 Inspector 旁边。现有的 **Geometry Level Editor** 仍负责绘制 2D 节点和连线。

## 从示例开始

1. 在 **Level** 点击 **Open Demo**，打开 **Echo Court**。
2. 点击 **Save and Playtest**。在 Game 视图点击左侧平台前缘的薄荷色圆形开关。转盘旋转后，两个锚点之间的通道打开。
3. 点击场景里的活塞、按 **Space**，或点击 **Push**。活塞推出球后回位。球沿跑道穿过空间连接、在杏色弹跳垫上弹起，并停在终点。试玩界面会显示 **Complete**。
4. 拖动绿色立柱体验弹性形变。**R** 将球送回生成点或检查点。**Restart Level** 重载关卡和机关状态。**Stop and Return to Editing** 返回保存的编辑状态。

**Tools → Open Geometry Trigger Run** 打开独立的试玩关卡。进入 Play Mode 后点击活塞、按 **Space** 或点 **Push**。球经过触发区，转盘会让开跑道，随后球从弹跳垫跃过缺口到达高处终点。

示例中的音频是随项目保存的原创程序合成占位音色。可以在 **Look → Adaptive audio** 选择音频控制器，换成导入的音乐、滚动声和事件音效。

## 建关卡和复用资产

1. 在 **Level → Create Level** 新建 Unity 场景。场景自带正交相机和灯光。
2. 点击 **Build in Scene**，或从顶部菜单打开 **Tools → Build Geometry in Scene View**。从 **Shapes** 或 **Gadgets** 拖出形体或机关，光标下会出现半透明预览。松手放置并选中 Starter Prefab 实例，也可以先点托盘条目，再点落点。按 **Esc** 取消，放置后按 **Ctrl+Z** 可一次撤销。开启 **Surface** 后，指向实体时贴在表面，指向空处时放到 **Floor Y**。
3. 点 **Workplane** 再点一个面，可在墙面、平台或斜坡上铺设网格。新形体的底面会对齐工作平面。**Grid Snap** 使用 Unity 的移动步长。**Grab** 沿工作平面拖动，**Lift** 拉近或拉远。Grab 时按住 **Shift** 可临时升降。点 **Clear** 回到默认地面，按 **Esc** 取消当前操作。
4. 选中物体后，在 **Size** 输入宽、高、深，触发区对应 **Volume**。改尺寸时保留底部中心。展开 **Transform** 可输入世界坐标和旋转角度。按 **W / E / R** 切换 Unity 的移动、旋转和缩放手柄。
5. 在 **Create → Browse model assets** 可复用已有模型。将模型拖进 Scene，或点击 **Place Linked Model**。普通 Unity Prefab 也可以从 Project 拖进场景。
6. 在左侧层级中用 **Shift** 或 **Ctrl** 点击多选。使用 **Group**、**Reparent selection** 或 **Align** 整理场景。圆点控制 Scene 中的可见性，方框控制拾取。Mesh 标签底部提供 Unity 的平移和角度步长设置。
7. 移动自动生成的球和活塞起点，在 **Gadgets** 摆好终点和机关。点击 **Validate Level** 查看配置提示，再点击 **Save and Playtest**。

**Save As** 切换到新场景文件。**Duplicate Level** 保存场景副本并保留当前打开的场景。场景和 Prefab 可以共享同一模型资产。对某个实例做独立修改前，在 **Mesh** 点击 **Make Model Unique**。**Center Pivot** 会为该实例创建独立模型，同时保留几何体的世界位置。生成的模型和材质存于 `Assets/GeometryWorldLibrary`，撤销场景放置后仍可从库中复用。

### 运行时搭方块

进入 Play Mode 后，在 **Game** 视图点左下角 **Build** 或按 **B**。搭建模式使用第一人称自由飞行。用鼠标转向，**WASD** 移动，**空格 / 左 Ctrl** 升降，**左 Shift** 加速。按 **B** 恢复关卡原来的镜头并回到试玩。

- **E · Backpack**。背包收纳基础形体和机关，可按 **Shapes / Gadgets** 筛选。点击下方快捷栏格子，再点击背包里的物品即可换入。飞行中用 **1–9** 或鼠标滚轮切换快捷格。
- **搭建**。用准星瞄准实心面，右键放置选中的物品，左键破坏搭建过的物品，**R** 旋转 90°。绿色预览表示可以放置。背包中的弹跳垫、史莱姆和平台沿用原有玩法，起点的球和活塞由关卡单独管理。
- **V · Sculpt**。普通 **Block** 可雕刻为 4 × 4 × 4 小格。左键挖一格，右键补一格。雕刻会更新碰撞体，再按 **V** 回到搭建。
- **Esc** 释放鼠标，点 **Resume Flight** 继续飞。点 **Save Blocks & Exit** 把搭建、破坏和雕刻写回 Unity 场景。保存后的方块可在下次试玩继续雕刻，整次改动也可以在编辑模式撤销。

## 2D 绘图和建模

- 在 **Create → From 2D drawing** 选现有绘图资产。**Solid** 挤出闭合轮廓，**Surface** 生成平面，**Beams** 按每条边生成梁，可以沿用画笔宽度和颜色。只转换可见图层，生成的模型保留对原绘图的引用。
- 实体和平面要求每个连通轮廓闭合、无分支、无自交。有效的轮廓会继续生成，问题会显示在窗口底部。需要转换开放路径或分支路径时用 **Beams**。内圈当前会作为独立面处理，带孔轮廓需要拆分绘制。
- 在 **Mesh** 选 **Vertex**、**Edge** 或 **Face**。在 Scene 视图点顶点、边中点或面中心，使用手柄拖动。顶点模式支持 Shift 多选和合并，**Esc** 返回 Object 模式。
- **Extrude Boundary Edge** 适用于只有一个相邻面的边。**Bevel Convex Edge** 适用于闭合凸体上的单条锐边。**Inset Convex Face** 适用于平面凸面，距离用面中心的缩放比例控制。操作条件不合适时，窗口会提示如何调整。
- **Decorative** 会关闭该模型的生成碰撞体。**Object material** 修改模型实例的主材质，**Face material** 给当前面分配材质。修改共享模型的面材质会影响引用该模型的实例。

## 拼接机关、事件和声音

- 在 **Project → Assets → GeometryWorldLibrary → Prefabs → Starter → Metric** 可以直接拖放统一单位的形体和机关。网格与触发体使用 1 米基准，未缩放的父对象下，Scale 的 X、Y、Z 分别表示宽、高、深的米数。平台等模板通过默认 Scale 保留各自的比例。**Geometry Build → Size (m)** 会计入父级缩放，可直接填写实际尺寸；旧模型可点 **Normalize Scale** 换算到 1 米基准，当前大小和枢轴位置会保留。
- 每个 Geometry World 关卡有一组球和活塞。点击 **Level → Select Ball and Piston** 选中起点，或用 **Grab** 把活塞与球一起移动。转动起始对象会改变出球方向。选中后可在 **Geometry Build** 调整球的直径、出球速度、活塞行程、推进和回位时间。老关卡可用 **Tools → Set Up Ball and Piston** 补齐起点。
- 球在起始状态贴住推板，前方畅通时随推板走完行程。如果遇到实体障碍，推板会提前停下并回位，球在接触处转为自由滚动。Scene 中的 **Ball Start** 标记显示静止时的球心，`Spawn` 的变换表示完整行程结束时的球心。
- 选中整组起点，在 **Geometry Build** 点 **Snap to Block**，再点击实体方块或平台的顶面。球会对齐顶面和中心线，出球朝向扣到最接近当前方向的方块轴。点顶面上不同的位置可以沿出球轴摆放，按 **Esc** 取消。
- 活塞的石拱外壳带有封闭背墙，推杆伸缩时始终插在固定导向座内。可编辑的 Blender 源模型位于 `ArtSource/Architectural Piston.blend`，Unity 的网格位于 `GeometryWorldLibrary/Prefabs/Starter/Art/Piston`。
- **Gadgets** 可以直接拖入终点、弹跳垫、开关、移动平台、旋转平台、触发区、弹性平台、检查点、重置区和空间锚点。首个终点会自动分配给当前关卡，后续可选中其它终点并点 **Use as Goal**。开关和触发区可以用 **Link Action** 接到平台或弹性物体。锚点用 **Connection** 连接。
- 选中开关或触发区，点 **Link Action**，再点移动平台、旋转平台或弹性平台。选中控制源时，虚线会标出目标。选中平台可直接修改 **Travel**、**Turn (°)** 和 **Seconds**，并在 Scene 查看行程或转动后的形状。连线后，这些字段会编辑驱动平台的序列动作。选中弹跳垫可改 **Speed (m/s)** 和 **Direction**，Scene 中的箭头会同步更新。
- **Slime** 是半透明的绿色弹性方块，默认尺寸为 1 × 1 × 1 米。球撞上时，方块会沿撞击方向压缩并回弹，球会沿接触面的法线弹开。Inspector 里的 **Bounce Ball** 控制碰撞反弹，**Restitution** 调整弹回速度的比例。试玩时也可以拖动方块；**Move Collider** 与 **Scale Collider** 控制拖动时碰撞体是否跟随形变。
- **Connection** 需要手动指定 Source 和 Destination。**Manual** 用 **Enabled By Logic** 控制。**Camera Aligned** 在此基础上比较两个锚点在指定相机中的位置。出口朝向、速度转换、转移时长和出口偏移可单独设置。
- 为对象添加 **Puzzle Sequence**，在 Inspector 的 **Actions** 列表里逐项添加动作。将序列分配给开关、Trigger、碰撞、移动完成、连接激活或终点事件。移动和旋转动作会完成动画再运行下一项。需要额外停顿时插入 **Wait**。
- 音频控制器支持静态氛围和活动音乐层、滚动循环与节流的一次性音效。序列可以发出音频事件。速度、碰撞强度、机关移动和通关状态会影响声音。

## 制作工具与 AI 接手流程

在另一台电脑继续迭代，可直接复制[台式机 Codex 交接提示词](CODEX_HANDOFF.md)。

| 工具 | 在这个项目中的用途 |
| --- | --- |
| Unity CLI | 用 `unity status` 确认当前项目及 Editor 连接。用 `unity command` 查看场景和工具，用 `unity test` 跑测试。 |
| Unity MCP | 通过 Unity CLI 自带的 `unity mcp` 连接正在运行的 Editor。可读场景层级、检查组件和控制台，也可在确认目标后编辑场景和 Prefab。 |
| Blender MCP | 在 Blender 中制作形体、道具、UV 和动画，检查网格并导出资产。Unity 内的碰撞、机关和试玩继续使用项目已有组件。 |

### Unity CLI 与 Unity MCP

1. 在 Windows 安装 Unity CLI 可用 `winget install --id Unity.CLI -e`。检查 `unity --version`，在 Unity 中打开 Bounce Back。项目已声明 `com.unity.pipeline`，打开 Editor 后通过 `unity status` 确认状态为 `ready`。如果 Pipeline 停止，在 Unity 点 **Window → Pipeline → Start Server**。
2. 在仓库根目录运行以下命令。使用 `--project-path` 可以明确选中当前工作区，特别是同时打开测试副本时。Codex 通过本机 stdio 启动 MCP，其他客户端可用相同的命令和参数。

```powershell
$project = (Get-Location).Path
$unityCli = (Get-Command unity).Source
unity status
unity command scene list_open --project-path "$project" --format json
codex mcp add unity -- "$unityCli" mcp --tool-groups --project-path "$project"
codex mcp get unity
```

3. 新启一个加载了配置的客户端会话。列出 Unity MCP 工具后，对 `scene` 工具传入 `{"command":"list_open"}`。返回值里的 `isActive`、`isDirty` 和场景路径用于判断当前编辑目标。接着可用 `{"command":"get_hierarchy"}` 读取活动场景的对象树。如果 CLI 能连接而 MCP 工具没出现，检查客户端是否加载了新配置。

本项目使用 Unity CLI 提供的 MCP 入口。Unity AI Assistant 包里的旧 MCP 服务已由这个入口接替。[Unity CLI 文档](https://docs.unity.com/en-us/unity-cli/use-unity-cli) · [Unity MCP 迁移文档](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli) · [Codex MCP 配置](https://learn.chatgpt.com/docs/extend/mcp?surface=cli)

### Blender MCP

1. 安装 Blender 和 `uv`。Windows 上可用 `winget install --id astral-sh.uv -e` 安装 `uv`。重新打开终端后运行以下命令。本机验证使用 Blender 5.3 和 `mcp-for-blender` 2.3.1。

```powershell
uvx mcp-for-blender@2.3.1 install-addon
$uvxCli = (Get-Command uvx).Source
codex mcp add blender --env DISABLE_TELEMETRY=true --env BLENDER_MCP_SAFE_MODE=1 -- "$uvxCli" mcp-for-blender@2.3.1 --host 127.0.0.1 --port 9876
codex mcp get blender
```

2. 如果安装器找不到插件目录，在 Blender 的 Python Console 用 `bpy.utils.user_resource('SCRIPTS', path='addons', create=True)` 确认目录，然后给安装命令设置 `BLENDERMCP_ADDONS_DIR`。Microsoft Store 版的实际文件可能位于 Blender 包的 `LocalCache/Roaming/Blender Foundation/Blender/<版本>/scripts/addons`。如果终端找不到 `uvx`，可以从 WinGet 的包目录获取 `uvx.exe` 完整路径。
3. 在 Blender 的 **Edit → Preferences → Add-ons** 启用 **MCP for Blender**。保持 Blender 打开，在 3D 视图按 **N**，检查 MCP 面板。若未启动，点 **Connect to MCP server**。本项目使用本机回环地址和默认端口 9876。
4. 新启一个加载了配置的客户端会话，调用 Blender MCP 的 `get_scene_info`，核对返回的场景名和对象。MCP 使用对应的 Blender 实例。开始建模前确认当前文件和工作集合。

[MCP for Blender 安装与连接说明](https://github.com/ahujasid/mcp-for-blender#quickstart)

### 从模型到可玩的机关

1. 先用 Unity MCP 或 CLI 读活动场景与层级。需要方块、斜坡、台阶、工作平面或基础机关时，优先用现有 **Geometry Build** 和 `LevelAuthoring`。复杂外形、UV 或动画在 Blender 中制作。
2. 在 Blender 检查尺寸、局部轴、枢轴点、法线和材质槽。保留可编辑的 `.blend` 源文件，将所需网格导出为 FBX 后放入 Unity 的目标资产目录。导入后在 Scene 视图核对比例、朝向、枢轴和材质。
3. 在 Unity 用 Prefab 或现有模型资产组装关卡，沿用 `GeometryObject`、`PuzzleSwitch`、`PuzzleTrigger`、`PlatformMotion` 等已有组件。在 **Validate Level** 检查配置，随后试玩一条完整的球路。更新已有资源时保留 `.meta`，以维持场景和 Prefab 的引用。

## 验证和发布

在 Unity Test Runner 的 **EditMode** 和 **PlayMode** 分别运行 `BounceBack.GeometryWorld` 测试。发布时在 Unity 的 **Build Profiles** 将要导出的关卡场景加入对应的 Web 场景列表。编辑器程序集仅在 Unity Editor 中加载，构建会移除场景物体上的几何编辑元数据。

当前项目有活动编辑会话时，可以把最新的 `Assets`、`Packages` 和 `ProjectSettings` 放在独立测试目录，避免争用 Unity 项目锁。在 PowerShell 把 `$project` 指向测试副本，再运行：

```powershell
unity test "$project" --mode EditMode --filter BounceBack.GeometryWorld.Tests -o "$env:TEMP/bounceback-editmode.xml"
unity test "$project" --mode PlayMode --filter BounceBack.GeometryWorld.Tests -o "$env:TEMP/bounceback-playmode.xml"
```

如果 CLI 找不到此项目的 Unity 版本，可给测试命令加 `--editor-path` 并指定 `Unity.exe` 的完整路径。
