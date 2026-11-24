# G-StylizedWater · 纪念碑谷式浅水、瀑布与水雾

纯视觉水效果。目标 **Unity 6000.3.23f1 / URP 17.3 / WebGL 2**。水通过 Shader 时间与少量顶点位移运动，不使用 Compute、深度纹理、Opaque Texture、屏幕折射、粒子模拟、烘焙动画或外部贴图。**没有修改全局 URP Asset、Renderer、正式关卡、玩法 Runtime 或其他任务目录。**

## 交付文件

- 可编辑源文件：`ArtSource/Prototypes/G-StylizedWater/G-StylizedWater.blend`
- 可重跑网格脚本：`ArtSource/Prototypes/G-StylizedWater/build_g_stylized_water.py`
- 静态网格：`FBX/G-StylizedWater.fbx`
- 水池 Prefab：`Prefabs/G-PoolWater.prefab`
- 瀑布与底部雾 Prefab：`Prefabs/G-Waterfall.prefab`
- Shader：`Shaders/GPoolWater.shader`、`GWaterfall.shader`、`GWaterMist.shader`
- 材质：`Materials/MV_Water.mat`、`MV_Waterfall.mat`、`MV_Mist.mat`
- 唯一可选运行时脚本：`Scripts/WaterRipple.cs`
- 可重建 Unity Prefab / 预览 / 校验的 Editor 工具：`Editor/GStylizedWaterBuild.cs`
- 独立预览：`Preview/G-StylizedWater Preview.unity`
- 尺寸、拓扑、对齐与性能记录：`Documentation/`

## 锚点、尺寸与网格预算

单位均为米，Unity **+Y 向上**。交付根节点零旋转、单位缩放；轴向换算保留在 FBX 内部，不要再手动归一内部旋转或缩放。

| 模块 | 安装锚点（世界坐标） | 尺寸 / 枢轴 | 三角面 | 材质槽 |
|---|---|---|---:|---:|
| `G-PoolWater` | `Courtyard and Water Art`：`(0,0,0)` | 水面中心 `(-1.40,0.45,2.05)`；`3.25×3.65 m`；网格平面精确位于 `Y=0.45` | 2304 | 1 |
| `G-Waterfall / G_WaterfallRibbon` | `Waterfall Slot`：`(5.72,0.43,5.24)` | 根部即出口中心；局部 `+X` 朝崖外；宽 `1.60 m`，落差 `10 m` | 640 | 1 |
| `G-Waterfall / G_WaterfallMist` | 与瀑布共用出口锚点 | 三片无贴图柔边面片雾，位于落差底端 | 6 | 1 |
| **合计** |  |  | **2950** | 每个 Mesh 1 |

任务 D 的 README 在制作时尚未出现在工作区。因此水面尺寸以布局 JSON 的 `Elastic Pool Surface` 与 `SantoriniSystemsAuthoring.cs` 为准：**顶面 Y=0.45，中心 X/Z=(-1.40,2.05)，XZ=3.25×3.65**。这里只有一张水面，不制作任务 D 的石盆、池沿、岛屿或其他建筑。

### 对齐与净空

- 池水平面与 `Elastic Pool Surface` 的顶面重合，静态几何误差为 **0 m**（浮点误差除外）。
- 静止几何波纹只向下，最大 **0.006 m**；满蓄力冲击顶点位移的保守上限为 **0.018 m**，均不超过 `0.02 m`。
- 水面上方没有几何体。泡沫、亮纹和冲击环都在 Shader 内生成，不另放装饰板或粒子。
- 瀑布最高附着边精确经过出口，局部 `X=0,Y=0,Z=0`；水流随后向 `+X` 翻落，全部 `Y≤0`。它不占压力板的顶面，也不改变球路碰撞。
- 雾仅位于 `Y≈-9.38…-10.38` 的下落末端。三片雾代替 ParticleSystem，不需要排序脚本或逐帧生成对象。
- 所有交付网格无 Collider、Rigidbody、LOD 额外逻辑或玩法脚本。仅池 Prefab 的可选 `WaterRipple` 用于传参。

