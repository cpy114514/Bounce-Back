# E-LiftGate · 升降墙门楼

纯视觉模块。没有碰撞体、刚体或脚本。碰撞和滑动仍由关卡里的 Lift Wall 负责。

墙在正确高度时，门楣和墙板各带半个圆饰，拼成三枚完整的杏橙色圆章。偏低或偏高时圆章从中间错开。墩柱上的浅槽是 0.25 m 一档的吸附刻度。墙板中央是三枚蓝色菱形，形状与终点金镶相同，不用金色。

## 文件

- 源文件：`ArtSource/Prototypes/E-LiftGate/E-LiftGate.blend`
- 可重跑脚本：`ArtSource/Prototypes/E-LiftGate/build_e_liftgate.py`
- Unity 导入与预览：`ArtSource/Prototypes/E-LiftGate/unity_import_preview.cs`
- 尺寸记录：`ArtSource/Prototypes/E-LiftGate/E-LiftGate_Manifest.json`
- Unity 实测：`Documentation/E-LiftGate_Unity.txt`
- 墙板：`FBX/LiftGate_Shutter.fbx`，`Prefabs/LiftGate Shutter.prefab`
- 门楼：`FBX/LiftGate_FrameLintel.fbx`、`FBX/LiftGate_FramePiers.fbx`，`Prefabs/LiftGate Frame.prefab`
- 预览：`Preview/E-LiftGate Preview.unity`

## 锚点与朝向

单位是米。Prefab 根节点放到锚点上时，旋转保持 `(0,0,0)`，缩放保持 `(1,1,1)`。墙的倾斜写在内部子节点上。FBX 导入节点自带约 `-90°` X 和 `100` 倍缩放，这是轴向换算，世界尺寸已经核对过，不要把它归零。

| Prefab | 根节点世界锚点 | 含义 |
| --- | --- | --- |
| LiftGate Frame | `(-1.55, -0.22, 3.95)` | 地基顶面，与墙中心同一条竖线 |
| LiftGate Shutter | `(-1.55, 2.15, 3.95)` | 墙板几何中心，与 Lift Wall 正确位置重合 |

**LiftGate Shutter**

- 子节点 `Orientation` 的本地旋转是 Lift Wall 的旋转，四元数 `(x, y, z, w) = (0.012413774, 0.9237969, -0.030059848, 0.38149914)`。
- 这个子节点的本地 **+Z 是正面**，世界方向约为 `(0.704, -0.065, -0.707)`，朝向镜头和来球方向。本地 +X 沿墙的右轴，本地 +Y 沿墙的上轴。
- 网格以几何中心为原点。釉面外壳正好是 `2.60 × 1.25 × 0.29` m。正面圆章比碰撞面探出 **2 mm**。

**LiftGate Frame**

- `Orientation` 在本地 `(0, 2.37, 0)`，旋转与墙板相同。门楣、拱和上半圆章用这套墙局部坐标，原点在墙中心。
- `PierYaw` 在同一个 `(0, 2.37, 0)`，四元数 `(0, 0.9242858, 0, 0.38170105)`。它只保留墙的水平朝向，本地 +Y 是世界向上，所以墩柱垂直落在地基上。本地 +Z 仍是朝向镜头的正面。

墙的实际滑动轴是世界向上，不是倾斜后的局部上轴。平移时只改墙板根节点的世界位置，不要转根节点：

```csharp
shutter.transform.position = new Vector3(-1.55f, 2.15f + offset, 3.95f);
shutter.transform.rotation = Quaternion.identity;
```

`offset = 0` 时圆章对齐。玩法里的行程是 `offset` 从 `-1.5` 到 `1.5`，吸附步长 `0.25`。

挂到已经带有墙旋转的 Lift Wall 下面时，用逆旋转抵消父节点，让根节点的世界旋转仍是 0：

```csharp
shutter.transform.SetParent(liftWall, false);
shutter.transform.localPosition = Vector3.zero;
shutter.transform.localRotation = Quaternion.Inverse(liftWall.rotation);
```

门楼不要挂到会动的 Lift Wall 上。庭院锚点在原点时，把 Frame 放在它下面，本地位置用上面的锚点，本地旋转为 0。

