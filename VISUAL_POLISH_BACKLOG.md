# 视觉占位清单

目录 `Assets/Scenes/Archive/BounceBack` 存放正式战役场景。每项当前状态为待制作，带 `待素材` 的项需要原创音频输入。

| 分组 | 场景与对象 | 当前占位 | 目标外观 | 优先级 / 任务 | 依赖 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| 教程 | `Santorini / Architecture Visuals / Cupola Tower, Blue Cupola, Terrace Foundation` | 方块塔、蓝圆柱和方块基座 | 白墙、拱门、蓝色穹顶和崖边楼群 | P0 · K-01 | 默认镜头和球路净空 | 待制作 |
| 教程 | `Santorini / Course` 的露台、三级阶梯、坡道、终点露台 | 单色箱体 | 区分滚道与装饰的浅色石材、圆角台阶和少量蓝色饰边 | P0 · K-01 | 既有 BoxCollider | 待制作 |
| 教程 | `Santorini / Rail Mechanisms / Slime Block 1, Slime Block 2, Deflector` 以及三组 `Rail Guide` 和 `Alignment Marker` | 绿色与蓝色方块、单根导轨 | 两个柔性垫、刚性偏转板、清楚的滑槽和对齐指示 | P0 · K-02 | K-01 配色，`ElasticGeometry.visual` | 待制作 |
| 主菜单 | `Geometric Menu / Menu Architecture / Continue, LevelSelect, Settings, Quit` | 球、长方体和简化花瓣 | 四种辨识度明确的几何设备和弹性展开的部件 | P1 · K-04 | 命中区域和四个 `MenuAction` | 待制作 |
| 主菜单 | `Geometric Menu / Opening Logo, Plaza` | 散开的方形徽标和方块基座 | 开场标记、低多边形广场和连续的建筑过渡 | P1 · K-04 | `logoMotion`、`transition` | 待制作 |
| 选关地图 | `Floating City / Districts / District 1–4` 及菜单中的对应背景 | 方块塔、线框盒、蓝色屋顶 | 四种相异的轮廓、半透明锁定、显形、已完成信标 | P1 · K-05 | `WorldEntrance` 的三组视觉引用 | 待制作 |
| 选关地图 | `Floating City / Perspective Crossing, Projecting Walkways` | 圆形标记和箱体道面 | 可从等距镜头读懂的两端边框、道路衔接和建筑支撑 | P1 · K-05 | 固定的 Waypoints 和相机投影 | 待制作 |
| 选关地图 | `Floating City / Floating Garden, Finale Bridge Visual` | 宽方形甲板、小雕塑和直梁 | 窄边界清楚的悬浮花园、桥和可互动雕塑 | P1 · K-03/K-05 | `gardenFloor` 与终局通行范围 | 待制作 |
| 几何动画 | `Santorini / Retry Fragments` | 程序生成八面体 | 风格统一、重新汇聚时能辨识球形的片段 | P0 · K-02 | `BallRetryEffect.fragmentVisual` | 待制作 |
| 几何动画 | `Santorini / Completion Visuals / Bloom, Geometric Wave 1–3` | 六片球形花瓣、三个线框圆环 | 几何花朵、波纹和建筑重组，镜头平顺接到城市 | P1 · K-03 | `CampaignLevelExit.completion`、K-01、K-05 | 待制作 |
| UI | 教程 HUD、选关 HUD、`Geometric Menu` 设置 | Unity 默认样式 | 易读的按钮、提示与面板 | P1 · K-04 | 对应 `GUISkin` 槽 | 待制作 |
| 灯光氛围 | `Santorini`、`Geometric Menu`、`Floating City` 的方向光和环境色 | 简单方向光和纯色背景 | 清晨爱琴海、菜单和地区光色分明 | P2 · K-06 | K-01、K-04、K-05 | 待制作 |
| 音频联动 | 菜单和地图中的 `Music and Markers`，教程 `Audio`，终局 `finaleSound` | 教程与地图已接入循环主题，菜单保留静音时标 | 音乐分轨、碰撞/滑动/通关音效和有节拍的建筑变形 | P2 · 用户与 Codex | 菜单音乐与音效 | 部分接入 |
