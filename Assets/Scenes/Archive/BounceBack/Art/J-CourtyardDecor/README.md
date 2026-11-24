# J-CourtyardDecor · 圣托里尼庭院装饰补充包

参考本地 `H-Props`、蓝色穹顶建筑与 `C-AqueductWalk` 制作的 **18 件新增装饰**。沿用象牙白、穹顶蓝、杏色陶土、玫瑰色与淡紫配色，采用清楚的低多边形块面，不使用外部贴图。

本包全部为纯静态模型：没有 Collider、Rigidbody、MonoBehaviour、Animator、Animation、布料模拟或运行时脚本。**未接入正式玩法场景，不改变碰撞和球路。**

## 18 件内容

| Prefab / FBX | 内容 |
| --- | --- |
| `J_Blue_Wooden_Bench` | 蓝色板条长椅、象牙白支撑 |
| `J_Courtyard_Chair` | 配套庭院单椅 |
| `J_Cafe_Table_Round` | 象牙圆桌与蓝色底座 |
| `J_Ceramic_Stool` | 蓝色陶凳与浅色环带 |
| `J_House_Number_07` | 蓝色门牌，几何块数字 `07`，不依赖字体 |
| `J_Courtyard_Wall_Fountain` | 拱背壁泉、小盆、出水口与静态水面 |
| `J_Blue_Railing_1m` | 约 1 m 蓝色栏杆 |
| `J_Rooftop_Pergola_2m` | 约 2 m 宽四柱板条花架 |
| `J_Canvas_Parasol` | 蓝白分片遮阳伞 |
| `J_Striped_Awning_2m` | 约 2 m 蓝白静态雨篷 |
| `J_Laundry_Line_2m` | 约 2 m 晾衣绳与三块有厚度的布片 |
| `J_Clay_Pitcher` | 带柄与几何壶嘴的陶壶 |
| `J_Fruit_Basket` | 低面数水果与杏色篮子 |
| `J_Blue_Wooden_Crate` | 蓝色板条木箱 |
| `J_Rope_Coil` | 杏色螺旋绳卷 |
| `J_Lifebuoy` | 象牙色救生圈与玫瑰色包带 |
| `J_Book_Stack` | 四本错位叠放的书 |
| `J_Candle_Set` | 托盘与三根不同高度的蜡烛 |

## 预算与使用

- 合计 **6,114 三角面**；最大单件 **746 三角面**。
- 每件不超过 **1,500 三角面、4 个材质槽**；UV0 完整。
- 单位为米，Unity `+Y` 向上、`+Z` 向前。
- 每件轴心为自身底部包围盒中心：局部 X/Z 居中，最低可见点 `Y=0`。
- 外层 Prefab 保持零旋转和单位缩放；保留内部 FBX 轴向/单位转换，不要重复修正。
- 墙面物件也使用底部居中轴心，安装高度和墙面距离由摆放父节点决定。
- 壁泉水面只是静态网格；晾衣绳与雨篷没有物理或布料模拟。
- 不使用机关薄荷绿或球/终点金色；`MV_Water` 仅用于壁泉。

## 文件

- `FBX/`：18 个独立模型。
- `Prefabs/`：18 个可直接摆放的独立 Prefab。
- `Materials/MV_*.mat`：沿用的 URP/Lit 配色材质。
- `Preview/J-CourtyardDecor_Preview.unity`：18 件真实尺寸总览。
- `Preview/J-CourtyardDecor_All.png`：Unity URP 实际渲染的 1920×1080 总览图。
- `Documentation/J-CourtyardDecor_Mesh_Report.json`：逐件尺寸、材质和面数。
- `Documentation/J-CourtyardDecor_Unity_Validation.txt`：Unity 导入与 Prefab 验证记录。
- `ArtSource/Prototypes/J-CourtyardDecor/J-CourtyardDecor.blend`：可编辑源模型。
- `ArtSource/Prototypes/J-CourtyardDecor/build_j_courtyard_decor.py`：可重复生成脚本。
- `ArtSource/Prototypes/J-CourtyardDecor/unity_j_courtyard_decor_preview.cs`：隔离 Unity 构建工具，不是道具组件。

预览中的底座、临时墙面、墙帽和晾衣绳支柱只属于陈列场景，不包含在任何道具 Prefab 中。

## 重建与验证

从项目根目录运行：

```powershell
blender --background --factory-startup --python-exit-code 1 `
  --python ArtSource/Prototypes/J-CourtyardDecor/build_j_courtyard_decor.py
```

Unity 验证只在 `.utmp/J-CourtyardDecor-QA` 隔离副本执行。将 Unity 工具复制为该副本的 `Assets/Editor/JCourtyardDecorPreview.cs`，同步本包 FBX 和网格报告后运行：

```powershell
Unity.exe -batchmode -projectPath .utmp/J-CourtyardDecor-QA `
  -executeMethod JCourtyardDecorEditor.BuildAndCapture -quit `
  -logFile .utmp/J-CourtyardDecor-QA/J-CourtyardDecor-QA.log
```

不要对正在使用的主项目执行该批处理入口。后续整合应按实际墙面、台阶与净空要求摆放，不要为了装饰给球路添加未验证的碰撞。
