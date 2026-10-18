using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using TowerDefense.Gameplay;

namespace TowerDefense.UI
{
    /// <summary>English remains the source language; the saved preference changes presentation only.</summary>
    public static class Localization
    {
        static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly List<TranslationRule> Rules = new List<TranslationRule>();
        static TMP_FontAsset chineseFont;

        sealed class TranslationRule
        {
            public Regex expression;
            public Func<Match, string> translate;
        }

        public static TMP_FontAsset Font
        {
            get
            {
                if (!chineseFont) chineseFont = Resources.Load<TMP_FontAsset>("UI/ChineseFont");
                return chineseFont ? chineseFont : TMP_Settings.defaultFontAsset;
            }
        }

        public static void ApplyFont(TMP_Text text)
        {
            if (text && Font) text.font = Font;
        }

        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english) || GameSettings.Language != GameLanguage.Chinese) return english;
            return Translate(english);
        }

        // Used by the font authoring step to prewarm only the characters this UI needs.
        public static IEnumerable<string> ChineseTexts => Chinese.Values;

        static Localization()
        {
            AddMenu();
            AddGameplay();
            AddAugments();
            AddRules();
        }

        static void Add(string english, string chinese) => Chinese[english] = chinese;
        static void Rule(string pattern, Func<Match, string> translate)
        {
            Rules.Add(new TranslationRule { expression = new Regex("\\A(?:" + pattern + ")\\z", RegexOptions.CultureInvariant), translate = translate });
        }
        static string G(Match match, int group) => match.Groups[group].Value;

        static string Translate(string source)
        {
            if (Chinese.TryGetValue(source, out var exact)) return exact;
            if (Cache.TryGetValue(source, out var cached)) return cached;
            // Values such as currency counts and the Chinese language option need no processing.
            bool latin = false;
            foreach (char c in source) if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) { latin = true; break; }
            if (!latin) return source;
            string translated = source;
            bool matchedRule = false;
            foreach (var rule in Rules)
            {
                Match match = rule.expression.Match(source);
                if (!match.Success) continue;
                translated = rule.translate(match);
                matchedRule = true;
                break;
            }
            // Keep existing whole-message rules (including the two-line barracks summary),
            // then translate appended lines such as max-tier tower abilities independently.
            if (!matchedRule && source.IndexOf('\n') >= 0)
            {
                var lines = source.Split('\n');
                for (int i = 0; i < lines.Length; i++) lines[i] = Translate(lines[i].TrimEnd('\r'));
                translated = string.Join("\n", lines);
            }
            if (Cache.Count >= 1024) Cache.Clear();
            Cache[source] = translated;
            return translated;
        }

        static void AddMenu()
        {
            Add("HEX DEFENSE", "海克斯塔防");
            Add("HEX\nDEFENSE", "海克斯\n塔防");
            Add("TOWER DEFENSE", "海克斯塔防");
            Add("TOWER\nDEFENSE", "海克斯\n塔防");
            Add("THE LAST LINE", "最后防线");
            Add("BUILD. ADAPT. DEFEND.", "建造 · 应变 · 坚守");
            Add("Defend three frontiers. Shape your strategy.", "守卫三处前线，打造你的专属战术。");
            Add("START GAME", "开始游戏");
            Add("SETTINGS", "设置");
            Add("QUIT GAME", "退出游戏");
            Add("SELECT A LEVEL", "选择关卡");
            Add("Choose your battlefield", "选择你的战场");
            Add("CHOOSE YOUR BATTLEFIELD", "选择你的战场");
            Add("Every frontier brings a new challenge.", "每一道防线，都有新的挑战。");
            Add("FOREST OUTPOST", "林间哨站");
            Add("CANYON CONFLUENCE", "岩谷汇流");
            Add("CORE FORTRESS", "核心要塞");
            Add("FROZEN FORTRESS", "核心要塞");
            Add("LEARN THE DEFENSE", "筑起防线");
            Add("HOLD TWO FRONTS", "双线迎敌");
            Add("FACE THE TITANS", "迎战巨兽");
            Add("Establish your defenses in the forest.", "在森林中建立你的第一道防线。");
            Add("Command soldiers and control the battlefield.", "指挥士兵，掌控双线战场。");
            Add("Master powerful spells and confront two bosses.", "驾驭强大法术，迎战两大首领。");
            Add("7 WAVES", "7 波攻势");
            Add("12 WAVES", "12 波攻势");
            Add("18 WAVES", "18 波攻势");
            Add("ENTER BATTLE", "进入战场");
            Add("BACK", "返回");
            Add("MASTER VOLUME", "主音量");
            Add("LANGUAGE", "语言");
            Add("DIFFICULTY", "游戏难度");
            Add("EASY", "简单");
            Add("HARD", "困难");
            Add("CHEAT", "作弊");
            Add("More gold and health. Weaker, slower enemies.\nChanges apply to the current battle.", "更多初始金币与生命，敌人更弱、行动更慢。\n修改会应用于当前战斗。");
            Add("Original enemy strength, speed and starting resources.\nChanges apply to the current battle.", "保留原有敌人强度、速度与初始资源。\n修改会应用于当前战斗。");
            Add("Unlimited gold, base health and mana.\nChanges apply to the current battle.", "无限金币、核心生命和魔法值。\n修改会应用于当前战斗。");
            Add("English", "English");
            Add("ENGLISH", "English");
            Add("Adjust music and sound effects instantly. Your volume is saved.", "即时调节背景音乐和音效，音量设置会自动保存。");
            Add("Your language choice applies throughout the game.", "语言设置会应用于整个游戏。");
            Add("GAME PAUSED", "游戏已暂停");
            Add("Take a breath. Plan your next move.", "稍作休息，规划下一步战术。");
            Add("RESUME GAME", "继续游戏");
            Add("RETURN TO HOME", "返回首页");
            Add("ESC / RESUME", "ESC / 继续游戏");
            Add("ESC / BACK", "ESC / 返回");
            Add("ESC / MENU", "ESC / 菜单");
            Add("THREE FRONTIERS · ONE DEFENSE", "三处前线 · 一道防线");
        }

        static void AddGameplay()
        {
            Add("RAPID FIRE", "速射塔"); Add("FROST", "冰霜塔"); Add("BLAST", "爆破塔");
            Add("BARRACKS", "兵营"); Add("SUPPORT", "增益塔");
            Add("Rapidfire Tower", "速射塔"); Add("Frost Tower", "冰霜塔"); Add("Blast Tower", "爆破塔");
            Add("Barracks", "兵营"); Add("Sanctuary Tower", "增益塔");
            Add("Focus fire", "集火"); Add("Slow", "减速"); Add("AOE 2.4", "范围伤害 2.4");
            Add("Block + fight", "阻挡与近战"); Add("Weaken + heal", "易伤与治疗");
            Add("GRUNT", "步兵"); Add("RUNNER", "疾行者"); Add("BRUTE", "重甲兵");
            Add("SWARM", "虫群"); Add("BREAKER", "破阵者"); Add("FLYER", "飞行怪");
            Add("SHAMAN", "萨满"); Add("WARDEN", "守卫"); Add("TITAN", "泰坦");
            Add("ASSAULT ENDED", "战斗已结束"); Add("PAUSED", "已暂停"); Add("RESUME", "继续"); Add("PAUSE", "暂停");
            Add("CHOOSE AUGMENT", "选择海克斯"); Add("CHOOSE AN AUGMENT", "选择海克斯");
            Add("CLICK MAP / RMB CANCEL", "点击地图释放 / 右键取消");
            Add("Click ground to cast / RMB cancels", "点击地面释放 / 右键取消");
            Add("[Q] AIM", "[Q] 瞄准"); Add("[E] AIM", "[E] 瞄准"); Add("[R] AIM", "[R] 瞄准");
            Add("[F] CAST / GLOBAL", "[F] 释放 / 全场");
            Add("FIREBALL", "火球术"); Add("Fireball", "火球术");
            Add("FROST NOVA", "冰霜魔法"); Add("FROSTNOVA", "冰霜魔法"); Add("FrostNova", "冰霜魔法");
            Add("Frost Nova", "冰霜魔法");
            Add("REINFORCE", "援军"); Add("REINFORCEMENTS", "援军"); Add("Reinforcements", "援军");
            Add("BATTLECRY", "战吼"); Add("Battlecry", "战吼");
            Add("MAX LEVEL / LV 3", "最高等级 / 三级"); Add("FREE RETROFIT TO LV 3", "免费改造至三级");
            Add("ENEMIES APPROACHING", "敌人即将来袭"); Add("SELECTED", "已选择");
            Add("FREE CHARGE", "免费施法"); Add("ASSAULT CLEARED", "攻势已击退"); Add("FINAL ENEMIES", "最后一批敌人");
            Add("Pick a tower and pad, then BUILD.", "选择防御塔和建造点，然后建造。");
            Add("Build and upgrade during the assault.", "迎战敌人时也能建造和升级。");
            Add("BUILD PAD", "建造点"); Add("BUILD", "建造"); Add("VICTORY", "胜利"); Add("BASE LOST", "基地失守");
            Add("PLAY AGAIN", "重新挑战"); Add("NEXT LEVEL / CANYON", "下一关 / 岩谷汇流");
            Add("NEXT LEVEL / FROZEN FORTRESS", "下一关 / 核心要塞"); Add("BACK TO LEVEL 1", "返回第一关");
            Add("CHOOSE YOUR AUGMENT", "选择你的海克斯"); Add("OPENING CHOICE", "开局选择");
            Add("SILVER AUGMENT", "银色海克斯"); Add("GOLD AUGMENT", "金色海克斯"); Add("PRISMATIC AUGMENT", "彩色海克斯");
            Add("CHOOSE THIS AUGMENT", "选择此海克斯"); Add("REFRESH / 1 LEFT", "刷新 / 剩余 1 次"); Add("REFRESH USED", "已刷新");
            Add("Choose one card. Each slot can be refreshed once per choice. Hover equipped cards to review their effects.", "选择一张卡牌。每个卡位在本次选择中可刷新一次。悬停已选卡牌可查看效果。");
            Add("Choose one card. The effect lasts for this battle. Hover your equipped cards to review them.", "选择一张卡牌，效果持续整场战斗。悬停已选卡牌可查看效果。");
            Add("UNLEASH YOUR PRISMATIC POWER", "释放彩色海克斯之力"); Add("REINFORCE YOUR STRATEGY", "强化你的战术");
            Add("SILVER / OPENING", "银色 / 开局"); Add("GOLD / MIDGAME", "金色 / 中期"); Add("PRISMATIC / LATE", "彩色 / 后期");
            Add("Waiting for the level.", "等待关卡加载。"); Add("PAUSED / plan your defense", "已暂停 / 规划防线");
            Add("The enemies reached your base.\nTry frost and blast together.", "敌人突破了防线。\n尝试搭配冰霜塔与爆破塔。");
            Add("2 soldiers / upgrades: 3 soldiers + hero / auto respawn", "2 名士兵 / 升级后：3 名士兵与英雄 / 自动复活");
            Add("Enemies: vulnerability / allies: heal every second", "敌人：易伤 / 友军：每秒治疗");
            Add("Weaken enemies / restore nearby allies", "削弱敌人 / 治疗附近友军");
            Add("01 / FOREST OUTPOST", "01 / 林间哨站"); Add("02 / CANYON CONFLUENCE", "02 / 岩谷汇流");
            Add("03 / FROZEN FORTRESS", "03 / 核心要塞");
            Add("Level configuration is incomplete.", "关卡配置不完整。");
            Add("Choose a Silver augment before the assault begins.", "攻势开始前，先选择一个银色海克斯。");
            Add("Boss approaching! Use support auras, reinforcements and upgraded spells.", "首领来袭！利用增益光环、援军和强化法术迎战。");
            Add("A huge wave is approaching!", "一大波敌人即将来袭！");
            Add("This pad already has a tower. Upgrade it instead.", "该建造点已有防御塔，可以升级。");
            Add("Barracks need a nearby road to rally their troops.", "兵营需要附近的道路来集结士兵。");
            Add("This tower is already fully upgraded.", "这座防御塔已升至最高等级。");
            Add("The core has fallen. Try a different placement and tower mix.", "核心已被攻破，试试调整布局与防御塔组合。");
            Add("Assault cleared. The core is safe!", "攻势已击退，核心安全了！");
            Add("The assault has begun. Build and upgrade while enemies approach.", "战斗开始！敌人来袭时也能建造和升级。");
            Add("Resume the assault to cast Fireball.", "继续战斗后才能施放火球术。");
            Add("Resume the assault to cast Frost Nova.", "继续战斗后才能施放冰霜魔法。");
            Add("Choose a point inside the map.", "请选择地图范围内的位置。");
            Add("Free Fireball cast!", "免费火球术已释放！");
            Add("Frost Nova is still recharging.", "冰霜魔法仍在冷却。");
            Add("Second Wind saved the core! Nearby enemies were banished.", "绝地逢生守住了核心！附近敌人已被驱逐。");
            Add("Battlecry! All towers and allies gain damage, attack speed and range.", "战吼！所有防御塔与友军的攻击力、攻速和范围提升。");
            Add("Card refreshed. Each slot can be refreshed once during this choice.", "卡牌已刷新。每个卡位在本次选择中只能刷新一次。");
        }

        static void Augment(AugmentId id, string name, string description)
        {
            var definition = AugmentCatalog.Get(id);
            Add(definition.Name, name);
            Add(definition.Description, description);
        }

        static void AddAugments()
        {
            // Draft definitions own their bilingual copy, keeping rules, costs and unlocks together.
            foreach (var definition in AugmentCatalog.All)
            {
                Add(definition.Name, definition.ChineseName);
                Add(definition.Description, definition.ChineseDescription);
                Add(definition.SynergyHint, definition.ChineseSynergyHint);
                Add(definition.Description + "\n" + definition.SynergyHint, definition.ChineseDescription + "\n" + definition.ChineseSynergyHint);
            }
            Add("SYNERGY", "联动");
            Add("Burst shells: splash 45% / R 1.25", "爆裂弹：溅射45%伤害 / 半径1.25");
            Add("Iceburst: 1s freeze / R 1.7 / 3s control lock", "冰爆：冻结1秒 / 半径1.7 / 控制间隔3秒");
            Add("Shatter: 64 DMG / R 4", "碎冰：64伤害 / 半径4");
            Add("Champion: every 3rd strike spins", "英雄：每第3次攻击旋风斩");
            // Retired-card translations are kept for compatible editor checks and old references.
            Augment(AugmentId.LuckyLoot, "幸运战利品", "击杀有 20% 概率额外获得基础金币的 50%。魔法值掉落不变。");
            Augment(AugmentId.ConstructionGrant, "建造补助", "接下来建造的 3 座防御塔各少花费 15 金币，不影响升级费用。");
            Augment(AugmentId.ManaScavenger, "魔力拾荒者", "选取后每击杀 8 个敌人，额外获得 1 点魔法值。");
            Augment(AugmentId.LongRange, "远程火力", "所有防御塔攻击范围增加 0.5 米，爆炸与法术范围不变。");
            Augment(AugmentId.LastingFrost, "长效寒霜", "冰霜减速时间由 2.5 秒提高至 3.25 秒。重复命中会刷新减速时间。");
            Augment(AugmentId.BlastCalibration, "爆破校准", "爆破塔普通爆炸半径增加 0.25 米，碎冰爆炸范围不变。");
            Augment(AugmentId.EmergencyShield, "应急护盾", "立即获得 3 点核心护盾。漏过的敌人优先消耗护盾，再扣除生命。");
            Augment(AugmentId.StoredMana, "魔力储备", "立即获得 12 点魔法值，足够释放一次火球术。");
            Augment(AugmentId.FocusedFlame, "聚能烈焰", "火球术燃烧区域内侧一半半径中，每次伤害额外增加 2 点。");
            Augment(AugmentId.CombinedArms, "协同作战", "同时拥有速射塔、冰霜塔和爆破塔时，获得 30 金币和 6 点魔法值。每局触发一次。");
            Augment(AugmentId.HeavyHunter, "重装猎手", "所有防御塔对重甲兵和破阵者的伤害增加 30%，仍会受到护甲减伤。");
            Augment(AugmentId.GunRhythm, "射击节奏", "速射塔每第 3 次普通射击额外发射一颗造成 60% 伤害的子弹。额外子弹不会再次触发。");
            Augment(AugmentId.FrostMark, "寒霜印记", "被冰霜减速的敌人受到速射塔的伤害增加 30%。");
            Augment(AugmentId.ShatterResonance, "碎冰共振", "爆破塔碎冰爆炸的伤害增加 50%，普通爆炸伤害不变。");
            Augment(AugmentId.ExtendedBurn, "持续燃烧", "火球术燃烧时间由 3 秒延长至 4 秒，伤害次数由 6 次增加至 8 次。");
            Augment(AugmentId.FireDividend, "烈焰红利", "每次成功释放付费火球术获得 8 金币。取消施法与免费施法不提供金币。");
            Augment(AugmentId.FieldWorkshop, "战地工坊", "后续防御塔升级费用降低 20%，向上取整。此前花费不会返还。");
            Augment(AugmentId.TypeSpecialization, "兵种专精", "拥有至少 3 座同类型防御塔时，该类型所有防御塔的伤害增加 25%。");
            Augment(AugmentId.BurningMark, "燃烧印记", "处于火球术燃烧区域的敌人受到防御塔的伤害增加 25%。多个燃烧区域不叠加此增益。");
            Augment(AugmentId.FieldMedic, "战地医疗", "选取后每击杀 15 个敌人，为核心恢复 1 点生命，最多触发 6 次，不能超过生命上限。");
            Augment(AugmentId.VeteranTraining, "老兵训练", "兵营士兵和英雄的生命上限与伤害增加 25%。存活单位保留当前生命百分比。");
            Augment(AugmentId.FastReinforcements, "快速增援", "后续士兵与英雄的复活等待时间缩短 25%，已开始的复活倒计时不变。");
            Augment(AugmentId.UltimateRetrofit, "终极改造", "下一次防御塔升级免费并直接升至三级。该塔永久获得 25% 额外伤害。");
            Augment(AugmentId.PiercingRounds, "穿甲弹", "速射塔的普通射击可穿透目标后方最多 2 个敌人，各造成 40% 伤害。");
            Augment(AugmentId.PolarPulse, "极寒脉冲", "冰霜塔每 6 秒冻结附近敌人 1.5 秒。同一目标每 6 秒最多受到一次脉冲。");
            Augment(AugmentId.Aftershock, "余震", "爆破塔命中后 0.6 秒再次爆炸，造成普通爆炸 40% 的伤害。余震不能触发碎冰或再次触发余震。");
            Augment(AugmentId.TwinMeteor, "双重陨火", "每次付费火球术在 0.75 秒后于原位置额外释放一次完整火球术，不再消耗魔法值。");
            Augment(AugmentId.ManaFurnace, "魔力熔炉", "每战斗 15 秒获得一次免费火球术充能，最多储存一次。按 Q 或点击法术卡施放。");
            Augment(AugmentId.LoneGuardian, "孤胆守卫", "周围 10 米内没有其他防御塔的塔获得 50% 额外伤害和 1 米范围。兵营士兵继承伤害加成。");
            Augment(AugmentId.LastStandEngine, "背水一战", "牺牲核心 5 点当前生命与生命上限，获得 100 金币、24 点魔法值，防御塔与友军伤害增加 25%。");
            Augment(AugmentId.SecondWind, "绝地逢生", "本局一次：核心受到致命伤害时保留 8 点生命，并清除附近敌人。被清除的敌人不提供奖励。");
            Augment(AugmentId.ElementalBoil, "元素沸腾", "火球术命中被减速或冻结的敌人时引发蒸汽爆炸，造成 30 点伤害。每个目标每 5 秒触发一次。");
            Augment(AugmentId.VanguardCommand, "先锋号令", "兵营士兵与英雄的攻击力增加 25%，生命上限增加 35%，对后续招募的单位同样有效。");
            // Current data wins over legacy wording above (for example spell discounts and all summons).
            foreach (var definition in AugmentCatalog.All)
            {
                Add(definition.Name, definition.ChineseName);
                Add(definition.Description, definition.ChineseDescription);
            }
        }

        static void AddRules()
        {
            Rule(@"Soldier HP (\d+) / ATK (\d+) / revive ([\d./]+)s", m => "士兵生命 " + G(m, 1) + " / 攻击 " + G(m, 2) + " / 复活 " + G(m, 3) + " 秒");
            Rule(@"Vulnerability \+(\d+)% / ([\d.]+)s", m => "易伤 +" + G(m, 1) + "% / " + G(m, 2) + " 秒");
            Rule(@"Allies heal (\d+) HP/s / range ([\d.]+)", m => "友军每秒恢复 " + G(m, 1) + " 生命 / 范围 " + G(m, 2));
            Rule(@"All (\d+) waves defended\.", m => "已守住全部 " + G(m, 1) + " 波攻势。");
            Rule(@"Base remaining: (\d+|∞)", m => "核心剩余生命：" + G(m, 1));
            Rule(@"1 champion \+ (\d+) veterans / ([\d.]+)s / (\d+) guard HP", m => "1名英雄 + " + G(m, 1) + "名老兵 / " + G(m, 2) + "秒 / 护卫生命" + G(m, 3));
            Rule(@"(\d+) (recruits|veterans) / ([\d.]+)s / (\d+) HP", m => G(m, 1) + "名" + (G(m, 2) == "recruits" ? "新兵" : "老兵") + " / " + G(m, 3) + "秒 / " + G(m, 4) + "生命");
            Rule(@"READY IN (\d+)s", m => "冷却剩余 " + G(m, 1) + " 秒");
            Rule(@"NEED (\d+) MANA", m => "还需 " + G(m, 1) + " 魔法值");
            Rule(@"(\d+) MANA", m => G(m, 1) + " 魔法值");
            Rule(@"(\d+) ALIVE", m => G(m, 1) + " 个存活");
            Rule(@"\+(\d+) SHIELD", m => "+" + G(m, 1) + " 护盾");
            Rule(@"WAVE (\d+) / (\d+)", m => "第 " + G(m, 1) + " / " + G(m, 2) + " 波");
            Rule(@"NEXT WAVE IN (\d+)s", m => "下一波：" + G(m, 1) + " 秒");
            Rule(@"(FIREBALL|FROST NOVA|FROSTNOVA|REINFORCE|REINFORCEMENTS|BATTLECRY) / LV (\d+)", m => Translate(G(m, 1)) + " / " + G(m, 2) + " 级");
            Rule(@"LV (\d+) / (\d+) GOLD", m => "升至 " + G(m, 1) + " 级 / " + G(m, 2) + " 金币");
            Rule(@"(\d+) troops / ([\d.]+)s", m => G(m, 1) + " 名援军 / " + G(m, 2) + " 秒");
            Rule(@"([\d.]+)s / DMG\+SPD\+RANGE", m => G(m, 1) + " 秒 / 攻击·攻速·范围");
            Rule(@"ACTIVE / ([\d.]+)s", m => "生效中 / " + G(m, 1) + " 秒");
            Rule(@"BOSS( / ENRAGED)?\s+(\d+) / (\d+) HP", m => "首领" + (m.Groups[1].Success ? " / 狂暴" : "") + "    " + G(m, 2) + " / " + G(m, 3) + " 生命");
            Rule(@"(OPENING|MIDGAME|LATE GAME) CHOICE / (\d+) OF (\d+) / (BATTLE STARTS AFTER YOUR PICK|BATTLE PAUSED WHILE YOU CHOOSE)",
                m => (G(m, 1) == "OPENING" ? "开局" : G(m, 1) == "MIDGAME" ? "中期" : "后期") + "选择 / 第 " + G(m, 2) + " / " + G(m, 3) + " 次 / " + (G(m, 4) == "BATTLE STARTS AFTER YOUR PICK" ? "选择后开始战斗" : "选择期间战斗暂停"));
            Rule(@"([\d.]+)s (?:burn|BURN) / (?:radius|R) ([\d.]+)", m => "燃烧 " + G(m, 1) + " 秒 / 半径 " + G(m, 2));
            Rule(@"([\d.]+)s (?:freeze|FREEZE) / (?:radius|R) ([\d.]+)", m => "冻结 " + G(m, 1) + " 秒 / 半径 " + G(m, 2));
            Rule(@"([\d.]+)s burn / R ([\d.]+) / (\d+) per tick", m => "燃烧 " + G(m, 1) + " 秒 / 半径 " + G(m, 2) + " / 每次 " + G(m, 3) + " 伤害");
            Rule(@"(\d+) allies / ([\d.]+)s / (\d+) HP", m => G(m, 1) + " 名友军 / " + G(m, 2) + " 秒 / " + G(m, 3) + " 生命");
            Rule(@"([\d.]+)s / DMG \+(\d+)% / SPEED \+(\d+)% / R \+([\d.]+)", m => G(m, 1) + " 秒 / 攻击 +" + G(m, 2) + "% / 攻速 +" + G(m, 3) + "% / 范围 +" + G(m, 4));
            Rule(@"All (\d+) waves defended\.\nBase remaining: (\d+|∞)", m => "已守住全部 " + G(m, 1) + " 波攻势。\n核心剩余生命：" + G(m, 2));
            Rule(@"(RAPID FIRE|FROST|BLAST|BARRACKS|SUPPORT) / LV (\d+)", m => Translate(G(m, 1)) + " / " + G(m, 2) + " 级");
            Rule(@"(RAPID FIRE|FROST|BLAST|BARRACKS|SUPPORT) / PAD (.+)", m => Translate(G(m, 1)) + " / 建造点 " + G(m, 2));
            Rule(@"BASE DMG (\d+) / R ([\d.]+) / AOE ([\d.]+)", m => "基础伤害 " + G(m, 1) + " / 范围 " + G(m, 2) + " / 爆炸 " + G(m, 3));
            Rule(@"Base damage (\d+)\s+Range ([\d.]+)", m => "基础伤害 " + G(m, 1) + "    范围 " + G(m, 2));
            Rule(@"UPGRADE TO LV (\d+) / (\d+) GOLD", m => "升至 " + G(m, 1) + " 级 / " + G(m, 2) + " 金币");
            Rule(@"LV (\d+) UPGRADE / NEED (\d+) GOLD", m => "升至 " + G(m, 1) + " 级 / 还需 " + G(m, 2) + " 金币");
            Rule(@"(Focus fire|Slow|AOE [\d.]+|Block \+ fight|Weaken \+ heal)\s+Range ([\d.]+)", m => Translate(G(m, 1)) + "    范围 " + G(m, 2));
            Rule(@"BUILD  /  NEED (\d+) GOLD", m => "建造 / 还需 " + G(m, 1) + " 金币");
            Rule(@"BUILD  /  (\d+) GOLD", m => "建造 / " + G(m, 1) + " 金币");
            Rule(@"(\d+)/(\d+) soldiers(?: · (\d+)/1 hero)?", m => G(m, 1) + "/" + G(m, 2) + " 士兵" + (m.Groups[3].Success ? " · " + G(m, 3) + "/1 英雄" : ""));
            Rule(@"(.+ soldiers(?: · \d+/1 hero)?)\nSoldier HP (\d+) / ATK (\d+) / revive ([\d./]+)s", m => Translate(G(m, 1)) + "\n士兵生命 " + G(m, 2) + " / 攻击 " + G(m, 3) + " / 复活 " + G(m, 4) + " 秒");
            Rule(@"Vulnerability \+(\d+)% / ([\d.]+)s\nAllies heal (\d+) HP/s / range ([\d.]+)", m => "易伤 +" + G(m, 1) + "% / " + G(m, 2) + " 秒\n友军每秒恢复 " + G(m, 3) + " 生命 / 范围 " + G(m, 4));
            Rule(@"Choose a (Silver|Gold|Prismatic) augment to strengthen your defense\.", m => "选择一个" + (G(m, 1) == "Silver" ? "银色" : G(m, 1) == "Gold" ? "金色" : "彩色") + "海克斯来强化防线。");
            Rule(@"(.+) activated\. The assault has begun!", m => Translate(G(m, 1)) + "已激活。战斗开始！");
            Rule(@"(.+) activated\. The assault continues!", m => Translate(G(m, 1)) + "已激活。战斗继续！");
            Rule(@"Wave (\d+) incoming\. Protect both entrances!", m => "第 " + G(m, 1) + " 波来袭，守住两处入口！");
            Rule(@"Not enough gold\. This tower costs (\d+)\.", m => "金币不足，建造此塔需要 " + G(m, 1) + " 金币。");
            Rule(@"Not enough gold\. This upgrade costs (\d+)\.", m => "金币不足，本次升级需要 " + G(m, 1) + " 金币。");
            Rule(@"(.+) built on (.+)\.", m => Translate(G(m, 1)) + "已建于 " + G(m, 2) + "。");
            Rule(@"Barracks upgraded to level (\d+): (.+)\.", m => "兵营已升至 " + G(m, 1) + " 级：" + Translate(G(m, 2)) + "。");
            Rule(@"(.+) upgraded\. Damage, range and firing speed increased\.", m => Translate(G(m, 1)) + "已升级，攻击力、范围与攻速提升。");
            Rule(@"Need (\d+) mana to cast (Fireball|Frost Nova)\.", m => "释放" + Translate(G(m, 2)) + "需要 " + G(m, 1) + " 魔法值。");
            Rule(@"Fireball cast! Burning ground for ([\d.]+) seconds\.", m => "火球术已释放！地面持续燃烧 " + G(m, 1) + " 秒。");
            Rule(@"Frost Nova cast! Enemies frozen for ([\d.]+) seconds\.", m => "冰霜魔法已释放！敌人冻结 " + G(m, 1) + " 秒。");
            Rule(@"(Fireball|FrostNova|Reinforcements|Battlecry) upgraded to level (\d+)\. (.+)", m => Translate(G(m, 1)) + "已升至 " + G(m, 2) + " 级。" + Translate(G(m, 3)));
            Rule(@"(\d+) reinforcements deployed to the nearby road for ([\d.]+) seconds\.", m => G(m, 1) + " 名援军已部署至附近道路，持续 " + G(m, 2) + " 秒。");
        }
    }
}
