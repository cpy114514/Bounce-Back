# A-FloatingIsland · 浮岛底座与悬垂岩层

## 交付范围

一件静态视觉浮岛：四层倒阶梯砌体、交替条纹岩层、三根悬垂尖锥、四个盲凹拱窗、一条 `+X` 小阳台，以及贯穿下部轮廓的瀑布凹槽。不是场景整合，不包含水体、云海远景、碰撞、机关、动画或玩法脚本。

- Blender 源：[`ArtSource/Prototypes/A-FloatingIsland/A_FloatingIsland.blend`](../../../../../ArtSource/Prototypes/A-FloatingIsland/A_FloatingIsland.blend)
- 可重跑生成脚本：[`ArtSource/Prototypes/A-FloatingIsland/build_floating_island.py`](../../../../../ArtSource/Prototypes/A-FloatingIsland/build_floating_island.py)
- Unity 模型：`FBX/A_FloatingIsland.fbx`
- 视觉 Prefab：`Prefabs/A_FloatingIsland.prefab`
- 独立预览：`Preview/A_FloatingIsland_Preview.unity`
- 实测记录：`Documentation/A_FloatingIsland_Unity_Validation.json`、`A_FloatingIsland_Blender_Validation.json`、`A_FloatingIsland_Source_Audit.json`

**不要把预览场景加入 Build Settings，也不要用它替换 `Santorini Gameplay.unity`。** 正式场景、生成器、Runtime、ProjectSettings 和 Background 均未由本任务修改。

## 尺寸、锚点与轴心

| 项目 | 数值 |
| --- | --- |
| Unity 局部总尺寸 X×Y×Z | `14.240 × 11.150 × 10.990 m` |
| Unity 局部包围盒最小值 | `(-6.760, -11.150, -5.495)` |
| Unity 局部包围盒最大值 | `(7.480, 0.000, 5.495)`；X 最大值来自阳台 |
| 放置锚点（世界 / `Courtyard and Water Art` 局部） | `(0, -0.22, 0.05)` |
| 轴心 | 顶面中心，Unity 局部 `(0,0,0)` |
| 顶面 | 局部 `Y=0`，即世界 `Y=-0.22` |
| 最底端 | 中央尖锥，世界 `Y=-11.37`，总下垂 `11.15 m` |
| 第一层外轮廓 | 每侧相对地基外扩 `0.58 m` |
| 朝向 | `+Y` 向上；建筑立面朝 `-Z` 与 `+X` |
| Prefab 根变换 | 放置后仅位置为锚点；旋转 `0`、缩放 `1` |

Unity 的轴向修正保留在 FBX 导入子节点中。**请直接使用配套 Prefab，不要清零其子节点旋转或缩放，也不要给外层再叠加轴向旋转。** FBX 导出副本已补偿 Blender→Unity 的 X 手性转换，阳台和瀑布均实测位于 Unity `+X` 一侧。

## 上部四层与下部岩层

每层高度均在任务要求的 `0.6–1.2 m` 内，逐层内收 `0.16 m`：

| 层 | 顶面局部 Y | 高度 | 每侧外扩 | 材质 |
| --- | ---: | ---: | --- | --- |
| 1 · 白石檐冠 | `0.00` | `0.65 m` | `0.58 m` | `MV_Ivory` |
| 2 · 拱窗砌体 | `-0.65` | `0.85 m` | `0.42 m` | `MV_Ivory` |
| 3 · 玫瑰色退台 | `-1.50` | `0.80 m` | `0.26 m` | `MV_Rose` |
| 4 · 深色退台 | `-2.30` | `0.85 m` | `0.10 m` | `MV_RoseDeep` |

下方六段不规则岩层交替使用 `MV_Rose / MV_RoseDeep`，收至三根悬垂尖锥；最低一根至局部 `Y=-11.15`。无写实贴图，无额外装饰粒子。

## 拱窗、阳台、瀑布和东侧通路

- **拱窗：** `-Z` 两个、`+X` 两个，共四个。是真正凹入的拱形几何，蓝色小背板位于凹槽内部，不是穿墙大洞。开口半径 `0.30 m`，凹入 `0.32 m`。
- **阳台：** 位于 `+X`，局部 `Z=0.15`，世界中心约 `(7.01,-1.72,0.20)`；含厚板、三面矮栏和两根三角牛腿。最大世界 `X=7.48`。
- **瀑布：** 出口锚点 `(5.72,0.43,5.24)`。凹槽背壁实测世界 `X=5.7200003`；出口上方余量 `0.10 m`。切削贯通外轮廓，没有残留薄壁。
- **建议水芯：** 世界 `Z=5.04..5.50`，宽 `0.46 m`；`X=5.72` 为背壁，水向其 `+X` 一侧落下。任务 G 可在出口上方保持较宽水舌，但**到达浮岛顶面 `Y=-0.22` 前应收窄到该水芯范围**。水本身不在本任务内。
- **竖向通道：** 检查范围世界 `Y=0.53..-11.72`；Unity 对水芯内部及两侧边界共 `20` 条竖向射线均无网格命中。
- **东侧码头路线：** 按只读参考 `(9.7,0.86,3.83)` 及其后续路径采样，到浮岛三角面的最小距离 `3.14115 m`，未阻挡码头道路。

