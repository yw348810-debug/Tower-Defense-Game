# Kenney 地图导入与场景说明

## 已完成

- 源包：默认下载目录中的 `kenney_tower-defense-kit.zip`，Tower Defense Kit 2.1。
- 导入 160 个 FBX 模型、原始配色贴图及 variation-a 变体，保留原包 License.txt。
- 资源目录：`Assets/ThirdParty/Kenney/TowerDefenseKit`。
- 默认模型统一使用 `Assets/Game/Materials/Kenney_Default_URP.mat`，第二关环境使用 `Kenney_Canyon_URP.mat`。
- 使用项目现有 URP；模型、贴图和原包的 CC0 授权文件均留在项目中。

## 打开地图

在 Unity Project 窗口进入 `Assets/Game/Scenes`，双击一个场景。若原场景有未保存修改，先按需要保存。

| 场景 | 内容 |
|---|---|
| Level01_ForestOutpost | 林间哨站：双入口在弯道合流、3.4米宽双列道路、6 个塔位 |
| Level02_CanyonConfluence | 岩谷汇流：3.4 米宽两路合流、8 个塔位，已接入第二关战斗 |
| Level03_CoreFortress | 核心要塞：雪地风格、双线进攻、10 个塔位 |

每关独立包含 Main Camera、Sun 和地图根节点。三关均已接入可玩战斗与顺序通关，塔种、法术、敌人与海克斯逐关解锁，运行说明见项目根目录 README.md。

第三关使用套件自带 `snow-*` 模型；墙面积雪使用独立 `Fortress_Snow.mat`，光照与背景采用冷色。路线、塔位与经济配置不因主题更换而改变。

地图根节点下依次为 Terrain、RoadTiles、EnemyRoutes、BuildSlots、Entrances_And_Base、Scenery 和 Outskirts。外围地面、树林和岩石延伸到屏幕边缘；模块仍是与 FBX 关联的 Prefab 实例，可手动调整。

## 后续玩法接口

- `LevelMap`：关卡 ID、中文名称、初始金币、基地生命、基地 Transform、路线和塔位引用。
- `EnemyRoute`：routeId 与按顺序排列的 waypoints；Scene 视图打开 Gizmos 可查看路线方向。
- `BuildSlot`：slotId、服务区域、可点击 BoxCollider。选中塔位显示 5.5 单位的参考范围；这不是最终塔射程。
- 路点与地图均采用 Unity 世界坐标。每块地砖 2×2 单位，草地顶面 0.4、道路路面 0.2，路点 y=0.24。
- 三关为 21×13 格，相机采用透视投影，36 度俯视并略作水平偏转；`MapCameraRig` 在窗口比例变化时自动框选路线、塔位和基地，按 PC 横屏查看。
- 合流路线拥有各自完整路点，重合路段的世界位置相同。无需在合流处切换路径系统。
- 当前三关初始金币为 180/240/400，基地生命为 20/20/25。各关玩法与验证记录见 README；Three-Level-Design.md 保留首轮规划供对照。

## 创建工具与验证范围

`Tools > Tower Defense > Create Missing Kenney Maps` 只创建缺失的场景，不覆盖已有场景的手动编辑。代码位于 `Assets/Game/Editor/KenneyMapBuilder.cs`，只在编辑器中使用。

场景预览位于项目根目录 `Captures/Maps`。本次检查涵盖脚本编译、资源引用、路线终点、塔位数量与相机渲染；不代表已完成敌人移动、射程或关卡平衡测试。

三关已加入 Build Settings，顺序对应森林、峡谷与雪地地图。前两关胜利后可进入下一张图，三个场景都可独立打开测试。第三关的四种法术、增益塔与两次 Boss 见 `Third-Level-Gameplay.md`。

第一关已增加 `08_WideRoadSurface`：道路、草地切口、路边及原石块细节四个网格。旧地块与道路 Renderer 通过 Prefab 覆盖隐藏，源模型与碰撞仍保留；生成网格位于 `Assets/Game/Maps/WideRoad`。`Tools > Tower Defense > Widen First Level Roads` 可重复生成。
