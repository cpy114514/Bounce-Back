# K-01 · 圣托里尼教程建筑

## 交付边界

纯视觉资产。单位为米，Unity 中 **+Y 向上、+Z 向前**。所有 FBX 和视觉 Prefab 均不含 Collider、Rigidbody、机关、输入、存档、场景切换或音频。

- Blender 源文件：`ArtSource/Kimi/K-01/K01_Santorini_Architecture.blend`
- 可重建脚本：`ArtSource/Kimi/K-01/build_k01.py`
- Unity 模型：`FBX/`、`Materials/`、`Prefabs/`
- 精确尺寸、局部包围盒、枢轴及放置坐标：`Documentation/K01_Dimensions.json`
- 验证与可见性记录：`Documentation/`；独立 PlayMode 结果：`Documentation/K01_PlayTests.xml`

`Preview/K01_Santorini_Preview.unity` 是独立验证副本，**不是正式场景，不应加入战役或 Build Settings**。正式 `Santorini.unity` 的接线仍由 Codex 完成。

## 对象对应表

下列位置均相对于 `Santorini` 根。没有旋转的道路模块保持父对象原有旋转，不再叠加一次旋转。

| FBX / Prefab（同名） | 正式场景替换入口 | 本地枢轴与放置方式 | 主要尺寸 W×H×D（m） |
|---|---|---|---|
| `K01_Cupola_Tower` | `Architecture Visuals/Cupola Tower` | 地面中心；放到 `(-2.4, -0.7, -9.9)`；关闭原占位 MeshRenderer | 主体约 `1.6×5.2×1.6`；含线脚 `1.74×5.2×1.74` |
| `K01_Blue_Cupola` | `Architecture Visuals/Blue Cupola` | 穹顶安装面中心；`(-2.4, 4.5, -9.9)` | 穹顶主体直径约 `1.65`；含檐口和顶饰 `1.72×1.12×1.72` |
| `K01_Terrace_Foundation` | `Architecture Visuals/Terrace Foundation` | 基座地面中心；`(0, -1.75, -9.9)` | 占位主体 `2.15×5.9×1.8`；含檐口 `2.25×5.9×1.9` |
| `K01_Course_Launch_Terrace` | `Course/Launch Terrace/VisualRoot` | Collider 中心；局部 `(0,0,0)` | 道路主体 `2.4×1.4×2.0` |
| `K01_Course_Stair_1` | `Course/Stair 1/VisualRoot` | 同上 | `2.4×0.45×1.0` |
| `K01_Course_Stair_2` | `Course/Stair 2/VisualRoot` | 同上 | `2.4×0.45×1.0` |
| `K01_Course_Stair_3` | `Course/Stair 3/VisualRoot` | 同上 | `2.4×0.45×1.0` |
| `K01_Course_Smooth_Slope` | `Course/Smooth Slope/VisualRoot` | 同上；保留父对象 X 轴坡面旋转 | `2.4×0.4×4.212173` |
| `K01_Course_Ramp_Approach` | `Course/Ramp Approach/VisualRoot` | 同上 | `2.4×0.42×0.5` |
| `K01_Course_Landing_Walk` | `Course/Landing Walk/VisualRoot` | 同上 | `2.8×0.65×8.2` |
| `K01_Course_Goal_Approach` | `Course/Goal Approach/VisualRoot` | 同上 | `3.3×0.65×6.4` |
| `K01_Course_Goal_Steps_Ramp` | `Course/Goal Steps Ramp/VisualRoot` | 同上；保留父对象坡道旋转 | `6.4×0.4×1.2218426` |
| `K01_Course_Goal_Terrace` | `Course/Goal Terrace/VisualRoot` | 同上 | `2.2×0.75×6.4` |
| `K01_Cliffside_Steps` | `Architecture Visuals` 下新增 | 第一节台阶顶面中心；`(-1.66, 4.8, -8.75)` | `0.84×1.91×3.91` |
| `K01_Goal_Pavilion` | `Architecture Visuals` 下新增 | 安装面中心；`(-7.15, -0.12, 21.18)`，绕 Y 转 `180°` | `1.98×2.51×0.52` |
| `K01_Cliff_Foundations` | `Architecture Visuals` 下新增 | 场景原点；`(0,0,0)` | 总包围盒约 `10.0×3.41×33.06` |