## 尺寸

| 部件 | 尺寸 |
| --- | --- |
| 墙板外壳 | 宽 2.60，高 1.25，厚 0.29；正面边框宽 0.10，中心内凹 0.037 |
| 圆章 | 半径 0.30，三个，局部 x = `-0.72, 0, 0.72`；接缝在局部 y = `0.625` |
| 终点徽章 | 三枚菱形，中间一枚对角线 0.30 × 0.164，两侧 0.25 × 0.136，MV_DomeBlue |
| 墩柱中心 | 沿墙右轴 ±1.56。世界位置约 `(-2.655, y, 2.849)` 和 `(-0.445, y, 5.051)` |
| 墩柱 | 可见柱身从地基 y = `-0.22` 到 y = `4.00`；上段截面约 0.35 × 0.44 |
| 穹顶 | 八边形穹顶加杏色尖顶，尖顶约到 y = `4.43` |
| 门楣梁 | 紧贴墙板顶边之上，正面比墙板后退约 4 cm |
| 拱 | 浅拱，外拱冠约在世界 y = `3.79` |
| 吸附刻线 | 世界 y = `0.65, 0.90, …, 3.65`，每 0.25 m 一道，刻在墩柱内侧并绕到正面 |

刻线对应墙板中心的吸附高度。圆章对齐的那一档是世界 y = `2.15`（墙板中心），不是刻线里单独涂色的一档。

## 三角面与材质

单个模块都少于 4 个材质槽。

| 模块 | 三角面 | 材质槽 |
| --- | ---: | --- |
| LiftGate Shutter | 288 | MV_Mint、MV_Apricot、MV_DomeBlue |
| LiftGate Frame | 1476 | MV_Ivory、MV_Apricot、MV_DomeBlue |
| 合计 | 1764 |  |

MV_Mint 只给墙板釉面，光滑度 0.72。穹顶用 MV_DomeBlue。圆章和线脚用 MV_Apricot。没有金色。

## 和碰撞体的误差

Unity 把墙板放在锚点、旋转为 0 后，全部顶点变到墙的局部坐标里测量：

- 釉面外壳不出 `±1.30 / ±0.625 / ±0.145`。
- 最大超出量 **0.002 m**，只来自正面杏色圆章。
- 布局 JSON 的 8 个角点与这套旋转重建的角点，误差小于 0.002 m。

墩柱在墙宽之外：上段内侧面在局部 x = ±1.385，墙板边缘在 ±1.30，缝约 0.085 m，滑动时横向不漂移。门楣在对齐高度之上。墙板升过门楣时会从门楣前方滑过，门楣正面在墙板正面后面约 4 cm，不会和釉面抢深度。

## 预览

`Preview/E-LiftGate Preview.unity` 里有正式正交镜头（size 9.5，从 `(12, 15, -15)` 看向 `(-0.05, 0.95, 0.25)`）、门楼近景，以及从终点 `(5, 1.5, 5.2)` 回看门楼的透视镜头。`Collision Reference` 是 Lift Wall 的盒子，渲染默认关闭，打开它可以对照碰撞体。

- `Preview/E-LiftGate_Official_Aligned.png`：正式镜头，圆章对齐
- `Preview/E-LiftGate_Official_Offset.png`：正式镜头，墙板下降 0.5 m
- `Preview/E-LiftGate_Pattern_Aligned.png`：近景，三枚圆章拼完整
- `Preview/E-LiftGate_Pattern_Offset.png`：近景，圆章上下错开
- `Preview/E-LiftGate_Goal_Lookback.png`：从终点回看

预览里的红色地基只属于这个场景，不在 Prefab 里。

## 重建

```powershell
& "D:\indie_game_dev\unity\AI Trolley Problem\BlenderPortable\blender.exe" --background --python-exit-code 1 --python "ArtSource/Prototypes/E-LiftGate/build_e_liftgate.py"
unity command script eval --project-path "<项目根目录>" --file "ArtSource/Prototypes/E-LiftGate/unity_import_preview.cs" --timeout 300
```

导入脚本使用已经打开的编辑器，不进入 Play Mode，也不保存 Santorini Gameplay。
