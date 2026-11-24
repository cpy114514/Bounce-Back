# Bounce Back 资产目录

共享资产放在 `Assets/_Project`。主菜单资产集中在 [MainMenu](Assets/_Project/MainMenu)，可编辑的 Blender 源文件在 [ArtSource/MainMenu](ArtSource/MainMenu)，早期美术原型集中在 [ArtSource/Prototypes](ArtSource/Prototypes)。新类别有第一份资产时再建子目录。

## 手绘 2D 和 UI

| 内容 | 存放位置 |
| --- | --- |
| 按钮、面板、图标、装饰 | [2D/UI](Assets/_Project/2D/UI) 下的 `Buttons`、`Panels`、`Icons`、`Decorations` |
| 插画、背景 | [Illustrations](Assets/_Project/2D/Illustrations)、[Backgrounds](Assets/_Project/2D/Backgrounds) |
| PSD、Krita 文件、原始 SVG 等可编辑档 | [2D/SourceFiles](Assets/_Project/2D/SourceFiles)。导出的 PNG 或游戏用 SVG 放到对应的成品目录 |
| Unity 几何绘图 | [GeometryDrawings](Assets/_Project/2D/SourceFiles/GeometryDrawings)。用现有 **Geometry Level Editor** 打开 `.asset` |

- 透明 PNG 导入 Unity 后，选 **Sprite (2D and UI)**。单图用 **Single**，透明边缘需要过滤时启用 **Alpha Is Transparency**。屏幕空间 UI 通常关闭 mipmaps，细线和文字边缘优先检查 **Compression: None** 的实际效果。
- 当前项目有 SVG Importer。导入时确认输出类型与使用它的 UI 组件一致，也可导出透明 PNG。面板需要拉伸时，在 Sprite Editor 设九宫格边距。
- 保留手绘源文件；游戏和 Prefab 引用成品导出。更新图片时，可覆盖同一成品文件并保留对应的 `.meta`，这样已有引用会继续跟随素材。

## 其他资产

| 内容 | 存放位置 |
| --- | --- |
| 新的共享 3D 模型、材质、贴图、动画 | 在 `Assets/_Project/3D` 下按需要创建 `Models`、`Materials`、`Textures`、`Animations`。Blender 可编辑源文件继续放 [ArtSource](ArtSource)。现有关卡美术包在 [BounceBack/Art](Assets/Scenes/Archive/BounceBack/Art) |
| 可复用 Prefab | 现有米制积木和机关在 [GeometryWorldLibrary/Prefabs](Assets/GeometryWorldLibrary/Prefabs)。新增独立系统的共享 Prefab 可在 `Assets/_Project/Prefabs` 按 `Ball`、`Mechanisms`、`Buildings`、`UI` 分类。只有渲染用途的模型 Prefab 可与共享模型同组 |
| 音乐、音效、环境声 | 新资产放 `Assets/_Project/Audio/Music`、`SFX` 或 `Ambient`。已有四段运行时音频归在 [Audio/Resources/BounceBack/Audio](Assets/_Project/Audio/Resources/BounceBack/Audio)，按原有键加载 |
| 着色器、特效、配置 | [Shaders](Assets/_Project/Shaders)、[VFX](Assets/_Project/VFX)、[Data](Assets/_Project/Data)。搭建目录在 `Data/Resources/BounceBack/Build Catalog.asset` |
| 脚本和测试 | 现有关卡扩展使用 [GeometryWorld/Runtime](Assets/GeometryWorld/Runtime)、[GeometryWorld/Editor](Assets/GeometryWorld/Editor) 和 [GeometryWorld/Tests](Assets/GeometryWorld/Tests) 的程序集。独立新系统可在 `Assets/_Project/Scripts` 里按职责建目录，并配置自己的程序集 |
| 场景 | 正式入口为 `Assets/Scenes/Main Menu.unity`，预览为 `Assets/Scenes/Arrival Preview.unity`。旧菜单和关卡集中在 [Archive](Assets/Scenes/Archive)，默认关闭其构建条目。正式场景变更后检查 Build Profiles 和 `CampaignHub.destinations` |
| 第三方资产 | UPM 依赖由 [Packages](Packages) 管理。项目内直接导入的第三方资产放 [ThirdParty](Assets/ThirdParty)，并保留许可证。Source Serif 4 放在这个目录下 |

## 迁移约定

1. 用 Unity 的 Project 窗口或 `AssetDatabase.MoveAsset` 移动已导入资产。提交时检查资产和 `.meta` 一起移动，并与正在使用该目录的协作者确认。
2. 检查硬编码路径、场景清单、场景启动路径、导出脚本以及 Prefab 引用。放在 `Resources` 下的文件，运行时键从 `Resources/` 后面开始，移动时保持这段路径。
3. 迁移整包时，同时更新代码和 Blender 导出脚本中的路径，并在隔离工程验证。2D 编辑器和演示场景继续使用原目录。
