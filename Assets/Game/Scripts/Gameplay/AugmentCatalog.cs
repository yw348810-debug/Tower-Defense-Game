using System;
using System.Collections.Generic;
namespace TowerDefense.Gameplay
{
    /// <summary>Exactly 18 draftable cards per tier. Retired IDs remain compatible.</summary>
    public static class AugmentCatalog
    {
        static AugmentDefinition Card(AugmentId id, AugmentTier tier, string name, string description,
            string chineseName, string chineseDescription, string hint, string chineseHint, bool generic, int level)
            => new AugmentDefinition(id, tier, name, description, generic, level, chineseName, chineseDescription, hint, chineseHint);
        static readonly AugmentDefinition[] definitions =
        {
            Card(AugmentId.LuckyLoot, AugmentTier.Silver, "Lucky Loot", "Kills have a 20% chance to drop 50% extra base gold.", "幸运战利品", "击杀有20%概率额外掉落50%基础金币。", "Economy -> upgrades", "经济 → 升级", true, 1),
            Card(AugmentId.ConstructionGrant, AugmentTier.Silver, "Construction Grant", "Your next 3 tower builds cost 15 less gold each. Upgrades are unaffected.", "建造补助", "接下来建造的3座塔各少花15金币，不影响升级。", "Build -> Combined Arms", "建造 → 协同作战", true, 1),
            Card(AugmentId.ManaScavenger, AugmentTier.Silver, "Mana Scavenger", "Every 8 kills after selection grant 1 extra mana.", "魔力拾荒者", "选取后每击杀8个敌人额外获得1魔法值。", "Kills -> spells", "击杀 → 施法", true, 1),
            Card(AugmentId.LongRange, AugmentTier.Silver, "Long Range", "All towers gain +0.5m range. Spell and explosion radii are unchanged.", "远程火力", "所有塔攻击范围+0.5米，法术与爆炸半径不变。", "Coverage -> Crossfire", "覆盖 → 交叉火力", true, 1),
            Card(AugmentId.LastingFrost, AugmentTier.Silver, "Lasting Frost", "Tower frost slows last 0.75s longer. Repeated hits refresh the duration.", "长效寒霜", "冰霜塔减速延长0.75秒，重复命中刷新持续时间。", "Frost -> shatter / steam", "寒霜 → 碎冰 / 蒸汽", false, 1),
            Card(AugmentId.BlastCalibration, AugmentTier.Silver, "Blast Calibration", "Blast's normal explosions gain +0.25m radius.", "爆破校准", "爆破塔普通爆炸半径+0.25米。", "Explosion -> Aftershock", "爆炸 → 余震", false, 1),
            Card(AugmentId.FocusedFlame, AugmentTier.Silver, "Focused Flame", "Fireball ticks deal +2 damage inside the inner half of the burn radius.", "聚能烈焰", "火球燃烧内侧一半半径中，每次伤害+2。", "Burn -> Cinder Harvest", "燃烧 → 薪火收割", true, 1),
            Card(AugmentId.CombinedArms, AugmentTier.Silver, "Combined Arms", "Own Rapidfire, Frost and Blast together: gain 30 gold and 6 mana, once per run.", "协同作战", "同时拥有速射、寒冰、爆破塔时获得30金币和6魔法值，每局一次。", "Mixed towers -> Crossfire", "混合防线 → 交叉火力", false, 1),
            Card(AugmentId.FastReinforcements, AugmentTier.Silver, "Fast Reinforcements", "Barracks respawn delays are 25% shorter. Existing countdowns are unchanged.", "快速增援", "兵营复活等待缩短25%，已经开始的倒计时不变。", "Recruits -> Phoenix Pact", "新兵 → 凤凰契约", false, 2),
            Card(AugmentId.EmberLedger, AugmentTier.Silver, "Ember Ledger", "Kills inside a Fireball burn grant +1 gold, at most 4 bonus gold per second.", "余烬账本", "燃烧区域内击杀额外获得1金币，每秒最多获得4额外金币。", "Burn -> gold -> upgrades", "燃烧 → 金币 → 升级", true, 1),
            Card(AugmentId.FrostBattery, AugmentTier.Silver, "Frost Battery", "A normal Frost tower impact generates 1 mana. Global cooldown: 4s.", "霜冻电池", "寒冰塔普通炮弹命中获得1魔法值，全局冷却4秒。", "Frost -> Fireball -> steam", "寒霜 → 火球 → 蒸汽", false, 1),
            Card(AugmentId.PowderKeg, AugmentTier.Silver, "Powder Keg", "Every fourth normal Blast impact causes a 1.8m explosion for 35% shot damage. Cannot retrigger.", "火药桶", "爆破塔每第4次普通命中追加1.8米爆炸，造成35%炮弹伤害，不能再次触发。", "Blast -> shatter", "炮击 → 碎冰", false, 1),
            Card(AugmentId.Crossfire, AugmentTier.Silver, "Crossfire", "Two different attacking tower types hitting one enemy within 3s add 12 damage. Per-enemy cooldown: 3s.", "交叉火力", "3秒内两种攻击塔命中同一敌人追加12伤害，每个敌人冷却3秒。", "Mixed towers -> Frost Mark", "混合防线 → 寒霜印记", false, 1),
            Card(AugmentId.HeroicGuard, AugmentTier.Silver, "Heroic Guard", "Barracks heroes heal 5 HP each second while blocking an enemy.", "英雄壁垒", "兵营英雄阻拦敌人期间，每秒回复5生命。", "Hero -> Siege Formation", "英雄阻拦 → 围城阵列", false, 2),
            Card(AugmentId.SpellPrimer, AugmentTier.Silver, "Spell Primer", "Each purchased spell upgrade immediately grants 6 mana.", "法术启蒙", "每次购买法术升级立即获得6魔法值。", "Upgrade -> cast -> refund", "升级 → 施法 → 回流", true, 2),
            Card(AugmentId.PatientAim, AugmentTier.Silver, "Precision Craft", "Level-3 attacking towers gain +15% damage.", "精工准星", "三级攻击塔伤害+15%。", "Level 3 -> Masterworks", "满级 → 杰作共振", true, 1),
            Card(AugmentId.SanctuaryRoots, AugmentTier.Silver, "Sanctuary Roots", "Sanctuary vulnerability lasts +1s and its healing gains +3 HP per second.", "圣所根系", "增益塔易伤持续时间+1秒，每秒治疗量+3生命。", "Vulnerability -> Reactor", "易伤 → 圣所反应堆", false, 3),
            Card(AugmentId.ScorchingEdges, AugmentTier.Silver, "Scorching Edges", "Fireball ticks deal +4 damage to armored enemies. Armor still applies.", "灼甲烈焰", "火球每次对有护甲敌人的伤害+4，护甲仍会结算。", "Fireball -> heavy enemies", "火球 → 重甲敌人", true, 1),
            Card(AugmentId.GunRhythm, AugmentTier.Gold, "Gun Rhythm", "Every third normal Rapidfire shot adds a 60%-damage bullet. Bonus shots cannot trigger themselves.", "射击节奏", "速射塔每第3次普通射击追加60%伤害子弹，额外射击不能再次触发。", "Rapidfire -> piercing", "速射 → 穿透", false, 1),
            Card(AugmentId.FrostMark, AugmentTier.Gold, "Frost Mark", "Slowed or frozen enemies take +30% damage from Rapidfire towers.", "寒霜印记", "被减速或冻结的敌人受到速射塔伤害+30%。", "Frost + Rapidfire", "寒冰 + 速射", false, 1),
            Card(AugmentId.ShatterResonance, AugmentTier.Gold, "Shatter Resonance", "Blast shatter explosions deal +50% damage.", "碎冰共振", "爆破塔碎冰爆炸伤害+50%。", "Frost + Blast", "寒冰 + 爆破", false, 1),
            Card(AugmentId.ExtendedBurn, AugmentTier.Gold, "Extended Burn", "Fireball burns last +1s, adding two normal damage ticks.", "持续燃烧", "火球燃烧延长1秒，追加两次正常伤害。", "Burn -> steam / harvest", "燃烧 → 蒸汽 / 收割", true, 1),
            Card(AugmentId.FireDividend, AugmentTier.Gold, "Fire Dividend", "Every successful paid Fireball grants 8 gold. Free and duplicated casts grant nothing.", "烈焰红利", "每次付费火球获得8金币，免费或复制施法不提供收益。", "Mana -> gold -> spell upgrades", "魔法 → 金币 → 法术升级", true, 1),
            Card(AugmentId.FieldWorkshop, AugmentTier.Gold, "Field Workshop", "Future tower and spell upgrades cost 20% less gold, rounded up.", "战地工坊", "之后防御塔与法术升级费用降低20%，向上取整。", "Economy -> level 3", "经济 → 满级", true, 1),
            Card(AugmentId.BurningMark, AugmentTier.Gold, "Burning Mark", "Enemies inside a Fireball burn take +25% tower damage. Overlapping burns do not stack.", "燃烧印记", "燃烧区域内敌人受到塔伤害+25%，多个区域不叠加。", "Fireball + every tower", "火球 + 全部防御塔", true, 1),
            Card(AugmentId.VeteranTraining, AugmentTier.Gold, "Veteran Training", "All recruits gain +25% maximum HP and damage. Living units keep their health fraction.", "老兵训练", "士兵与英雄生命上限、伤害+25%，存活单位保留当前生命比例。", "Recruits -> healing / block", "新兵 → 治疗 / 阻拦", false, 2),
            Card(AugmentId.SteamCircuit, AugmentTier.Gold, "Steam Circuit", "Fireball hitting frost causes a 24-damage, 2m steam burst and grants 1 mana. Each target: 5s; mana: 1/s.", "蒸汽回路", "火球命中寒霜引发2米、24伤害蒸汽爆炸并获得1魔法值；每目标冷却5秒，魔法回流每秒最多1。", "Frost -> Fireball -> mana", "寒冰 → 火球 → 魔法", true, 1),
            Card(AugmentId.FinishingBlow, AugmentTier.Gold, "Execution Protocol", "Tower hits deal +35% damage to enemies at or below 25% HP.", "处决协议", "塔攻击生命不超过25%的敌人时伤害+35%。", "AOE -> execute -> rewards", "范围伤害 → 处决 → 收益", true, 1),
            Card(AugmentId.ArcaneInvestment, AugmentTier.Gold, "Arcane Investment", "Tower and spell upgrades return 1 mana per 12 gold actually spent, rounded down. At most 12 per purchase.", "奥术投资", "塔与法术升级每实际花费12金币返还1魔法值，向下取整，每次最多返还12。", "Gold -> upgrade -> mana", "金币 → 升级 → 魔法", true, 1),
            Card(AugmentId.UpgradeRelay, AugmentTier.Gold, "Upgrade Relay", "A manual spell grants 2 mana if you own a level-3 tower. Global cooldown: 5s.", "升级接力", "拥有三级塔时，手动施法返还2魔法值，全局冷却5秒。", "Level-3 towers -> spell cycle", "满级塔 → 施法循环", true, 1),
            Card(AugmentId.WarDrums, AugmentTier.Gold, "Echoing War Drums", "Casting Battlecry heals every ally for 40 HP and refunds 6 mana.", "战鼓回响", "释放战吼时为全部友军回复40生命并返还6魔法值。", "Battlecry -> frontline survival", "战吼 → 前线续航", false, 3),
            Card(AugmentId.GuardianBond, AugmentTier.Gold, "Guardian Bond", "Attacking towers within 5m of a living ally deal +20% damage. Allies gain +15% maximum HP.", "守护羁绊", "5米内有存活友军的攻击塔伤害+20%，全部友军生命上限+15%。", "Barracks + ranged towers", "兵营 + 远程塔", false, 2),
            Card(AugmentId.ExplosiveFarewell, AugmentTier.Gold, "Explosive Legacy", "A recruit's combat death explodes for 35 damage in 2.5m. Global cooldown: 2s. Expiry grants nothing.", "爆裂遗志", "友军战斗死亡引发2.5米、35伤害爆炸，全局冷却2秒；到期消失不触发。", "Frontline -> explosive defense", "前线 → 爆炸防线", false, 2),
            Card(AugmentId.ColdFront, AugmentTier.Gold, "Cold Front", "Frost Nova also deals 25 / 40 / 55 damage at spell levels 1 / 2 / 3.", "冷锋冲击", "冰霜魔法同时造成25 / 40 / 55伤害，对应法术1 / 2 / 3级。", "Freeze -> shatter / steam", "冻结 → 碎冰 / 蒸汽", true, 2),
            Card(AugmentId.CinderHarvest, AugmentTier.Gold, "Cinder Harvest", "Kills inside a Fireball burn grant 1 extra mana, at most 3 per second.", "薪火收割", "燃烧区域内击杀额外获得1魔法值，每秒最多3。", "Fireball -> more Fireballs", "火球 → 更多火球", true, 1),
            Card(AugmentId.SiegeFormation, AugmentTier.Gold, "Siege Formation", "Blast hits deal +35% damage to enemies blocked by friendly units.", "围城阵列", "爆破塔对被友军阻拦的敌人伤害+35%。", "Barracks + Blast", "兵营 + 爆破", false, 2),
            Card(AugmentId.UltimateRetrofit, AugmentTier.Prismatic, "Ultimate Retrofit", "Your next tower upgrade is free and jumps to level 3. That tower permanently gains +25% damage.", "终极改造", "下一次塔升级免费并直接升至三级，该塔永久获得25%额外伤害。", "Level 3 -> Masterworks", "满级 → 杰作共振", true, 2),
            Card(AugmentId.PiercingRounds, AugmentTier.Prismatic, "Piercing Rounds", "Normal Rapidfire shots pierce up to 2 enemies behind the target for 40% damage each.", "穿甲弹", "速射塔普通炮弹穿透目标后方最多2名敌人，各造成40%伤害。", "Rapidfire + Frost Mark", "速射 + 寒霜印记", false, 2),
            Card(AugmentId.PolarPulse, AugmentTier.Prismatic, "Polar Pulse", "Frost towers freeze nearby enemies for 1.5s every 6s. Each target: once per 6s. Boss control resistance applies.", "极寒脉冲", "寒冰塔每6秒冻结附近敌人1.5秒；每目标6秒最多一次，首领控制抗性照常结算。", "Freeze -> elemental bursts", "冻结 → 元素爆发", false, 2),
            Card(AugmentId.Aftershock, AugmentTier.Prismatic, "Aftershock", "Blast impacts explode again after 0.6s for 40% shot damage. Secondary blasts cannot retrigger.", "余震", "爆破塔命中0.6秒后追加40%炮弹伤害爆炸，次级爆炸不能再次触发。", "Blast + blocked enemies", "爆破 + 阻拦", false, 2),
            Card(AugmentId.TwinMeteor, AugmentTier.Prismatic, "Twin Meteor", "Each paid Fireball creates a second complete burn after 0.75s for no extra mana. Copies do not trigger cast rewards.", "双重陨火", "付费火球0.75秒后复制一个完整燃烧区域，复制不触发施法收益。", "Fireball + Cinder Harvest", "火球 + 薪火收割", true, 2),
            Card(AugmentId.ManaFurnace, AugmentTier.Prismatic, "Mana Furnace", "Every 15s of combat grants one free Fireball charge. Store at most one.", "魔力熔炉", "每战斗15秒获得一次免费火球充能，最多储存一次。", "Free burn -> kill rewards", "免费燃烧 → 击杀收益", true, 2),
            Card(AugmentId.SecondWind, AugmentTier.Prismatic, "Second Wind", "Once per battle, survive lethal core damage with 8 HP and banish enemies within 6m of the core. No kill rewards.", "绝地逢生", "每局一次：核心致命伤害后保留8生命并清除核心6米内敌人，清除不提供击杀收益。", "Safety -> late-game scaling", "容错 → 后期成型", true, 2),
            Card(AugmentId.ElementalBoil, AugmentTier.Prismatic, "Elemental Boil", "Fireball hits on frost cause a 30-damage, 2.5m steam burst. Each target: 5s. Steam Circuit adds its damage.", "元素沸腾", "火球命中寒霜引发2.5米、30伤害蒸汽爆炸，每目标冷却5秒；蒸汽回路伤害可以叠加。", "Frost + Fireball + Steam Circuit", "寒冰 + 火球 + 蒸汽回路", true, 2),
            Card(AugmentId.VanguardCommand, AugmentTier.Prismatic, "Vanguard Command", "All recruits gain +25% attack and +35% maximum HP, including future summons.", "先锋号令", "全部士兵、英雄攻击+25%，生命上限+35%，之后召唤单位同样有效。", "Army -> Guardian Bond", "军团 → 守护羁绊", false, 2),
            Card(AugmentId.GrandConvergence, AugmentTier.Prismatic, "Triple Convergence", "Fireball hitting a frosted, vulnerable enemy causes a 100-damage, 3m burst. Global cooldown: 4s; each target: 8s.", "三相共鸣", "火球命中同时受寒霜与易伤的敌人，触发3米、100伤害爆炸；全局冷却4秒，每目标8秒。", "Frost + Fireball + Sanctuary", "寒冰 + 火球 + 圣所", false, 3),
            Card(AugmentId.Masterworks, AugmentTier.Prismatic, "Masterwork Resonance", "Every manual spell charges level-3 attacking towers for 6s: +25% attack speed and +20% damage. Refreshes, never stacks.", "杰作共振", "每次手动施法使三级攻击塔6秒内攻速+25%、伤害+20%，重复施法刷新，不叠加。", "Max towers + spell cycle", "满级塔 + 施法循环", true, 2),
            Card(AugmentId.ArchmageCycle, AugmentTier.Prismatic, "Three-Color Cycle", "Cast 3 different spell types within 20s to gain a free Fireball charge and 8 mana. Cooldown: 12s. Copies do not count.", "三色循环", "20秒内释放3种不同法术获得免费火球充能与8魔法值，冷却12秒；复制施法不计入。", "Three spells -> free Fireball", "三种法术 → 免费火球", true, 3),
            Card(AugmentId.EternalLegion, AugmentTier.Prismatic, "Eternal Legion", "Reinforcements last +12s. All living recruits heal 4 HP every combat second.", "不灭军团", "援军持续时间+12秒，所有存活士兵、英雄每战斗秒回复4生命。", "Summons + Sanctuary", "召唤 + 圣所", false, 3),
            Card(AugmentId.SanctuaryReactor, AugmentTier.Prismatic, "Sanctuary Reactor", "Killing a vulnerable enemy grants 2 mana and heals all allies within 5m for 12 HP. Global cooldown: 2s.", "圣所反应堆", "击杀易伤敌人获得2魔法值，并为目标5米内友军回复12生命，全局冷却2秒。", "Vulnerability -> mana + heal", "易伤 → 魔法 + 治疗", false, 3),
            Card(AugmentId.Warpath, AugmentTier.Prismatic, "Warpath", "Battlecry lasts +3s. During Battlecry, level-3 tower hits and all allied attacks deal +35% damage.", "战争路径", "战吼延长3秒，生效期间三级塔与全部友军攻击伤害+35%。", "Battlecry + elite army", "战吼 + 精英军团", false, 3),
            Card(AugmentId.PhoenixPact, AugmentTier.Prismatic, "Phoenix Pact", "A recruit's combat death grants 3 mana and heals other allies within 4m for 20 HP. Global cooldown: 5s. Expiry grants nothing.", "凤凰契约", "友军战斗死亡获得3魔法值，并为4米内其他友军回复20生命，全局冷却5秒；到期消失不触发。", "Army deaths -> summon again", "军团牺牲 → 再次召唤", false, 2),
            Card(AugmentId.GlacialCataclysm, AugmentTier.Prismatic, "Glacial Cataclysm", "Frost Nova also erupts for 60 damage in its radius and refreshes a 15% vulnerability for 4s. Cold Front damage adds.", "冰川灾变", "冰霜魔法追加范围60伤害并施加15%易伤4秒，可叠加冷锋冲击伤害。", "Freeze + vulnerability -> Fireball", "冻结 + 易伤 → 火球", true, 2),
            Card(AugmentId.ArcaneSupremacy, AugmentTier.Prismatic, "Arcane Supremacy", "Level-3 Fireball ticks and Frost Nova bursts deal +30% damage. Every level-3 manual spell heals allies within 5m of its target for 30 HP.", "奥术统治", "三级火球每次伤害、冰霜爆发伤害+30%，三级手动法术为施法点5米内友军回复30生命。", "Level-3 spells + frontline", "满级法术 + 前线", true, 2),
        };
        public static IReadOnlyList<AugmentDefinition> All { get; } = Array.AsReadOnly(definitions);
        public static AugmentDefinition Get(AugmentId id)
        {
            foreach (var definition in definitions) if (definition.Id == id) return definition;
            switch (id)
            {
                case AugmentId.EmergencyShield: return new AugmentDefinition(id, AugmentTier.Silver, "Emergency Shield", "Gain 3 core shield.", true);
                case AugmentId.StoredMana: return new AugmentDefinition(id, AugmentTier.Silver, "Stored Mana", "Gain 12 mana.", true);
                case AugmentId.HeavyHunter: return new AugmentDefinition(id, AugmentTier.Gold, "Heavy Hunter", "Tower damage +30% against Brutes and Breakers.", true);
                case AugmentId.TypeSpecialization: return new AugmentDefinition(id, AugmentTier.Gold, "Type Specialization", "Three towers of one type gain +25% damage.");
                case AugmentId.FieldMedic: return new AugmentDefinition(id, AugmentTier.Gold, "Field Medic", "Every 15 kills heal 1 core HP, up to 6 times.", true);
                case AugmentId.LoneGuardian: return new AugmentDefinition(id, AugmentTier.Prismatic, "Lone Guardian", "Isolated towers gain damage and range.", true, 2);
                case AugmentId.LastStandEngine: return new AugmentDefinition(id, AugmentTier.Prismatic, "Last Stand Engine", "Trade 5 core HP for resources and damage.", true, 2);
                default: throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown augment.");
            }
        }
    }
}

