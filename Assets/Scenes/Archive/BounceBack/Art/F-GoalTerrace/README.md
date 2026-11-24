# F-GoalTerrace · 终点露台、门亭与瀑布口

## 交付范围

五件纯视觉静态模块，单位为米，Unity 中 **+Y 向上、+Z 向前**。不含 Collider、Rigidbody、动画、粒子、外部贴图或玩法组件；不给正式场景添加脚本。

- Blender 源文件：`ArtSource/Prototypes/F-GoalTerrace/F-GoalTerrace.blend`
- 可重跑 Blender 脚本：`ArtSource/Prototypes/F-GoalTerrace/build_f_goal_terrace.py`
- Unity 导入、Prefab、验证和预览生成脚本：`ArtSource/Prototypes/F-GoalTerrace/unity_build_preview.cs`
- Unity 资产：`FBX/`、`Materials/`、`Prefabs/`
- 尺寸和源网格检查：`Documentation/FGoalTerrace_Geometry.json`
- Unity 实际导入检查：`Documentation/FGoalTerrace_Unity_Validation.txt`
- 独立预览：`Preview/FGoalTerrace_Preview.unity`

预览场景不是正式关卡，不应加入战役或 Build Settings。正式场景、作者脚本、玩法、ProjectSettings 和 Background 均不在本任务修改范围内。

## 锚点、轴心与放置

**所有五件模块共用同一个轴心：Goal Terrace Art `(4.90, 0.83, 4.10)`。**

推荐使用 `Prefabs/FGoalTerrace.prefab`：放到上述世界坐标，外层旋转 `(0,0,0)`、缩放 `(1,1,1)`。五件独立 Prefab 的本地位置也均为零，可按需只使用其中几件。

门亭的斜向、拱形和所有装饰都已写入网格坐标。不要为了对准门亭而旋转外层 Prefab。FBX 内部子节点保留 Blender→Unity 的单位和轴向换算，不能再次归一化其旋转或缩放。整个组合可随 Goal Terrace Art 锚点一起折叠移动。

## 模块尺寸与三角面数

下表是放在锚点后的 **Unity 世界轴对齐包围盒**；不是把坡道斜长或旋转门亭的局部宽度误当作世界宽度。源网格精确数值见 Geometry JSON，Unity 导入复测见 Validation 文档。

| FBX / Prefab（同名） | 世界包围盒尺寸 X×Y×Z（m） | 三角面数 | 内容 |
| --- | ---: | ---: | --- |
| `FGoalTerrace_Terrace` | `3.436 × 0.660 × 3.668` | 2124 | 精确露台核心；朝 `-Z` 五龛、朝 `+X` 五龛；两道退台线脚 |
| `FGoalTerrace_Cascade` | `1.747 × 0.700 × 3.112` | 60 | 真实八角斜 Lip、压力板支撑地面、杏色下缘 |
| `FGoalTerrace_Pavilion` | `2.052 × 2.535 × 0.960` | 1000 | 前后贯通拱、四柱、蓝穹顶及终点小顶饰 |
| `FGoalTerrace_Railing` | `2.100 × 0.500 × 0.490` | 192 | 缩短的 `+Z` 矮栏杆、两个空盆栽底座 |
| `FGoalTerrace_Waterfall` | `1.348 × 2.640 × 2.320` | 516 | 复用剖面的石檐、拱形出口、静态水舌 |
| **合计** | 组合包围盒 `5.726 × 4.870 × 3.919` | **3892** | 每件均低于 15k；每 Mesh 不超过 4 个材质槽 |

## 关键构造尺寸

### 露台立面

- Landing Terrace 碰撞核心：中心 `(2.30,0.63,4.72)`，尺寸 `3.40×0.66×3.65 m`，顶面 `Y=0.96`。
- 上部饰带和下部玫瑰色基座由 `Y≈0.815`、`Y≈0.38` 两道细线脚划分。
- 拱龛内衬使用 `MV_Lilac`。为了同时保留原盒碰撞和 ≤0.02 m 的表面容差，拱龛采用**浅浮雕式退龛**，最大出挑 **0.018 m**，没有做需要分级碰撞的深凹槽。

### 终点门亭

- 门亭中心：`(4.90,0.78,4.23)`；本地 `+Z` 朝来球点 `(4.6,6.6)`，Y 朝向约 `-7.214°`，烘焙在网格内。
- 柱中心跨距约 `1.70 m`；保守净宽 `1.58 m`。
- 起拱线 `Y=1.63`，拱内最高点 `Y=2.42`，相对抬起板面 `Y=0.78` 的净高为 **1.64 m**。
- 蓝色穹顶最高 `Y=3.04`，小顶饰最高 `Y=3.12`。
- 前后均开敞，不做封闭背墙。左柱脚楔切贴合露台／Lip，右柱脚落在瀑布侧静态檐座上。
- 它是**朝向入球方向的终点背景门亭**，不假定球必须继续滚到门亭中心；球路由现有玩法决定。

### 瀑布与栏杆

