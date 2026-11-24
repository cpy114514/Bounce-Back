# Bounce Back 全局系统

正式入口是 **Campaign Boot**。从 [Campaign Boot.unity](Assets/Scenes/Archive/BounceBackCampaign/Campaign%20Boot.unity) 进入 Play Mode 即可测试实际开场。首次启动自动加载已有教程，完成后沿用教程自己的完成动画，再进入四入口选关。已有进度时，主菜单的 **Continue** 会从存档中的入口恢复。**Start** 可重玩教程，进度会保留。

| 部分 | 入口与责任 |
| --- | --- |
| 开场与菜单 | `GeometricMenu` 播放花形片头、拾取 3D 菜单造型和过渡到设置或世界。用鼠标或 `Tab`、`Enter` 操作 |
| 场景与进度 | `CampaignScenes` 管理异步加载和排他请求。 `CampaignProgress` 保存通关、解锁派生状态、最近停稳的入口、导航提示和已显形的建筑 |
| 选关城市 | [Campaign City.unity](Assets/Scenes/Archive/BounceBackCampaign/Campaign%20City.unity) 的 `CampaignHub` 驱动流程，`CityRail` 和 `WorldRoute` 管理单线道路和相邻入口 |
| 相机 | `PuzzleCameraRig` 提供统一的等距正交位姿、阻尼和缩放。选关沿路线预判，建筑打开和关闭用可编辑的镜头锚点，试玩阶段可跟随球 |
| 建筑与特效 | `GeometryPose` 使用局部位置、旋转、缩放、阶段和曲线。`ArchitectureMotion` 统一按真实时间播放。路块、楼层和完成波纹复用同一个变形系统 |
| 声音与设置 | `GeometryMusicLoop` 按 BPM、时间标记和循环曲线派发音乐事件。可跟随同长度 AudioSource 或 Timeline。 `AdaptivePuzzleAudio` 有音量分组、强度和重复触发节流 |
| 关卡交接 | 现有教程保持自己的场景、球和机关，由 `CampaignLevelHandoff` 接收其现有完成过渡信号。其它场景可使用 `CampaignLevelAdapter` |

## 操作

- 在城市里按 `A` / `←` 或 `D` / `→` 到相邻已解锁入口。反方向会先减速再回头。停在已显形的入口按 `E` 或 `Enter`。
- 在通用试玩场，入场结束后先进入准备状态，按 `Space` 或点 **Release** 才开始滚动。按 `Esc` 可暂停，选择回选关或主菜单。
- 想跳过正式教程验证全套系统，可从 Unity 直接打开 **Campaign City** 进入 Play Mode。所有入口走通用试玩，后续入口可通过完成试玩依次解锁。试玩使用独立存档，正式战役继续使用自己的进度。

## 存档与扩展

四入口继续读取原有 `BounceBack.Campaign.v1`。增加入口后使用扩展存档，并保留原四入口的兼容数据。已有入口的索引是存档标识，增加入口时应顺序追加。导航只写入停稳的入口，避免在动画中存下无法恢复的位置。完成或显形时立即保存，设置调整自动保存。存档使用 Unity PlayerPrefs。Windows 和 Web 构建各自保留进度。

在 `CityRail.waypoints` 依道路顺序放锚点。每个 `DistrictMaterialization.carrierAnchor` 要落在对应路段上。增加一层建筑只需追加其底部枢轴到 `layers`。在 `CampaignHub.destinations` 的对应位置指定可构建场景。空项以及暂未加入 Build Profiles 的场景会进入共享系统试玩场。新增场景接法见 [LEVEL_INTEGRATION.md](LEVEL_INTEGRATION.md)。

两份新场景都由 `Tools → Bounce Back → Create Missing Campaign Shell` 建立。生成器使用已存在的资产作为基础，仅生成缺失的战役场景。现有手工配置可以继续通过 Inspector 编辑。
