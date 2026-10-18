# 海克斯塔防：求职展示与面试准备

整理日期：2026-10-17。依据当前项目源码、场景配置及已有 Captures 报告进行静态核对；本次没有重新启动 Unity、执行验证或测量性能。报告中的通过结果是项目已有记录。

## 1. 项目应该怎样定位

建议介绍为：**基于 Unity 的 3D 单机塔防与局内强化构筑 Demo，重点展示 Gameplay、战斗系统、生命周期管理和编辑器验证能力。**

适合围绕 Unity 客户端 / Gameplay 方向介绍。项目现有证据主要支持玩法系统实现、系统交互与完整流程，不能据此证明图形底层、联网同步或大规模并行计算能力。

一句话介绍：

> 这是一个使用 Unity 和 C# 制作的塔防 Demo，包含三张独立地图、五类防御塔、四种法术与 54 张局内强化卡。我重点实现和打磨了连续出怪、战斗联动，以及对象池复用、暂停和重开时的状态一致性。

第一人称内容须按本人实际承担的工作调整。模型等资源使用 Kenney、Quaternius 等第三方素材，应明确说明自己的工作是程序实现、玩法设计、场景搭建或资源集成中的哪些部分。

## 2. 实际使用的技术

| 技术 | 项目中的具体应用 | 可以体现的能力 |
|---|---|---|
| Unity 6000.6.3f1、C#、MonoBehaviour | 组件化运行逻辑、生命周期回调、Prefab 与场景组织 | Unity 基础与完整项目落地 |
| URP 17.6 | 低多边形场景、材质、灯光、后处理和战斗表现 | 渲染管线使用与表现整合；不等于自研渲染管线 |
| 序列化配置 | EnemyProfile、WavePlan、SpawnBatch、塔 Prefab 和关卡组件配置 | 在共用逻辑上扩展内容 |
| 运行状态管理 | Preparation / Combat / Victory / Defeat，加上暂停、选牌、系统菜单标记 | 多种交互状态的协调 |
| 绝对时间调度 | AssaultTime 与每批敌人的开始时间、间隔、数量比较；while 补齐已到期生成任务 | 倍速、跨帧后的调度一致性 |
| 自定义路径移动 | 路点路线、双列偏移、转角交接、累计剩余距离 | 向量运算、移动与索敌规则 |
| 战斗结算 | 单体、范围、减速、冻结、易伤、持续伤害、治疗、兵营近战与 Boss 抗性 | Gameplay 规则组合 |
| Unity ObjectPool<T> | 按敌种分别建立敌人池，复用弹体、火球和命中表现 | 对象生命周期与复用 |
| 生成版本号 | EnemyAgent / SoldierAgent 的 SpawnVersion；弹体、拦截、控制冷却记录同时检查对象与版本 | 防止复用后的过期引用生效 |
| 攻击参数快照 | ShotSpec 在发射时记录等级、伤害、范围和状态参数 | 避免升级改变已发射攻击的规则 |
| 强化定义与局内状态 | AugmentCatalog、AugmentDefinition、AugmentState；54 张卡按品质和可用条件筛选 | 内容组织、组合规则与状态隔离 |
| 独立随机序列 | 抽卡和掉落分别使用 System.Random；保存本局种子 | 复现抽取结果、隔离随机调用的影响 |
| 新 Input System | Mouse / Keyboard 输入，搭配 UI 输入模块与世界射线检测 | 操作交互与 UI 输入隔离 |
| UGUI、TextMesh Pro | HUD、选牌、菜单、SDF 中文字体、范围提示、分辨率适配 | 游戏 UI 集成与可读性处理 |
| C# 事件 | Changed 事件通知界面刷新，GameSettings.Changed 通知设置变化 | 界面与状态的基本解耦 |
| PlayerPrefs | 保存语言、音量和难度 | 本地设置持久化；不是对局存档系统 |
| 运行时特效 | ParticleSystem、TrailRenderer、LineRenderer、几何图形、受限特效池 | 战斗信息反馈与对象数量控制 |
| Unity Editor 工具 | 地图、关卡、Prefab、图标与字体配置，边界验证、自动试玩、JSON 报告 | 降低重复配置成本和回归成本 |