- 石檐安装原点保持 Waterfall Slot `(5.72,0.43,5.24)`，出水朝 `+X`。
- 沿用现有 WaterfallOutlet Stone Eave 的剖面，宽度缩为 `1.04 m`，重映射为 `MV_` 材质；中央保留下水缺口，两端檐座承托门亭右柱。
- 拱形出水平面前伸到 `X≈6.02`，最近立面 `X=5.96`，避开实际终点侧面的净空。静态水舌底端 `Y=-1.75`。
- 北侧栏杆范围约 `X=0.72–2.80`、`Z=6.45`，顶面 `Y=1.46`，在入球走廊前结束。
- 空底座中心 `(0.95,1.02,6.18)`、`(2.05,1.02,6.20)`；**没有植物**，植物由任务 H 制作。

## 材质

`MV_Ivory`、`MV_IvoryShade`、`MV_Rose`、`MV_Lilac`、`MV_Apricot`、`MV_DomeBlue`、`MV_Water`、`MV_Gold`。所有材质为本任务目录内的 URP/Lit、不透明、无贴图材质，颜色使用任务指定的 sRGB 数值。

`MV_Gold` 只用于门亭终点小顶饰；`MV_Water` 只用于水舌；没有把薄荷色用于静态建筑。

## 给任务 I 的整合注意事项

1. 保留 Goal Terrace Art 的 Transform、PoseTrack、现有碰撞和玩法对象，只替换／隐藏对应的占位 **MeshRenderer**。
2. 推荐保留现有动态 `Plate Surface Slot` 和 Gold Inlay。**本任务 Cascade 内的 `Cascade_Floor_Collision_Aligned` 是独立预览用静态地面；正式整合保留动态板时，关闭这个静态地面节点的 Renderer，避免抬起状态下 `Y=0.78` 顶面重复。** Lip、两侧下缘和其他模块不受影响。
3. 隐藏旧 Waterfall Cornice、旧 Fall Ribbon、旧 Cascade Baluster／Rail 的占位 Renderer，避免与替换件重叠；不要删除它们可能承载的父节点或玩法引用。
4. 本交付没有改动或验证正式球路。任务 I 整合后仍需在隔离副本中进行实际 Play Mode 通关和折叠检查。

## 重建与隔离验证

```powershell
blender --background --factory-startup --python-exit-code 1 --python "ArtSource/Prototypes/F-GoalTerrace/build_f_goal_terrace.py"
```

Unity 脚本应在 `.utmp/F-GoalTerrace-QA` 隔离副本中复制到其 `Assets/Editor/FGoalTerraceDelivery.cs`，再用项目指定的 Unity `6000.3.23f1` 执行：

```powershell
Unity.exe -batchmode -projectPath ".utmp/F-GoalTerrace-QA" -executeMethod FGoalTerraceDelivery.Build -quit
```

不要用 `-nographics`，截图需要实际渲染。交付截图为：

- `Preview/FGoalTerrace_Official_1920x1080.png`：正式正交相机，Size `9.5`，从 `(12,15,-15)` 方向看向 `(-0.05,0.95,0.25)`。
- `Preview/FGoalTerrace_ReverseFromGoal_1920x1080.png`：相机位于 `(5,1.5,5.2)`，正交 Size `6.5`，回看塔顶。

预览中保留布局 JSON 的白模参考；目标 Terrace／Lip／Floor 的重复白模 Renderer 关闭，以免与对齐网格争面。其余白模和 B-ReleaseTower 仅用于比例、球路与镜头检查，不属于 F 的正式集成。

## 本次实际验证结果

- Unity `6000.3.23f1` 隔离副本批处理最终退出码 `0`，验证报告末尾为 `RESULT=PASS`。
- 实际导入总计 **3892 三角面、9 个 MeshFilter**；五个模块分别为 `2124 / 60 / 1000 / 192 / 516`，最大单 Mesh 4 个材质槽。
- Terrace、真实斜 Lip、Pressure Plate Floor 各 8 个角点的最大最近顶点误差分别为 `0.00000061 / 0.00000030 / 0.00000030 m`，总体最大 **0.00000061 m**，低于 0.02 m 要求。
- 沿路线 42 个采样，X/Z 各扩展 0.60 m、路面上方 1.30 m；对 3856 个非承托三角面做 AABB 宽阶段和 13 轴 SAT，**相交 0**。三个承托核心共 36 三角面单独按全部 24 个碰撞角点检查，不与自身道路净空混算。
- Blender 源网格全部闭合，非流形边为 0，零面积面为 0。
- F Prefab 中 Collider、Rigidbody、MonoBehaviour、Camera、Light、Animation、Animator 均为 0；只保留 8 个正式 URP/Lit 材质，无贴图依赖。
- 两张截图均为实际 Unity URP 渲染的 1920×1080 PNG，尺寸与非空像素检查通过，并已人工查看。

正式 `Santorini Gameplay` 未打开到隔离流程、未保存或修改；本任务没有宣称完成正式场景 Play Mode 通关，球路整合及动态板复测属于任务 I。