蓝色收边仅位于道路立面的下缘，会让总包围盒比主体略宽。主道路盒的中心、高度和顶面仍与现有 Collider 相同。精确的 FBX 导入后尺寸以验证记录为准。

## Codex 接入步骤

1. 保留本目录中 FBX、材质、Prefab 的 `.meta`。不要将 FBX 根节点的缩放手工归一，避免重复换算；直接使用配套 Prefab。Prefab 外层是米制包装节点，内部已保留 FBX 的轴向旋转和单位换算。
2. 对道路：在原 `Course` 子对象下替换 `VisualRoot`。新节点命名回 `VisualRoot`，局部位置/旋转为零、缩放为 `(1,1,1)`。**不要替换其父对象、BoxCollider 或父对象旋转。**
3. 对塔楼、穹顶和基座：保留原占位对象作为映射锚点，关闭其 MeshRenderer；新增对应 Prefab 作为 `Architecture Visuals` 子对象，按上表放置。也可以删除纯 MeshRenderer 占位，但不要改变 `Architecture Visuals` 根。
4. 将台阶、终点拱亭和崖石添加到 `Architecture Visuals`。所有额外装饰保持无碰撞。
5. 保留 `Rail Mechanisms` 两块绿色史莱姆和蓝色偏转板、金色标记、`Goal`、`Completion Visuals` 以及现有事件接线。本任务没有提供 K-02/K-03 的机关替换模型。
6. 接入正式场景后，在默认相机下检查 16:9 和 4:3，再由 Codex 执行两次完整球路验收。K-01 的预览测试只验证独立副本，不替代正式场景验收。

## 净空与镜头

- 默认正交相机：位置 `(20,22,-6)`，看向 `(-1,1.5,3.5)`，Orthographic Size `10.6`。
- 塔楼位于起点路线负 X 一侧；崖边台阶也位于负 X 外侧，不占球路宽度。
- 第一次飞跃走廊按约 `X[-1.45,1.45]`、`Z[-1.4,6.0]` 留空；第二次按约 `X[-1.75,1.75]`、`Z[5.0,14.8]` 留空。没有横跨这些走廊的屋顶、拱梁或装饰。
- 终点拱亭位于终点后方，不覆盖金色终点环。崖石最高处均低于相应道路底面。
- `Preview/K01_DefaultCamera_16x9.png`、`K01_DefaultCamera_4x3.png` 和 `K01_ReverseFromGoal.png` 是 Unity 验收图；同名 `_Blender.png` 仅作源文件建模参考。
- 无音乐、音效或音频接线。本交付不处理 K-06 灯光统一和 K-07 音频。

## 材质与源文件

| Unity / Blender 材质 | 用途 |
|---|---|
| `K01_Lime_White` | 石灰白墙、道路、台阶 |
| `K01_Aegean_Blue` | 蓝穹顶、立面收边、终点拱亭檐口 |
| `K01_Recess_Blue` | 塔楼拱口内部阴影面 |
| `K01_Warm_Gold` | 穹顶小顶饰、拱亭顶饰；不代替轨道标记材质 |
| `K01_Cliff_Stone` | 低多边形崖石 |

无外部贴图依赖。Blender 中 `01_ARCHITECTURE`、`02_COURSE`、`03_CLIFF` 为交付几何；`90_PREVIEW_PROXIES` 只是镜头参考，不能接入正式场景。`Editor/` 下的生成和验证脚本仅在 Unity Editor 中使用。