## 水面效果与蓄力接口

`MV_Water` 使用浅青色 `_BaseColor=(0.45,0.80,0.84,1)`，Unity Linear 项目按 sRGB 颜色属性处理。白色边缘泡沫带宽约 `0.085 m`。低频顶点位移、少量干扰亮纹与柔和高光保持大块面的视觉风格。

| Shader 属性 | 含义 |
|---|---|
| `_Charge` | `0–1`，无单位时间；0 平静，升高时从冲击点向外扩散同心环，1 时主环约到达 3.25 m 半径 |
| `_ImpactPos` | **世界坐标**，例如 `(-1.40,0.45,2.05)`；不要传局部坐标 |
| `_PoolSize` | 物理宽长 `(3.25,3.65)`，泡沫和程序化纹理以米计算 |
| `_WaveHeight` | 静止波纹最大向下位移，默认 `0.006 m`；不要超过对齐预算 |
| `_FoamWidth` | 白边宽度，默认 `0.085 m` |
| `_RingWidth` | 主冲击环宽度，默认 `0.085 m` |

### 任务 I 接入

```csharp
using BounceBack.StylizedWater;

// poolRipple 是 G-PoolWater 根上的 WaterRipple。
poolRipple.SetCharge(normalizedCharge, ballWorldPosition);
// 弹力释放或重置时：
poolRipple.Clear();
```

`SetCharge` 使用 `MaterialPropertyBlock`，不会创建材质实例，也不会污染共享材质。它只设置参数；蓄力时间、球碰撞检测、释放和重置由现有玩法系统负责。也可以由任务 I 直接对 Renderer 设置同名参数而不使用此脚本。

**接入时隐藏旧的 `Breathing Pool Surface` 与 `Water Inlay` 网格，避免与水面 Z-fighting。保留 `Elastic Pool Surface` 的 Collider、ElasticGeometry 和事件接线。** 如果 ElasticGeometry 正在驱动视觉 Transform，应把此 Prefab 作为其视觉子节点，池根仍保持局部 `(0,0,0)`；不要让同一顶面重复渲染。

## 瀑布与水雾

- `MV_Waterfall` 用 UV 向下流动的宽水纹与少量断续白色水带。顶缘固定，下方有亚厘米级几何摆动。
- 水带底部 `78%–100%` 渐隐，配合三片缓慢漂移的软雾融入云海。
- 面片透明、不投阴影、ZWrite Off。无需透明排序脚本；只有瀑布和雾两次透明绘制。
- 最终崖面凹槽由任务 A 负责。预览内的紫色竖直崖面只是占位，**不属于交付 Prefab**。
- `MV_Waterfall` 与 `MV_Mist` 是同族水色的功能材质变体；不是额外建筑配色。白泡沫在 Shader 内生成，不占额外部件材质槽。

## 预览与截图

`Preview/G-StylizedWater Preview.unity` 包含：

- 锚点就位的池水、冲击环、10 m 瀑布及底部雾。
- 从布局 JSON 生成的 19 个可见白模盒；第 20 个 `Elastic Pool Surface` 由水面表示。白模没有碰撞体。
- 白模塔身、石盆与底座等上下文，以及待任务 A 替换的 `+X` 竖直崖面占位。这些都不是任务 G 的交付模块。
- 正式镜头：正交 size **9.5**，位置 `(-0.05,0.95,0.25)+(12,15,-15)`，看向 `(-0.05,0.95,0.25)`。
- 回看镜头：从 `(5,1.5,5.2)` 朝塔方向。
- 一张额外瀑布细节镜头，便于检查 10 m 落程与底部雾。

