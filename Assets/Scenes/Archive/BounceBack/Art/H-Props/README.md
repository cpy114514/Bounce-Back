# H-Props · 圣托里尼居住感小道具

13 件原创、低面数、纯静态道具。没有 Collider、Rigidbody、MonoBehaviour、Animator、动画、布料模拟或外部贴图。**没有接入 `Santorini Gameplay`，本交付不改变玩法或碰撞。**

## 使用约定

- 单位为米，Unity **+Y 向上、+Z 向前**。
- 每个 Prefab 的轴心均为**自身最低可见包围面在 X/Z 方向的包围盒中心**，即局部 `(0,0,0)`；最低可见点 `Y=0`。不是墙面安装点或吊点。
- Prefab 外层保持零旋转与单位缩放；内部 `FBX_Axis_And_Unit_Conversion` 节点保留 FBX 的轴向与单位换算。请直接使用配套 Prefab，不要手工再次归一化内部旋转或缩放。
- 百叶窗正面朝 **−Z**，安装背面约为局部 `Z=+0.028 m`。旗绳端点跨距 2 m；整件包围宽度包括绳半径，为 2.00554 m。海鸥喙尖朝 **−Z**，三种翼形均为静态网格。
- `MV_Apricot` 用于陶盆、铜钟、灯笼框架与铜色细节；未另造金属色。未使用专属于可动部件的 `MV_Mint` 或专属于球和终点的 `MV_Gold`。
- 13 件道具合计 **3932 三角面**（Unity 实测，与 Blender 一致）；单件上限 **614**，材质槽不超过 4。Unity 导入后的逐项检查见 `Documentation/H-Props_Unity_Validation.txt`。

## 尺寸与预算

下列尺寸为 Unity 局部空间的宽 × 高 × 深（X × Y × Z，米）。轴心均为底部包围盒中心，**世界锚点由整合任务决定，本任务不预设关卡摆放位置**。

| Prefab（FBX 同名） | 内容 | 尺寸（m） | 三角面 |
| --- | --- | --- | ---: |
| `H_Bougainvillea_Square` | 方盆三角梅，玫瑰色簇状花团 | 0.729 × 1.107 × 0.496 | 466 |
| `H_Bougainvillea_Round` | 圆盆三角梅，另一种盆形 | 0.729 × 1.107 × 0.521 | 614 |
| `H_Blue_Shutters_Pair` | 成对蓝色百叶窗，真实独立叶片 | 0.5215 × 0.84 × 0.0555 | 416 |
| `H_Bunting_2m` | 2 m 弯曲挂旗绳与四片三角旗 | 2.00554 × 0.47899 × 0.11279 | 288 |
| `H_Hanging_Lantern` | 六边形灯罩、框架与吊环 | 0.30 × 0.769 × 0.25981 | 304 |
| `H_Copper_Bell` | 铜色小钟、吊环与钟锤 | 0.38 × 0.40 × 0.38 | 260 |
| `H_Domed_Chimney` | 圆顶小烟囱 | Ø0.43 × 0.58 | 228 |
| `H_Roof_Cupola` | 屋顶蓝色小穹顶 | Ø0.50 × 0.415 | 276 |
| `H_Seagull_Glide` | 平展翼海鸥 | 0.88 × 0.13497 × 0.50565 | 96 |
| `H_Seagull_Upstroke` | 上扬翼海鸥 | 1.04 × 0.23497 × 0.50565 | 96 |
| `H_Seagull_Bank` | 下压／倾斜翼海鸥 | 0.94 × 0.13497 × 0.50565 | 96 |
| `H_Terracotta_Amphora` | 高陶罐与双耳 | 0.49904 × 0.48 × 0.424 | 440 |
| `H_Terracotta_Bowl` | 矮圆陶罐 | 0.508 × 0.385 × 0.508 | 352 |

花盆、陶罐的深色内表面是实际网格，不依赖透明贴图。旗绳与旗帜已合并为一个静态道具；如要改变弯曲形态，在 Blender 源文件或生成脚本中调整，不包含运行时绳索模拟。海鸥后续的移动由整合任务在自己的父节点上完成。

## 文件与重建

- Blender 源文件：`ArtSource/Prototypes/H-Props/H-Props.blend`
- 可重复生成脚本：`ArtSource/Prototypes/H-Props/build_hprops.py`
- Unity FBX：`FBX/`；独立 Prefab：`Prefabs/`
- URP/Lit 材质：`Materials/MV_*.mat`；不使用贴图或透明效果。
- 网格统计：`Documentation/H-Props_Mesh_Report.json`
- Unity 验证记录：`Documentation/H-Props_Unity_Validation.txt`
- 总览预览场景：`Preview/H-Props_Preview.unity`，仅供道具陈列验收，不加入战役或 Build Settings。
- 1920 × 1080 Unity 截图：`Preview/H-Props_All.png`，正交镜头，方向 `(12,15,-15)`。
- Unity 隔离构建工具源：`ArtSource/Prototypes/H-Props/unity_hprops_preview.cs`。它不是道具组件，未放进正式项目的 `Assets`。

从项目根目录重建 FBX、材质槽和 Blender 文件：

```powershell
blender --background --factory-startup --python-exit-code 1 --python "ArtSource/Prototypes/H-Props/build_hprops.py"
```

如果 `blender` 不在 PATH，请使用 Blender 可执行文件的完整路径。本机实际使用项目已有的便携版 Blender 4.5.16 LTS。生成器会覆盖本任务的 FBX 与 `.blend`；Unity 材质、Prefab 和预览由隔离验证工具生成。

## 预览与碰撞边界

总览图中的底座、百叶窗临时安装面、挂绳支柱和吊灯／铜钟支架均属于 `90_PreviewSupports_NotDeliveredAsProps`，不是额外交付的道具 Prefab。13 件道具以真实尺寸集中展示；海鸥采用静态悬空展示，没有飞行动画。

这是独立小道具包，不是碰撞体替换模块：**与布局碰撞表面的对齐误差不适用**，未进行球路或 Play Mode 验证。整合任务需要按实际墙面、台阶与吊点位置摆放，并遵守关卡净空要求。