不要把 Packages 中安装的包都写成亲自实现的技术。现有战斗移动是自定义路点移动，不应写成 NavMesh / A* 寻路；当前 Gameplay 配置也不应写成 ScriptableObject 配置框架、Excel 导表或外部 JSON 热更新。

另外两个可作为面试补充的实现：攻击染色使用 MaterialPropertyBlock 保存和恢复材质属性；本地化在动态 HUD 中分别保存源文与上次显示内容，避免中英往返切换时把译文当成新的源文。现有音量设置控制 AudioListener.volume，尚不足以称为完整音频管理或混音系统。

## 3. 最值得展开的技术难点

### 难点 A：对象池中的旧引用仍然指向同一个对象

场景：塔向敌人 A 发射子弹。A 提前死亡回池，同一个 GameObject 被复用为敌人 B。子弹只检查 target != null 和 IsAlive，会把 B 当作原来的 A。

当前处理：每次敌人初始化时递增 SpawnVersion，子弹发射时记录目标和版本，命中时同时核对。近战拦截及按敌人保存的联动冷却也采用类似思路。回池和奖励结算还要防止重复执行。

面试表达：

> 对象池最容易忽略的地方是，同一个对象引用不一定代表同一次生成。我给敌人增加了生成版本号，弹体发射时保存版本，命中时同时验证引用、存活状态和版本，避免旧弹体伤到复用后的新敌人。这个约束也用在兵营拦截和敌人控制冷却上。

准备阅读：EnemyAgent.cs 的 Initialize；ProjectileAgent.cs 的 Launch、Update、HasValidTarget；LevelSession.cs 的 ResolveEnemy 和 ReturnProjectile。

验证方向：发射后杀死目标、立即复用相同实例，检查旧子弹不能扣除新敌人的生命。项目的 CombatBoundaryValidation 和三级塔验证中有对应检查。

### 难点 B：强化联动的触发边界与递归控制

场景：范围伤害、碎冰、蒸汽、追加炮击和友军死亡爆炸发生组合。如果追加伤害继续经过同一套触发入口，可能重复触发、刷资源或形成循环。

当前处理：ShotSpec 使用 isSecondary 标记部分追加攻击；联动范围伤害走受控结算入口；通过按目标、按来源或全局冷却限制频率。炮击碎冰只消费直接目标的冰霜标记，范围命中不继续触发同类碎冰。按敌人记录冷却时也检查生成版本。

面试表达：

> 强化系统难在效果组合后的触发规则。我把普通攻击与部分追加攻击区分开，让二次结算不会再次启动同类联动，并按效果设计目标冷却或全局冷却。这样可以保留冰火和混合火力的组合收益，同时约束单次攻击的触发次数。

必须能解释一个具体例子，例如交叉火力、每四次炮击追加爆炸或冰火蒸汽，不要只说“做了 54 张卡”。当前效果逻辑仍主要在 C# 条件分支中实现，不能介绍为通用可视化技能编辑器或完整 GAS 框架。

### 难点 C：暂停、倍速、选牌、设置菜单叠加

场景：战斗处于 2 倍速，进入强化选择时暂停；此时再打开设置、关闭设置。正确结果应该仍停留在选牌，选完才恢复此前速度。若先手动暂停，再打开并关闭设置，也不能擅自恢复战斗。

当前处理：SessionPhase 与多个状态标记共同决定 IsCombatRunning；选牌保存此前阶段、暂停状态和速度；菜单单独保存此前暂停状态；各操作入口检查状态。敌人、子弹和法术在推进时间或结算之前也有暂停护栏。UI 动效可使用 unscaledDeltaTime，使菜单在战斗暂停时仍有反馈。

面试表达：

> 我没有只把暂停当成一次 timeScale 赋值。系统还需要记住暂停前的状态，并在建造、施法、选牌等入口统一检查当前权限。例如在 2 倍速选牌期间打开设置，关闭设置后仍然保持选牌暂停，选择完成才恢复 2 倍速。

能补充：普通战斗暂停允许规划和购买；选牌、系统菜单等覆盖层会限制相应操作；关闭覆盖层时还需要阻止同一次点击穿透到地图。

### 难点 D：双列路线在转角保持形状，索敌遵循玩法目标

场景：双列敌人沿折线路线移动，简单给每段加横向偏移，在转角会错位或切角。索敌若只选离塔最近的敌人，也不一定优先处理即将漏过的目标。

