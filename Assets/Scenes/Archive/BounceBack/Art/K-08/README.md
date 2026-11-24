# K-08 · 爱琴海白石拱窗

## 单件范围

原创、低多边形、纯视觉的单体拱窗。一个根节点下只有 **白石边框** 与 **钴蓝内衬** 两块独立网格；没有玻璃、窗扇、窗棂、五金、额外道具、Collider、Rigidbody 或外部贴图。预览相机和灯光不参与 FBX 导出，也不包含额外地台或背景网格。

- 总尺寸：**宽 1.20 m × 高 1.80 m × 深 0.24 m**。
- 枢轴：**整体底面包围盒中心，局部 `(0,0,0)`**。
- Unity 轴向：**+Y 向上，+Z 向前；可见正面为 +Z**。
- 轮廓：平底、直立窗梗、24 段半圆拱；无凸出的线脚或窗台。
- 白石细倒角：**8 mm、2 段**；钴蓝内衬细倒角：**3 mm、2 段**。

## 分件与净空

| 项目 | 尺寸 |
| --- | --- |
| 白石侧框与拱顶厚度 | 0.16 m |
| 白石底框高度 | 0.14 m |
| 钴蓝内衬厚度 | 0.032 m |
| 钴蓝内衬深度 | 0.20 m，前后各露出 0.02 m 白石边缘 |
| 贯穿净拱洞宽度 | 0.816 m |
| 贯穿净拱洞高度 | 1.436 m |
| 净拱洞底高 / 拱起点高 / 拱顶高 | 0.172 / 1.20 / 1.608 m |

两块网格均为封闭、法线朝外的实体环体，拱洞是真实的前后贯通空间，不是贴图、封口面或深色背板。Blender 求值后的每块网格为 712 个三角形，单件合计 **1424 个三角形**。

## 文件

- Blender 源文件：`ArtSource/Kimi/K-08/K08_Aegean_Arch_Window.blend`
- Blender 可重建脚本：`ArtSource/Kimi/K-08/build_k08.py`
- Unity 导入与验证脚本：`ArtSource/Kimi/K-08/unity_import_validate.cs`
- Unity FBX：`FBX/K08_Aegean_Arch_Window.fbx`
- Unity 单件 Prefab：`Prefabs/K08_Aegean_Arch_Window.prefab`
- URP 材质：`Materials/K08_White_Limestone.mat`、`Materials/K08_Cobalt_Blue.mat`
- 精确尺寸与 Blender 拓扑/射线检查：`Documentation/K08_Dimensions.json`
- Unity 实际导入检查：`Documentation/K08_Unity_Validation.txt`
- 造型预览：`Preview/K08_Aegean_Arch_Window_Preview.png`
- 正面预览：`Preview/K08_Aegean_Arch_Window_Front.png`

## 接入

直接使用配套 Prefab，外层包装节点保持位置 `(0,0,0)`、旋转 `(0,0,0)`、缩放 `(1,1,1)`。内部保留了 FBX 的轴向和单位换算，**不要再次归一化内部子节点的旋转或缩放**。需要碰撞时由 Codex 按实际玩法另行配置，不要给净拱洞加整面盒碰撞。

本任务没有修改或保存现有 Unity 场景，也没有接入玩法、音频、输入或关卡切换。

## 重建与验证

```powershell
blender --background --factory-startup --python-exit-code 1 --python "ArtSource/Kimi/K-08/build_k08.py"
unity command script eval --project-path "<项目根目录>" --file "ArtSource/Kimi/K-08/unity_import_validate.cs" --timeout 120
```

Blender 检查包括两块网格的封闭性、零面积面、正体积、尺寸和贯通/实体射线样本。Unity 检查包括实际 FBX 包围盒、底部中心枢轴、两个材质槽、双面三角形射线净空、无物理组件及 Prefab 生成。详细结果见上述验证文件。
