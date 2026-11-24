# Bounce Back Valley 主菜单

在 Unity 打开 `Assets/Scenes/Main Menu.unity` 后点 Play。鼠标悬停或 Tab 浏览建筑，点击或 Enter 选择。开场可按空格或点击右下角跳过。岛屿在总览中受到鼠标悬停时轻轻漂浮，光标移开后会回到原位。Video 和 Audio 的操作台从建筑底座沿导轨伸出。声音滑轨支持点击、拖动和方向键，Home、End 可直接到 0%、100%。首次开始旅程会进入 Arrival Preview。

## 文件位置

| 内容 | 位置 |
| --- | --- |
| 五座岛的 Blender 源文件 | 本目录的五个岛屿子目录 |
| 燕子 Blender 源文件 | `Environment/Swallow.blend` |
| 低多边形云 Blender 文件 | `Environment/Menu Clouds.blend`，作者和许可见 `Assets/StreamingAssets/Third-Party Notices.txt` |
| FBX、运行时网格、Prefab、材质、画质预设及 UI | `Assets/_Project/MainMenu` |
| 运动、镜头、设置与存档集成 | `Assets/GeometryWorld/Runtime/MainMenu` |
| 生成场景和 Prefab 的编辑器工具 | `Assets/GeometryWorld/Editor/MainMenu` |
| 归档关卡和旧菜单 | `Assets/Scenes/Archive` |

## 继续建模

在 Blender 保持 1 米为 1 单位。每个能动的部件以同名空物体为枢轴，模型放在组下。原有空物体名字对应 Unity 中的复原和散落姿态。石材用 `BB_RRGGBB` 标记 sRGB 颜色，构建器据此共用 Unity 材质。FBX 导出用 `-Z Forward`、`Y Up`、单位比例 1。当前 Blender 源文件的 X 轴已经为 Unity FBX 导入方向做了补偿，请用同一坐标系修改和导出。

Audio 岛的 Habitat 67 住宅用独立的模块、连廊和庭院做重组。保持 Blender 中各组的名称和枢轴，选中 **Habitat 67** 集合内的模型和空物体导出。Unity 用同一组模型驱动悬停复原和两侧住宅的滑动。

覆盖相应 FBX 并保留其 `.meta`。在 Unity 执行 **Tools → Bounce Back → Main Menu → Rebuild Generated Menu**，再检查 Prefab 和废墟姿态。工具会重建生成的场景、Prefab 和菜单道具。请先记录场景里手动指定的音乐，以便重建后重新连接。

## 替换 UI 和声音

在 **Menu Artwork** 资源中编辑悬停标记、字体和菜单文案。把 PNG 导入为 **Sprite (2D and UI)**，使用 Full Rect。音乐接到 **Audio routing / Music** 的 AudioSource，按第一次输入播放。确认反馈使用项目已有的 District Reveal Chime。

标题使用标题岛 Blender 文件中的可编辑 Marcellus 立体字，菜单标签使用 Source Sans 3。字体和许可证位于 `Assets/_Project/MainMenu/UI/Fonts`。

燕子的左右翅膀独立摆动，飞行路径由 Unity 主菜单控制。启用减少动态效果时，燕子保持滑翔姿态。

Habitat 67 的错层住宅、架空连廊和屋顶露台参考 [Safdie Architects 的项目档案](https://www.safdiearchitects.com/projects/habitat-67/)。
