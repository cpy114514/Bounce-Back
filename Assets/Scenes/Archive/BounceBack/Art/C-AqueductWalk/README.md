# C-AqueductWalk｜高架弯道与拱廊

把塔顶到水池的路线包装成连续拱廊的**纯静态视觉模块**。碰撞、球、机关和正式玩法场景均未接入或修改；最终整合属于任务 I。

## 使用

- Prefab：`Prefabs/C-AqueductWalk.prefab`
- 世界位置：`(-3.20, 4.27, -2.10)`
- 世界旋转：`(0, 0, 0)`；缩放：`(1, 1, 1)`
- 枢轴：90° 弯道圆心，位于弯道行走顶面高度。
- 单位：米；Unity `+Y` 向上、`+Z` 朝水池。

FBX 的轴向修正已经保存在模型本地几何中，外层 Prefab 不需要额外旋转。保留 FBX 子节点自己的单位/轴向转换，不要归零其内部旋转或缩放。

整合时应保留现有 Course Physics 碰撞体，隐藏与其重合的旧坡道/护栏白模 Renderer，避免双重渲染和 z-fighting。本模块自身没有 Collider、Rigidbody、Animator、Animation 或 MonoBehaviour。

## 网格与预算

| 部件 | 三角面 | 内容 |
| --- | ---: | --- |
| `C-AqueductWalk_Deck` | 342 | 六段碰撞坡道的连续外表面，0.09 m 杏色檐线 |
| `C-AqueductWalk_Arcade` | 1,380 | 六个连续拱跨、内外柱、柱脚和横梁 |
| `C-AqueductWalk_Donkey_Stairs` | 816 | 内弯外侧装饰台阶带 |
| `C-AqueductWalk_Parapet` | 1,020 | 八段碰撞护栏、压顶和六个小圆顶柱 |
| **合计** | **3,558 / 15,000** | 四个静态网格部件 |

每个部件不超过 4 个材质槽，整个模块只使用以下 4 个核心材质，无贴图依赖：

| 材质 | sRGB 色值 |
| --- | --- |
| `MV_Ivory` | `(0.95, 0.88, 0.77)` |
| `MV_IvoryShade` | `(0.86, 0.76, 0.70)` |
| `MV_Lilac` | `(0.50, 0.45, 0.66)` |
| `MV_Apricot` | `(0.90, 0.58, 0.37)` |

材质通道按项目已有 MV 资产的序列化约定写入。`Preview/Materials` 中的水、金和薄荷绿材质只供预览白模使用，不属于模块。

## 对齐与净空

- 碰撞真值：`ArtSource/Santorini Layout/Santorini Collision Layout.json`，直接使用 JSON `corners`。
- 坡道由直坡、四段弯道和落池坡的碰撞棱柱求连续外形；护栏使用全部八段原始碰撞角点。
- 柱脚最低面：`y = -0.22`。
- 装饰台阶带宽 `0.50 m`，设计间隙 `0.05 m`，踏面比相邻路面低 `0.12 m`，不进入碰撞宽度。
- 最后一根支架位于 `z = -0.25`，最后一级装饰台阶位于 `z = -0.28`。
- 模块最大世界 Z 为落池碰撞唇口 `z = 0.019205`；`z > 0.025` 没有本模块几何，不阻挡球飞向 `(-1.40, 0.45, 2.05)`。
- 路面侧向 `0.60 m`、上方 `1.30 m` 的附加净空检查排除路面自身和任务要求保留的碰撞贴合护栏/立柱；其余几何不得进入该空间。

Unity 实测：坡道顶面角点最大误差 `0.00518656 m`，外露坡道顶点到原始碰撞面的最大误差 `0.00000027 m`，护栏角点最大误差 `0.00000055 m`；装饰台阶最小水平净距 `0.043274 m`。拱廊/台阶的顶点、边中点、质心共检查 `12,140` 个样本，附加净空侵入为 `0`。

完整记录见 `Documentation/C-AqueductWalk_Unity_Validation.txt` 和 `Documentation/C-AqueductWalk_Geometry.json`。

## 预览

打开 `Preview/C-AqueductWalk Preview.unity`。模块实例已经放在指定锚点。

- `Formal Camera`：正交 size `9.5`，焦点 `(-0.05,0.95,0.25)`，方向 `(12,15,-15)`。
- `Reverse From Goal Camera`：位置 `(5,1.5,5.2)`，正交 size `3.8`，用于检查拱券层次。
- 两张 PNG 均为 Unity URP 实际渲染的 `1920×1080`。回看 PNG 临时隐藏无关池体/路线白模并使用无投影检查光照，保留模块、塔、球与地基；场景默认恢复完整语境和软阴影。
- `Reference Blockout` 保存了从布局 JSON 生成的白色碰撞盒。与模块重合的 14 个核心盒默认关闭 Renderer，检查对齐时可逐个开启；其余白模只提供空间语境。
- 预览中的塔身、地基、球和水池白模不是模块内容，也不是正式场景替换件。

## 可重建源文件

- Blender：`ArtSource/Prototypes/C-AqueductWalk/C-AqueductWalk.blend`
- 生成脚本：`ArtSource/Prototypes/C-AqueductWalk/build_c_aqueduct_walk.py`
- 隔离 Unity 构建/验证脚本：`ArtSource/Prototypes/C-AqueductWalk/qa_build.cs`

```powershell
blender --background --factory-startup --python-exit-code 1 `
  --python ArtSource/Prototypes/C-AqueductWalk/build_c_aqueduct_walk.py
```

Unity 验证必须在 `.utmp/C-AqueductWalk-QA` 的隔离副本运行。将 `qa_build.cs` 复制为该副本的 `Assets/Editor/CAqueductWalkQABuild.cs`，同步 FBX 后执行：

```powershell
Unity.exe -batchmode -projectPath .utmp/C-AqueductWalk-QA `
  -executeMethod CAqueductWalkQABuild.Build `
  -logFile .utmp/C-AqueductWalk-QA/C-AqueductWalk-QA.log
```

不要对正在使用的主项目执行该批处理入口。QA 会生成材质、包装 Prefab、独立预览场景、双镜头 PNG 和验证记录；正式 `Santorini Gameplay.unity` 与生成器不在验证流程中。