当前处理：用相邻路段方向求偏移转角的交接位置，并限制锐角偏移长度；使用剩余路径距离描述敌人的推进程度。塔先过滤射程，再选择剩余路程最短的敌人。

面试表达：

> 地图路线是固定的，所以我用路点和距离推进，便于精确控制双列出怪和移动。双列在转角需要按前后方向计算交接点；塔在射程内优先攻击剩余路程最短的敌人，让行为符合“优先阻止漏怪”的规则。

不是动态障碍寻路。当前塔的索敌仍是扫描活动敌人，复杂度约为每轮所有塔数乘敌人数；更大规模时是否改为网格查询，应先依据性能采样决定。

### 难点 E：死亡、清场和回调发生在同一条调用链

场景：火球的一次伤害杀死最后一个敌人，胜利立即触发清场，火球在自己的伤害回调中就被回收。如果回调返回后继续执行旧循环，可能产生多余结算或修改已复用实例。

当前处理：FireballArea 在回调后检查播放版本与 burning 状态；循环和范围结算检查战斗是否仍在运行；遍历会被伤害回调修改的集合时，部分代码先取快照。

面试表达：

> 我特别处理了“最后一击触发胜利清场”的重入边界。火球回调可能间接回收自己，所以回调结束后需要再次确认本次播放仍有效，不能继续执行原来的伤害循环。

这也带来取舍：ToArray 等集合快照有分配成本。现阶段它解决遍历期间集合变化的问题，后续可以结合 Profiler 决定哪些热点改用复用缓冲区；不要声称已经实现零 GC。

### 备用案例：运行中切换难度

重新计算属性但不重置敌人路线、控制状态和生成身份；现存敌人按生命比例换算，存活单位至少保留 1 点生命。额外起始经济应限制重复领取，作弊模式结束后恢复有限资源账户。这个案例适合说明跨系统修改时怎样维护不变量。

## 4. 演示视频：推荐约三分钟

| 时间 | 画面 | 口播内容 |
|---|---|---|
| 0:00–0:15 | 最精彩的实战片段，然后显示项目名 | “这是一个 Unity 3D 塔防 Demo，包含三张地图、五类塔、四种法术与局内强化构筑。” |
| 0:15–0:45 | 建塔、升级、双路敌人、一次火球 | “玩家要在持续进攻中分配金币和魔法值，通过塔位、升级和施法时机完成防守。” |
| 0:45–1:15 | 选牌、单卡刷新、冰火或混合火力联动 | “强化共有银、金、彩三个品质，会根据关卡和可用机制筛选候选。这里展示一种具体联动。” |
| 1:15–1:50 | 三级兵营、英雄、拦截、增益塔、Boss | “第三关加入友军拦截、治疗易伤和 Boss 抗性；升级还会改变单位构成和战斗能力。” |
| 1:50–2:20 | 2 倍速 → 选牌/设置 → 恢复；配一张版本号示意 | “技术上我重点处理对象池的过期引用，以及暂停、倍速和选牌叠加时的状态一致性。” |
| 2:20–2:45 | 清晰放大一个验证结果，再切胜利/重开 | “我通过编辑器验证覆盖复用误伤、联动冷却、升级边界和重开清理，并保存检查报告。” |
| 2:45–3:00 | 三关快速切镜、代码/下载入口、署名 | “这是面向 Unity 客户端和 Gameplay 方向的个人作品。模型采用第三方资源，程序职责按实际贡献列明。” |

视频应让观众看懂一条完整链路：操作 → 战斗变化 → 系统解释。技术画面只选两处，避免连续展示看不清的代码和报告。

主视频优先使用正常资源模式。若用作弊模式快速展示满级能力，应标注“功能展示模式”，不能把它剪成正常经济通关证明。截图中的 ∞ 资源就是作弊模式，录制前注意检查。

可把完整通关录像作为附加材料，主视频保留有代表性的片段。菜单和设置用数秒交代即可，把更多时间留给战斗与构筑的可见结果。

## 5. 可直接改写的简历内容

**海克斯塔防｜Unity / C#｜个人 Gameplay Demo**

技术栈：Unity 6、C#、URP、UGUI / TextMesh Pro、Input System、Unity ObjectPool、Unity Editor 工具。