- 正式相机：`Preview/G-StylizedWater_Formal.png`，**1920×1080**。
- 从终点回看：`Preview/G-StylizedWater_ReverseFromGoal.png`，**1920×1080**。该低机位按指定位置保留立面参考。
- 瀑布完整落程：`Preview/G-StylizedWater_WaterfallDetail.png`，**1920×1080**。
- 动画：`Preview/G-StylizedWater_5s.gif`，**960×540、20 fps、5 秒**；完整无损帧序列在 `Preview/Motion Frames/`。

预览场景**不加入战役或 Build Settings**，不替代正式场景的实玩测试。

## 重跑

已在项目提供的 **Blender 4.5.16 LTS** 实际运行。脚本也采用 Blender 5.x 可用的基础 API，但 5.3 的 WindowsApps 可执行权限不作为本交付的验证依据。

```powershell
blender --background --factory-startup --python-exit-code 1 --python "ArtSource/Prototypes/G-StylizedWater/build_g_stylized_water.py"
```

在 `.utmp/G-StylizedWater-QA` 这类独立副本中运行 Unity（不要在占用同一项目的编辑器里重复抢锁）：

```powershell
Unity.exe -batchmode -projectPath "<QA项目>" -executeMethod BounceBack.StylizedWater.Editor.GStylizedWaterBuild.Build -quit
Unity.exe -batchmode -projectPath "<QA项目>" -executeMethod BounceBack.StylizedWater.Editor.GStylizedWaterBuild.CaptureMotion -quit
Unity.exe -batchmode -projectPath "<QA项目>" -executeMethod BounceBack.StylizedWater.Editor.GStylizedWaterBuild.Measure -quit
# 真实 Editor 帧版本在采样结束后自行退出；不要附加 -quit。
Unity.exe -batchmode -projectPath "<QA项目>" -executeMethod BounceBack.StylizedWater.Editor.GStylizedWaterBuild.MeasurePresented -enable-frame-timing-stats
# 可选：Python 3 + Pillow 将100帧编码为GIF。
python "ArtSource/Prototypes/G-StylizedWater/encode_motion_gif.py"
```

`Motion Frames` 是 **100 张 960×540、20 fps、共 5 秒**的实际 Unity 渲染帧。捕获工具用 `_GPreviewTime` 固定时间复现动画；正常播放时该全局值为 0，Shader 自动使用 Unity `_Time`。不要让其他任务把 `_GPreviewTime` 设成非零。

## WebGL 与性能边界

- 每个 Shader 只有单个 Forward Pass，`#pragma target 2.0`，常规 HLSL 数学函数。
- 无屏幕纹理、深度图、RenderTexture 运行时分配、Compute、几何/曲面细分 Shader、Shader Graph 依赖或第三方插件。
- 水效果合计 3 个 MeshRenderer / 3 次绘制、2950 个三角面；没有阴影投射、粒子 CPU 模拟或大贴图带宽。
- A/B 离屏记录见 `Documentation/G-StylizedWater_Performance.json`：各有水/无水 60 帧，1920×1080 渲染目标，额外 CPU 提交中位数 **0.1114 ms**。
- 补充的真实 Editor 帧采样见 `Documentation/G-StylizedWater_PresentedProfiler.json`：每臂 60 帧，额外 CPU 提交中位数 **0.120000 ms**。
- **CPU 估算低于 1 ms，但尚未证明 WebGL GPU 开销低于 1 ms。** 本机 batchmode 不提供可信 GPU 时间，`FrameTimingManager` 返回 0 个有效 GPU 样本；计数器快照也返回 0，不能拿这些 0 当作绘制统计。绘制的 3 次调用与 2950 三角面来自已验证的网格和材质槽结构。
- 当前性能数是 Unity Editor / Intel Arc 140T / D3D12 的估算，**不能冒充 WebGL 浏览器 GPU 实测**。浏览器 `<1 ms` 仍需任务 I 整合后按相同分辨率复核。
- 本任务只做视觉与隔离预览，没有修改玩法或执行正式关卡 PlayMode 回归。
