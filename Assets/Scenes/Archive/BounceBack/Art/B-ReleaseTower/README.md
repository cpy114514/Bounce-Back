# B-ReleaseTower

Santorini 释放塔静态美术模块。三段收分塔身、杏色檐口、窄拱窗、低栏发射平台，以及西北后方的细高蓝穹顶钟楼。仅交付美术与预览，**未修改或整合进 `Santorini Gameplay.unity`**。

## 使用

将 `Prefabs/BReleaseTower.prefab` 实例放到：

```text
世界位置 (-5.64, -0.22, -3.65)
世界旋转 (0, 0, 0)
世界缩放 (1, 1, 1)
```

外层 Prefab 的本地位置、旋转为零，缩放为一。FBX 的轴向和单位换算保留在内部导入节点；不要给外层使用负缩放。

三个子模块也可独立使用。它们共用同一个锚点与本地坐标系，因此放在同一父节点下时，三个子模块的本地位置均为 `(0,0,0)`。

| 模块 | Prefab | 三角面 | 最大材质槽 |
|---|---|---:|---:|
| 三段塔身、0.5 m 基座 | `Prefabs/BReleaseTower_Body.prefab` | 1,260 | 4 |
| 发射承托板与断开的低栏杆 | `Prefabs/BReleaseTower_Platform.prefab` | 252 | 3 |
| 开放拱亭、穹顶、尖顶与三角旗 | `Prefabs/BReleaseTower_BellTower.prefab` | 1,088 | 4 |
| **合计** | `Prefabs/BReleaseTower.prefab` | **2,600** | — |

所有网格均低于每模块 15,000 三角面和 4 材质槽限制。交付 Prefab 不含 Collider、Rigidbody、MonoBehaviour、动画或外部纹理依赖。

## 尺寸、枢轴与对齐

单位：米。坐标：Unity X 横向、+Y 向上、+Z 向前。

- 总枢轴：塔基中心，世界 `(-5.64,-0.22,-3.65)`。
- 精确塔身核心：本地中心 `(0,2.49,0)`；尺寸 `1.77 × 4.02 × 1.79`。
- 精确塔身核心世界范围：X `[-6.525,-4.755]`，Y `[0.26,4.28]`，Z `[-4.545,-2.755]`。
- 基座：世界 Y `[-0.22,0.28]`，高 `0.50`；最大平面尺寸 `2.34 × 2.36`。
- 三段外墙的世界 Y 区间：`0.28–1.46`、`1.58–2.72`、`2.84–4.09`；杏色中间檐口分隔三段。
- 可见 `-Z` 与 `+X` 两面各有三个窄拱窗；蓝色窗芯是浅凹视觉嵌板，不参与碰撞。
- Launch Coping：本地中心 `(-0.27,4.49,0)`；尺寸 `2.83 × 0.36 × 1.92`。
- Launch Coping 世界范围：X `[-7.325,-4.495]`，Y `[4.09,4.45]`，Z `[-4.61,-2.69]`。顶面严格为 **Y=4.45**。
- 栏杆在承托板上方最高 `0.33 m`，低于 `0.35 m` 限制。东侧出口和球道两侧断开，不增加坡道门槛。
- 钟楼位于西北后方，实际平面范围 X `[-7.28,-6.54]`、Z `[-4.58,-4.08]`，尖顶最高 **Y=8.14**。
- 旗帜使用 `MV_Apricot`，不使用球/终点专属 `MV_Gold`；静态塔不使用 `MV_Mint`。

Unity 主项目资产校验中，塔身核心与 Launch Coping 的世界包围盒最大误差均为 **0.000000 m**（显示精度 6 位小数），允许误差为 `0.02 m`。逐顶点数据见 `Documentation/BReleaseTower_Dimensions.json`，导入实测见 `Documentation/BReleaseTower_Unity_Assets.txt`。

## 材质

`Materials/` 包含完整 `MV_` 调色板，颜色值按 sRGB 记录，URP/Lit，Smoothness `0.18`，Metallic `0`：

| 材质 | sRGB | 本模块用途 |
|---|---|---|
| `MV_Ivory` | `(0.95,0.88,0.77)` | 主墙面、拱亭、栏杆 |
| `MV_IvoryShade` | `(0.86,0.76,0.70)` | 预览白模；保留调色板 |
| `MV_Rose` | `(0.74,0.45,0.47)` | 基座与承托板下缘 |
| `MV_RoseDeep` | `(0.55,0.30,0.38)` | 保留调色板 |
| `MV_Lilac` | `(0.50,0.45,0.66)` | 拱亭内顶阴影 |
| `MV_Apricot` | `(0.90,0.58,0.37)` | 檐口、栏杆压顶、尖顶与旗帜 |
| `MV_DomeBlue` | `(0.30,0.47,0.80)` | 窗芯与穹顶 |
| `MV_Mint` | `(0.36,0.70,0.55)` | 仅保留调色板；本静态塔不使用 |
| `MV_Water` | `(0.45,0.80,0.84)` | 保留调色板 |
| `MV_Gold` | `(0.96,0.70,0.32)` | 仅预览参考球；塔本身不使用 |

## 预览与验证

- `Preview/BReleaseTower_Preview.unity`：塔位于规定锚点，附正式相机、反向相机、27 项 JSON 白模参考、现有活塞和直径 0.76 m 金球参考。
- `Preview/BReleaseTower_Formal_1920x1080.png`：正式镜头，位置 `(11.95,15.95,-14.75)`，焦点 `(-0.05,0.95,0.25)`，Orthographic Size `9.5`。
- `Preview/BReleaseTower_ReverseFromGoal_1920x1080.png`：从 `(5,1.5,5.2)` 朝向塔的反向镜头。
- 场景重载、查询碰撞和渲染在 `.utmp/B-ReleaseTower-QA` 隔离副本执行；报告为 `Documentation/BReleaseTower_QA_Report.txt`。
- 预览中的重合白模 Launch Coping 与塔身参考盒被隐藏，仅由新塔替换显示；其余布局盒保留用于检查接缝和路线。
- QA 时临时添加 MeshCollider 做禁区与视线查询，查询后立即销毁；场景中的塔 Prefab 仍无碰撞体。
- 实测结果：活塞禁区 `0` 个重叠，球道净空区 `0` 个重叠；球心、球顶、活塞外壳、推杆与首段路线的 `6/6` 条正式镜头视线畅通。钟楼比最高白模参考高 `3.335 m`。

## 可编辑源与重建

源文件位于 `ArtSource/Prototypes/B-ReleaseTower/`：

- `BReleaseTower.blend`：可编辑网格、材质、模块集合与源预览相机。
- `build_b_release_tower.py`：完整、可重复运行的 Blender 生成器，包含尺寸断言、FBX 导出、三角面/包围盒报告。
- `import_b_release_tower.cs`：Unity 材质创建、FBX 重绑定、Prefab 包装和尺寸校验。
- `build_b_release_tower_preview.cs`：隔离 QA 场景与截图生成器；只应复制到 QA 项目的 Editor 目录运行。

Blender 重建：

```powershell
blender.exe --background --factory-startup --python-exit-code 1 --python ArtSource/Prototypes/B-ReleaseTower/build_b_release_tower.py
```

主项目资产重建（通过 Unity CLI 外部脚本入口）：

```powershell
unity command script run --project-path <项目路径> --file ArtSource/Prototypes/B-ReleaseTower/import_b_release_tower.cs --entry BReleaseTowerImport.Run
```

正式关卡整合、移除旧白模及保存 `Santorini Gameplay.unity` 留给任务 I。
