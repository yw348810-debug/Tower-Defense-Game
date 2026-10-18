# 音频资产

本次为《海克斯塔防》制作了 4 段背景音乐与 25 个游戏音效，放在 `Assets/Game/Resources/Audio/`。它们由项目内的离线合成脚本原创生成，没有使用下载音乐、第三方采样或已有歌曲旋律。本项目及后续作品可以使用、修改和发布这些生成文件，无需署名外部音频作者。

## 背景音乐

| 资源名 | 时长 | 表现 |
| --- | --- | --- |
| `Music_Menu` | 54.86 秒 | 平静的奇幻主题；柔和拨弦、长音与木管旋律。 |
| `Music_Forest` | 48 秒 | 轻快的策略主题；拨弦节奏、木管、轻鼓与低音。 |
| `Music_Canyon` | 48 秒 | 冒险主题；小调旋律、木槌音色、鼓点与过门。 |
| `Music_Snow` | 60 秒 | 冷调史诗主题；冰晶音色、长音、木管与低沉鼓点。 |

背景音乐为立体声，采用 16 小节的原创编排。音符尾音和混响环形叠加至循环起点，不会在文件末尾突然切断。前后两半有配器变化，避免一个短小动机立即反复。建议运行时音乐基准音量约为 `0.22–0.30`，战斗音效比音乐更突出。

## 音效

全部音效为单声道；资源名如下。短音效有淡入、淡出和安静边缘，避免播放开始或结束时出现爆音。

| 用途 | 资源 |
| --- | --- |
| 按钮和菜单 | `SFX_UIClick`、`SFX_UIConfirm`、`SFX_UICancel` |
| 建造与升级 | `SFX_Build`、`SFX_Upgrade` |
| 海克斯选择 | `SFX_AugmentRefresh`、`SFX_AugmentPick` |
| 防御塔攻击 | `SFX_GunShot`、`SFX_FrostShot`、`SFX_CannonShot` |
| 命中与范围伤害 | `SFX_ProjectileHit`、`SFX_Explosion`、`SFX_Shatter` |
| 友方单位 | `SFX_SoldierHit`、`SFX_SoldierDeath` |
| 四种法术 | `SFX_Fireball`、`SFX_FrostNova`、`SFX_Reinforcements`、`SFX_Battlecry` |
| 关卡提示 | `SFX_WaveStart`、`SFX_EnemyLeak`、`SFX_BaseDanger`、`SFX_BossArrival` |
| 战斗结果 | `SFX_Victory`、`SFX_Defeat` |

## 再生成

使用 Python 3.10 或更高版本，并安装 NumPy、SciPy。在项目根目录执行：

```powershell
python Tools/Audio/generate_audio.py
```

脚本的随机种子固定为 `481516`。它覆盖同名 WAV，并重新生成 `Docs/Audio-Validation.json`。只需导入 WAV 即可运行游戏，玩家机器不需要 Python 或这些依赖。

## 文件检查

所有文件均为 PCM WAV、44.1 kHz、16 bit。背景音乐峰值约 `-5.04 dBFS`，音效峰值约 `-3.35 dBFS`，保留运行时混音余量。

生成脚本逐个检查音频是否含无效数值、裁切样本、异常时长，以及短音效的首尾是否归零。音乐额外检查循环接缝的样本变化是否处于该段音乐的正常相邻样本变化范围内。循环接缝不是静音，也无需额外淡出再淡入。实际指标与检查结果记录在 `Audio-Validation.json`。

设置里的音量滑块由运行时音频系统统一应用到音乐和音效；这些 WAV 只负责声音素材本身。

## Unity 运行与验证

`GameAudio` 在运行时自动创建一个跨场景音频对象，无需为已有场景手工挂音源。两路背景音乐使用1.25秒交叉淡化，24路音效池配合射击/命中限流和关键事件优先级。音乐采用流式 Vorbis，短音效预加载 PCM；所有声音的总音量只通过现有 `AudioListener.volume` 应用一次。

暂停和选择海克斯时，已播放的战斗声音暂停、新的攻击声音不播放；音乐、菜单操作、建造和升级仍可播放。重开、切关和返回首页会清理战斗及结算声音。快速返回上一场景会续播原音乐并反向淡化，避免同曲重复叠播。

2026-10-18 在主页和三关分别通过121项音频边界检查，包括29个资源、导入格式、实际解码和播放、池容量/抢占、音量、暂停恢复、结算清理及快速音乐切换。另通过三关真实玩法事件的音效触发检查，以及战斗设置滑块对总音量的控制检查。实际 `AudioListener` 混音采样确认：正常音量有输出，设置为0后输出为0，恢复后重新有输出。报告位于 `Captures/Audio/`。

Unity 编辑器可执行 `GameAudioValidation.Run()` 重跑边界检查。通过 `Tools > Tower Defense > Configure Audio Imports` 可重新应用本项目音频导入设置。
