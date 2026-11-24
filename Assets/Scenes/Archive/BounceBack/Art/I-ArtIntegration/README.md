# I-ArtIntegration

正式场景 `Santorini Gameplay` 的美术由 `SantoriniSystemsAuthoring.Create` 调用 `SantoriniArtIntegration.Apply` 生成。不要手改该场景。

## 接入

| 模块 | 结果 |
| --- | --- |
| A 浮岛 | 已接入。顶面角点误差 0.005 m，包围盒与交付一致，未镜像。 |
| B 发射塔 | 已接入。2600 三角面，锚点 `(-5.64, -0.22, -3.65)`。 |
| C 高架弯道 | 顶面和护栏已接入。底面不合格，见下文。 |
| D 弹性池石沿 | 已接入。水面顶面角点误差 0。 |
| E 升降门 | 门楼已接入。墙板跟随 Lift Wall，并挂在会折叠的庭院下。 |
| F 终点露台 | 五件都已接入。露台和斜唇的碰撞角点误差为 0。动态压力板保留。 |
| G 水 | 水池和瀑布已接入。波纹接到 `ElasticGeometry.BallCompressed` / `BallRebounded`。 |
| H 道具 | 11 件道具加 3 只绕塔海鸥。海鸥没有碰撞。 |
| Background | 只实例化现有 `Santorini Backdrop`，放在关卡根，不随折叠缩放。未改 Background。 |

被替换的白模只关闭 Renderer。碰撞体、刚体、触发器和玩法组件仍在。

仍保留的白模：没有对应交付的石灰石踏步、坡道基础和楼梯拱门，以及必须继续运动的终点压力板。K 组不在本次范围。

## 原活塞和球路

活塞仍是 `BallSpawn` 上的 `PistonLauncher`，不是直接给球一个初速度。

- 直径 `0.76`
- `launchSpeed` `5.9`
- `stroke` `0.28`（场景覆盖）
- `pushTime` `0.14`（发射器 Prefab 默认值，场景未另覆写）
- `requireStarterPiston` 为 1
- 出生点在塔顶，本地抬高一个半径

隔离副本里的球路：释放到第一次弹跳 2.94 秒，到压力板 5.10 秒。正式镜头正交 9.5 时，1080p 球高理论值是 43.2 px。

## 测试

隔离项目：`.utmp/I-QA`。

| 阶段 | 结果 | XML |
| --- | --- | --- |
| 原活塞基线 EditMode | 40 通过 / 0 失败 / 0 跳过 | `.utmp/I-QA/Logs/Baseline-Clean-EditMode.xml` |
| 原活塞基线 PlayMode | 15 通过 / 0 失败 / 0 跳过 | `.utmp/I-QA/Logs/Baseline-Piston-PlayMode.xml` |
| 美术场景 EditMode | 40 通过 / 0 失败 / 0 跳过 | `.utmp/I-QA/Logs/Art-EditMode.xml` |
| 美术场景 PlayMode | 15 通过 / 0 失败 / 0 跳过 | `.utmp/I-QA/Logs/Art-PlayMode.xml` |

PlayMode 总耗时 69.76 秒。战役折返时的过闸计时在负载较高时曾停在测试自己的 0.35 秒上限；同一场景单独重跑和随后的完整 PlayMode 都通过，最终 XML 是 15/15。

## 截图

均为 QA 中 Play Mode 的 Unity URP 1920×1080，不是摆拍。

- `Preview/01-formal.png`：正式镜头，正交 9.50，球 43.2 px
- `Preview/02-preparation.png`：准备阶段，活塞和球在塔顶
- `Preview/03-airborne.png`：已释放，位置 `(-0.77, 0.99, 1.96)`，速度 `(0.53, 5.83, 4.20)`
- `Preview/04-fold.png`：已通关，`Courtyard Art` 缩放 0.62，球在终点附近 `(6.72, 1.00, 5.98)`

## C 的对齐

Unity 实测与几何文档一致：

- 六段坡道顶面最大误差 `0.00519 m`，直坡就是这个数，其余顶面为 0
- 八段护栏角点误差 `0`
- 底面最大误差 `0.05264 m`，来自落池坡；弯道 3 为 `0.02139 m`，弯道 4 为 `0.04059 m`
- 底面超过 `0.02 m`，不能算通过
- 顶面合格，所以整张桥面网格替换了坡道白模。落池坡法线方向高出 `0.14765 m`，对应斜面上的檐线，不是另一套碰撞

世界包围盒中心 `(-2.74, 2.40, -2.29)`，半尺寸 `(2.05, 2.62, 2.31)`，与文档的世界范围一致。拱廊和装饰台阶没有进入路面净空。

## 三角面、材质、光和特效

生成器统计的已接受三角面：QA `23146`，随后主编辑器用当时磁盘上的资源再生成是 `22840`。差别主要来自 F 栏杆网格在两次生成之间有更新。分件文档数量：A 3136、B 2600、C 3660、D 852、E 1764、F 4096、G 池面 2304 加瀑布带。H 为 11 个道具实例和 3 只海鸥。

共享材质在 `Materials/`，按槽位原名重映射，不改 A–H 原材质。`MV_Mint` 只给活塞和可动墙板。`MV_Gold` 只给球、终点和门亭小顶饰。水用 `MV_Water`，穹顶和窗用 `MV_DomeBlue`。

场景内一盏暖色主光，大约 45° 俯角，从镜头左后方照来。环境光是粉天空、杏色地平线、紫色地面。线性雾从 36 到 160。三层高度雾挂在庭院下，随折叠走。场景 Volume 有轻 Bloom、偏暖色彩分级和轻暗角。相机开启 URP 后处理。没有改全局 URP Asset。

水池波纹使用 G 的 `WaterRipple`，由 `PoolRippleLink` 接到弹性池。海鸥用 `SeagullOrbit` 绕塔顶，没有碰撞。

## 主场景和 WebGL

主编辑器活动场景在生成前是 `Santorini Gameplay`，`isDirty=false`。随后执行 `Tools/Bounce Back/Create Gameplay Systems Scene`，大约 12 秒完成。日志有 `SANTORINI_SCENE_SAVED ... replace=True`。生成后场景仍是该文件，`isDirty=false`。场景里有四个美术锚点、Backdrop、浮岛、塔、弯道、水池、海鸥、墙板跟随脚本、折叠 `folding` 引用，以及上面的活塞数值。

WebGL 未改：`webGLMemorySize=256`，`webGLInitialMemorySize=256`。Build Settings 只有 `Geometric Menu` 和 `Santorini Gameplay`。

## 风险

- C 的底面误差 0.05264 m 仍超过 0.02 m。
- 主编辑器和 QA 的已接受三角面差 306，因为主场景生成时 F 栏杆资源已经和 QA 同步时不同。
- `Assembly-CSharp-Editor` 里 D 的编辑脚本仍有既有编译错误。它没有挡住 GeometryWorld 的生成和测试，但主编辑器控制台会继续报这些错误。
- 过闸计时断言对编辑器卡顿敏感。最终完整 PlayMode 是 15/15。