- 完成三关塔防玩法闭环，涵盖双入口连续出怪、五类防御塔、四种法术、兵营拦截与 Boss 战，并接通选关、胜败结算及重开流程。
- 实现 54 张局内强化卡及候选筛选、独立刷新与战斗联动，通过普通/追加攻击区分和分层冷却约束组合效果的触发频率。
- 为敌人、弹体及法术引入对象池复用，使用生成版本校验避免过期目标引用生效，并处理重复结算及清场回调边界。
- 编写关卡配置与编辑器验证工具，覆盖暂停倍速、卡池筛选、技能升级、联动冷却和重开清理，输出 JSON 检查报告。

篇幅紧张时保留前 3 条，把验证能力并入第 3 条。职责需要按实际承担范围修改；“独立开发全部内容”“自研全部美术”等表述需要真实依据。

可以使用的规模数字是三关、五类塔、四种法术、54 张卡。第三关配置为 18 波、共 552 个敌人；这里是全局累计生成数量，绝不能写成“552 单位同屏”。

不建议使用未经测量的表述：性能提升 50%、稳定 120 FPS、零 GC、高并发万人同屏。使用对象池只能证明采用了复用策略，不能自动证明某个性能收益。

## 6. 面试开场：约一分钟

> 我的作品是一个 Unity 单机塔防 Demo，加入了局内强化构筑。当前有三张独立地图、五类塔、四种法术和 54 张强化卡，包含连续出怪、兵营拦截、Boss、胜败和重开等完整流程。
>
> 我主要想展示的是 Gameplay 系统实现和系统交互的稳定性。例如对象池复用后，旧子弹仍可能引用同一个敌人实例，所以我在命中时验证生成版本；强化联动通过追加伤害标记和冷却约束，避免重复触发。暂停、倍速、选牌和设置菜单也有状态恢复与输入拦截处理。
>
> 验证方面，项目有编辑器边界检查和自动试玩记录。接下来我会进一步做性能采样，并根据热点改进集合分配和索敌；架构上也会逐步拆出目前集中在关卡控制器中的职责。

如果面试官问最难的问题，优先讲“旧弹体误伤复用敌人”或“最终一击触发回收重入”。用“触发场景 → 错误原因 → 当前处理 → 验证方法 → 局限”展开，不必背术语清单。

## 7. 高频追问准备

1. **为什么对象池还需要版本号？** 引用相同只说明是同一个实例，不代表同一次生成；版本号区分复用前后的身份。
2. **对象池大小 256 代表最多 256 个敌人吗？** 不是。Unity ObjectPool 的 maxSize 限制的是回收后保留的非活动对象数量，不能当作活动单位上限。当前特效表现另有 48 个活动 Burst 的显式限制。
3. **使用随机种子就能确定性回放吗？** 不能。本项目用种子复现抽卡/掉落的随机结果，并分离随机序列；运行时战斗仍依赖帧推进和玩家输入，不是确定性模拟或录像回放系统。
4. **为什么不用 NavMesh？** 当前地图是固定路线，需求更适合路点距离推进；如果未来出现自由放塔改路或动态障碍，需要重新评估寻路方案。
5. **如何避免追加伤害循环？** 识别普通与追加攻击、限制追加效果入口，并设计目标/来源/全局冷却；举当前代码中的一个具体效果说明。
6. **怎么证明暂停有效？** 不只观察敌人位置，还检查弹体命中、持续伤害、控制和冷却、选牌状态，以及恢复后是否保留原速度。
7. **如何测对象池的收益？** 固定硬件、构建版本、关卡和生成规模，对比有池/无池或改动前后的 CPU、GC Alloc 与帧时间；当前没有这样的实测对比，不报告百分比。
8. **当前数据驱动做到哪一步？** 波次、敌种、塔参数等可序列化配置；强化定义集中管理，但具体效果还有显式 C# 分支，尚不是无需编程即可扩展的通用框架。
9. **为什么使用 partial class？** 按功能整理同一个 LevelSession 的源码，便于浏览；编译后仍是同一个类，不代表完成了职责解耦。
10. **现在架构有什么不足？** LevelSession 承担调度、经济、战斗与强化等较多职责；GameHud 也较集中。后续可按变更频率拆出独立服务，但不应只为凑设计模式而重构。
11. **用了什么设计模式？** 对象池明确存在；事件通知类似观察者；运行流程有枚举和状态标记。不要因为存在不同塔种就宣称完整策略模式，也不要因为有 SessionPhase 就宣称通用分层状态机框架。
12. **美术和 AI 工具如何说明？** 如实区分资源来源、自己完成的程序工作以及工具协助。若使用 AI，应能解释并验证提交的实现，现场可以定位和修改关键逻辑。