## 顶面对齐与球路净空

布局 JSON 实际包含 **20 个碰撞盒 + 7 个零尺寸锚点**，没有地基碰撞盒。地基尺寸只读核对自 `SantoriniSystemsAuthoring.cs` 的 `Foundation Crown`：中心 `(0,-0.29,0.05)`、尺寸 `12.36×0.13×9.83`，顶面世界 `Y=-0.225`。任务指定美术顶面为 `-0.22`，因此与现有地基顶面相差 **`0.005 m`**，小于 `0.02 m` 容差。

- Unity 在地基矩形内做 `25×21 = 525` 个顶面采样：无缺失；采样点相对局部 `Y=0` 的最大误差 `0.000000715 m`。
- 美术所有几何均不高于世界 `Y=-0.22`。JSON 中最低碰撞路面顶面为 `Y=0.45`，垂直间隔 `0.67 m`，不会进入路面上方 `1.3 m` 的净空区。
- 正式镜头使用正交 `size=9.5`，焦点 `(-0.05,0.95,0.25)`，位置为焦点加方向 `(12,15,-15)`。下缘越出画面底边，背景与最终云海由其他任务负责。
- 回看镜头位于 `(5,1.5,5.2)`，朝参考塔中心 `(-5.64,2.3,-3.65)` 取景，透视 FOV `70°`。

## 三角面与材质预算

Unity 实际导入：**一个 Mesh、3136 三角面、4 个子材质、1 个 UV0 层、2861 个顶点**（顶点数包含法线/UV 拆分）。非流形边、退化面均为 `0`。

| Unity 子网格槽 | 材质 | 用途 | 三角形 |
| ---: | --- | --- | ---: |
| 0 | `MV_Ivory` | 顶面、白石砌体、阳台 | 1804 |
| 1 | `MV_Rose` | 浅岩层、牛腿 | 448 |
| 2 | `MV_RoseDeep` | 深岩层、悬垂尖锥 | 388 |
| 3 | `MV_DomeBlue` | 四个拱窗背板 | 496 |
| **合计** |  |  | **3136 / 15000** |

Unity/FBX 的子网格顺序与 Blender 材质声明顺序不同；请按**材质名**重映射，不要假定蓝瓷一定在槽 1。配套 Prefab 已按上表正确绑定。

`MV_Gold`、`MV_Mint`、`MV_Water` 只用于预览中的参考金球、可动 Lift Wall 白模和水面白模，**不被浮岛 Prefab 使用**。正式模块不使用 Gold 或 Mint。

## 预览截图

- `Preview/A_FloatingIsland_Formal.png` — Unity 正式镜头，1920×1080。
- `Preview/A_FloatingIsland_FromGoal.png` — 从终点回看塔，1920×1080。
- `Preview/A_FloatingIsland_Blender.png` — 额外的 Blender 单体检查图，不替代上述 Unity 截图。

预览场景中的白模盒按 JSON 的 `center/size/rotation` 生成；另按只读的 `SantoriniSystemsAuthoring.cs` 尺寸补充地基和塔身参考体积，用于覆盖与回看检查。它们不是正式场景整合，也不带碰撞体。

## 给任务 I 的接入提示

1. 在 `Courtyard and Water Art` 下实例化 `Prefabs/A_FloatingIsland.prefab`，局部位置 `(0,-0.22,0.05)`、旋转 `0`、缩放 `1`。
2. 可关闭被覆盖的 `Floating Limestone Foundation`、`Lower Limestone Course`、`Foundation Crown`、`Glazed Foundation Frieze` 的 Renderer；保留原碰撞和玩法对象，不要关闭整个 `Courtyard and Water Art`。
3. 按同名 `MV_*` 材质统一重映射。正式浮岛只使用表中四种材质。
4. 本任务未进入 Play Mode，也未修改玩法或编辑器代码；资产验证和截图全部在 `.utmp/A-FloatingIsland-QA` 隔离副本完成。

## 重建

```powershell
blender --background --python-exit-code 1 --python "ArtSource/Prototypes/A-FloatingIsland/build_floating_island.py"
# 只生成源文件和 FBX，不渲染 Blender 检查图：
blender --background --python-exit-code 1 --python "ArtSource/Prototypes/A-FloatingIsland/build_floating_island.py" -- --no-render
```

Unity 隔离验证脚本：`ArtSource/Prototypes/A-FloatingIsland/UnityValidation/A Floating Island Preview .cs`。其输出路径通过 `--afloat-output` 指定；本次 Unity 版本为 `6000.3.23f1`、URP `17.3.0`。