还要知道两个具体边界：兵营士兵重生目前仍有 Instantiate / Destroy，不能说所有战斗单位都池化；塔普通开火每帧最多一次，不能说整套战斗是固定步长、严格帧率无关的模拟。当前验证是自定义 Editor / Play Mode 检查，不应说已建立 NUnit 全覆盖或 CI 流水线。

## 8. 已有证据与表述边界

- 当前源码和场景支持三关，构建列表包含 MainMenu 与三张战斗地图。
- Captures/AugmentRework/synergy-validation-level1.json、level2.json、level3.json 分别包含 33、41、48 条检查，均记录 passed=true、failures=0。不同关卡有重复检查，不能把相加结果称为 122 个独立功能或全部卡牌组合覆盖。
- Captures/Mastery/tower-mastery-validation.json 包含 20 条通过检查；spell-mastery-level1/2/3.json 分别为 10、24、37 条通过检查。它们是已有验证结果，本次没有重跑。
- Captures/ThirdLevelGameplay/run-mixed.json 记录第三关 18 波、551 击杀、1 漏怪、24 剩余生命、两只 Boss 均击败。该记录对应早于当前 54 卡重设计的版本，只能作为历史正常经济试玩证据，不能代替新版全流程回归。
- 当前未找到足以支撑具体帧率、GC 或 CPU 优化幅度的性能报告。
- Builds/FirstPlayable 的 README 标明 0.2.0、单关、一次升级等旧内容，TowerDefense.exe 修改日期为 2026-10-13。不要将这个旧包作为当前三关版本交付。

## 9. 正式投递前的优先动作

1. 从当前工程重新构建 Windows 版本，核对三关、54 卡和当前升级行为；在独立 Player 中验证首页、选关、失败、胜利、重开与退出。
2. 新版本录制一次正常资源的代表性通关，再录制展示联动与满级能力的短片；记录所用版本和难度。
3. 保存一份第三关代表性压力片段的 Profiler 数据，记录硬件、分辨率、构建类型、活动单位数、CPU/GPU 帧时间与 GC Alloc，再决定是否把性能数据加入简历。
4. 把上面两个主案例讲熟：能找到代码、解释问题、描述验证方式，并说明实现的局限。

本轮只生成求职说明，没有修改游戏代码、重新打包或执行以上待办。

## 10. 核心源码索引

- [LevelSession：选牌状态与恢复](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/LevelSession.cs:370>)，以及 [调度](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/LevelSession.cs:487>)、[重开与随机序列](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/LevelSession.cs:826>)。
- [EnemyAgent：双列路线](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/EnemyAgent.cs:102>)，以及 [生成版本号](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/EnemyAgent.cs:129>)。
- [ProjectileAgent：发射时保存目标版本](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/ProjectileAgent.cs:47>)，以及 [命中与回收顺序](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/ProjectileAgent.cs:94>)。
- [ShotSpec：攻击参数快照](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/ShotSpec.cs:14>)。
- [AugmentSynergies：受控联动入口](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/LevelSession.AugmentSynergies.cs:229>)。
- [SystemMenu：菜单暂停恢复与输入阻挡](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/LevelSession.SystemMenu.cs:10>)。
- [FireballArea：伤害回调后的有效性检查](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/FireballArea.cs:143>)。
- [TowerAgent：按剩余路程索敌](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/TowerAgent.cs:209>)。
- [SoldierAgent：双向拦截关系](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/SoldierAgent.cs:147>)，以及 [BarracksAgent：复活](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/Gameplay/BarracksAgent.cs:100>)。
- [GameSettings：设置持久化](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/UI/GameSettings.cs:37>)、[Localization：翻译](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/UI/Localization.cs:62>)、[LocalizedCanvas：动态源文维护](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Scripts/UI/LocalizedCanvas.cs:80>)。
- [FrontendValidation：菜单与暂停验证](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Editor/FrontendValidation.cs:69>)、[AugmentSynergyValidation](<D:/Unity/UnityProjects/Tower Defense/Assets/Game/Editor/AugmentSynergyValidation.cs:1>)。
