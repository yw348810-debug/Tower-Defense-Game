using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TowerDefense.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    public static class SecondLevelValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Check { public string name; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public int failures; public string error; public Check[] checks; }
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Private)
            .First(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        static void Fresh(LevelSession s, params AugmentId[] cards)
        {
            s.enableAugments = false; s.RestartLevel();
            foreach (var card in cards) Call(s, "ApplyAugment", card);
            Set(s, "Gold", 10000); Set(s, "Mana", 500);
        }
        static void Tick(LevelSession s, float time) { Set(s, "AssaultTime", time); Call(s, "Update"); }
        static TowerAgent Build(LevelSession s, int pad, TowerKind kind)
        {
            if (!s.TryBuildTower(s.map.buildSlots[pad], kind)) throw new Exception("Cannot build fixture: " + kind);
            return s.GetTower(s.map.buildSlots[pad]);
        }
        static EnemyAgent Enemy(LevelSession s, EnemyKind kind)
        {
            var definitions = (Dictionary<EnemyAgent, EnemyProfile>)typeof(LevelSession).GetField("enemyDefinitions", Private).GetValue(s);
            return s.ActiveEnemies.First(e => definitions[e].kind == kind);
        }
        static void HideEnemies(LevelSession s)
        { foreach (var e in s.ActiveEnemies) e.transform.position = new Vector3(1000, 0, 1000); }

        public static Report Run()
        {
            if (!EditorApplication.isPlaying) throw new Exception("Requires Play Mode in level 2.");
            var s = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            var rows = new List<Check>();
            var report = new Report();
            Action<string, bool> check = (name, passed) => rows.Add(new Check { name = name, passed = passed });
            int seed = s.augmentSeed;
            try
            {
                check("Uses existing second map with eight pads and two entrances", s.gameObject.scene.path == SecondLevelSetup.ScenePath
                    && s.levelIndex == 2 && s.map.buildSlots.Length == 8 && s.map.routes.Length == 2);
                check("Twelve waves, 348 enemies and all six kinds", s.WaveCount == 12 && s.waves.SelectMany(w => w.batches).Sum(b => b.count) == 348
                    && s.waves.SelectMany(w => w.batches).Select(b => b.enemyKind).Distinct().Count() == 6);
                check("Second level configuration is complete", s.GetConfigurationErrors().Count == 0);
                check("Three tiers each have at least ten unique cards", Enum.GetValues(typeof(AugmentTier)).Cast<AugmentTier>()
                    .All(t => AugmentCatalog.All.Count(d => d.Tier == t) >= 10)
                    && AugmentCatalog.All.Select(d => d.Id).Distinct().Count() == AugmentCatalog.All.Count);
                check("Second-level navigation resolves its configured next map", string.IsNullOrEmpty(s.nextLevelScene)
                    || s.HasNextLevel);
                bool offers = true, pauses = true;
                for (int i = 0; i < 24; i++)
                {
                    s.enableAugments = true; s.augmentSeed = 27000 + i; s.RestartLevel();
                    for (int tier = 0; tier < 3; tier++)
                    {
                        if (tier > 0) Tick(s, tier == 1 ? 96f : 192f);
                        offers &= s.IsChoosingAugment && s.AugmentOffers.Count == 3
                            && s.AugmentOffers.All(d => d.Tier == (AugmentTier)tier)
                            && s.AugmentOffers.Select(d => d.Id).Distinct().Count() == 3
                            && s.AugmentOffers.Any(d => d.IsGeneric);
                        pauses &= Time.timeScale == 0 && !s.IsCombatRunning;
                        s.TryChooseAugment(0);
                    }
                    offers &= s.SelectedAugments.Count == 3;
                }
                check("24 seeds produce three distinct eligible offers at every tier", offers);
                check("All three selections pause combat", pauses);

                Fresh(s);
                var tower = Build(s, 0, TowerKind.Barracks); var b = tower.Barracks;
                check("Level 1 barracks create exactly two soldiers", b.AliveSoldierCount == 2 && b.AliveHeroCount == 0 && b.Troops.Count == 2);
                check("Formation retains separate road positions", Vector3.Distance(b.Troops[0].RallyPosition, b.Troops[1].RallyPosition) > .5f);
                var recruit = b.Troops[0]; recruit.TakeDamage(45);
                s.TryUpgradeTower(tower.Slot);
                check("Level 2 creates three stronger soldiers and preserves health fraction", b.AliveSoldierCount == 3
                    && recruit.MaxHealth == 140 && recruit.CurrentHealth == 70 && recruit.Damage == 18);
                s.TryUpgradeTower(tower.Slot);
                check("Level 3 retains three soldiers and adds one stronger hero", b.AliveSoldierCount == 3 && b.AliveHeroCount == 1
                    && b.Troops.Single(t => t.IsHero).MaxHealth == 420 && b.Troops.Single(t => t.IsHero).Damage == 40);
                check("Soldier and hero use different model assets", b.soldierPrefab != b.heroPrefab
                    && b.soldierPrefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh
                    != b.heroPrefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh);
                Tick(s, 96f); HideEnemies(s);
                var normal = Enemy(s, EnemyKind.Normal); var flying = Enemy(s, EnemyKind.Flying);
                normal.transform.position = recruit.transform.position + Vector3.right * .5f;
                check("Ground enemy accepts one exclusive blocker", normal.TryAssignBlocker(recruit)
                    && normal.Blocker == recruit && !normal.TryAssignBlocker(b.Troops.First(t => t != recruit)));
                check("Flying enemies bypass barracks blockers", flying.IsUnblockable && !flying.TryAssignBlocker(recruit));
                int hp = normal.CurrentHealth;
                s.ApplySoldierHit(recruit, normal, recruit.Damage);
                check("Soldier attack damages its blocked enemy", normal.CurrentHealth == hp - 18);
                recruit.TakeDamage(100000);
                check("Soldier death releases enemy and starts one respawn slot", normal.Blocker == null && b.AliveSoldierCount == 2 && b.Troops.Count == 3);
                var slots = (System.Collections.IEnumerable)typeof(BarracksAgent).GetField("slots", Private).GetValue(b);
                float respawn = 0;
                foreach (var slot in slots)
                {
                    var f = slot.GetType().GetField("respawn"); float value = (float)f.GetValue(slot);
                    if (value <= 0) continue; respawn = value; f.SetValue(slot, 0f);
                }
                check("Soldier respawn delay is eight seconds", Mathf.Approximately(respawn, 8f));
                Call(b, "SpawnReadySlots"); Call(b, "SpawnReadySlots");
                check("Respawn restores capacity without duplicates", b.AliveSoldierCount == 3 && b.AliveHeroCount == 1 && b.Troops.Count == 4);
                var hero = b.Troops.Single(t => t.IsHero); hero.TakeDamage(99999);
                float heroDelay = 0;
                foreach (var slot in slots)
                    if ((bool)slot.GetType().GetField("hero").GetValue(slot)) heroDelay = (float)slot.GetType().GetField("respawn").GetValue(slot);
                check("Hero respawn delay is fifteen seconds", Mathf.Approximately(heroDelay, 15f));
                Call(s, "ApplyAugment", AugmentId.FastReinforcements);
                Call(s, "ApplyAugment", AugmentId.VeteranTraining);
                Call(s, "ApplyAugment", AugmentId.VanguardCommand);
                check("Barracks cards stack on living and future troops", b.SoldierHealth == 224 && b.SoldierDamage == 27
                    && b.Troops.First(t => !t.IsHero).MaxHealth == 224 && Mathf.Approximately(b.RespawnSeconds, 6f));
                var freshTroop = b.Troops.First(t => !t.IsHero); freshTroop.TakeDamage(99999);
                float futureDelay = 0;
                foreach (var slot in slots)
                    if (!(bool)slot.GetType().GetField("hero").GetValue(slot)) futureDelay = Mathf.Max(futureDelay, (float)slot.GetType().GetField("respawn").GetValue(slot));
                check("Reinforcements shorten future deaths to six seconds", Mathf.Approximately(futureDelay, 6f));

                Fresh(s); Tick(s, 0); HideEnemies(s); normal = s.ActiveEnemies[0];
                normal.transform.position = Vector3.zero;
                Set(s, "Mana", 18);
                check("Frost spell spends eighteen mana and hard freezes", s.TryCastFrostNova(Vector3.zero) && normal.IsFrozen && s.Mana == 0 && s.FrostCasts == 1);
                Vector3 frozenPosition = normal.transform.position; Call(normal, "Update");
                check("Frozen enemy cannot move", normal.transform.position == frozenPosition);
                Set(s, "Mana", 500);
                check("Frost cooldown blocks immediate recast", !s.TryCastFrostNova(Vector3.zero));
                float cooldown = s.FrostSpellCooldownRemaining;
                s.TogglePause(); Call(s, "Update");
                check("Pause stops cooldown and rejects spells", Mathf.Approximately(cooldown, s.FrostSpellCooldownRemaining)
                    && !s.TryCastFrostNova(Vector3.zero) && !s.TryCastFireball(Vector3.zero));
                s.TogglePause(); Call(s, "UpdateAdvancedCombat", 10f);
                check("Frost cooldown recovers after ten combat seconds", s.TryCastFrostNova(Vector3.zero));

                Fresh(s, AugmentId.UltimateRetrofit);
                tower = Build(s, 0, TowerKind.Gun); int gold = s.Gold;
                check("Ultimate Retrofit jumps to level three once at zero cost", tower.UpgradeCost == 0
                    && s.TryUpgradeTower(tower.Slot) && tower.Level == 3 && s.Gold == gold && !s.Augments.RetrofitPending);
                Tick(s, 0); normal = s.ActiveEnemies[0];
                int modified = (int)Call(s, "ComputeTowerHitDamage", normal, 20, TowerKind.Gun, false, tower);
                check("Retrofitted tower gains permanent twenty-five percent damage", modified == 25);

                Fresh(s, AugmentId.LastStandEngine);
                check("Last Stand trades current and maximum health without mutating map", s.MaxBaseHealth == 15 && s.BaseHealth == 15 && s.map.baseHealth == 20);
                Tick(s, 0); normal = s.ActiveEnemies[0];
                check("Last Stand increases tower damage", (int)Call(s, "ComputeTowerHitDamage", normal, 20, TowerKind.Gun, false, null) == 25);

                Fresh(s, AugmentId.LoneGuardian); tower = Build(s, 0, TowerKind.Gun);
                float range = tower.range;
                check("Isolated tower receives extra range", Mathf.Approximately(tower.EffectiveRange, range + 1f));
                int nearby = Array.FindIndex(s.map.buildSlots, p => p != tower.Slot && Vector3.Distance(p.transform.position, tower.transform.position) < 10f);
                Build(s, nearby, TowerKind.Frost);
                check("Nearby tower immediately removes isolation bonus", Mathf.Approximately(tower.EffectiveRange, range));

                Fresh(s, AugmentId.ManaFurnace, AugmentId.TwinMeteor, AugmentId.FireDividend);
                Call(s, "UpdateAdvancedCombat", 15f); Set(s, "Mana", 0); gold = s.Gold;
                check("Furnace grants manual free fireball at zero mana", s.FreeFireballReady && s.TryCastFireball(Vector3.zero) && s.FreeFireballs == 1 && s.Mana == 0);
                Call(s, "UpdateAdvancedCombat", 1f);
                check("Free casts create no dividend or second meteor", s.Gold == gold && s.ExtraMeteors == 0);
                Set(s, "Mana", 12); gold = s.Gold; s.TryCastFireball(Vector3.zero);
                Call(s, "UpdateAdvancedCombat", 1f);
                check("Paid fireball creates exactly one extra meteor and dividend", s.Mana == 0 && s.ExtraMeteors == 1 && s.Gold == gold + 8 && s.PaidFireballs == 1);
                Call(s, "UpdateAdvancedCombat", 1f);
                check("Duplicate meteor cannot recursively duplicate itself", s.ExtraMeteors == 1);

                Fresh(s, AugmentId.ElementalBoil); Tick(s, 72f); HideEnemies(s);
                normal = Enemy(s, EnemyKind.Heavy); normal.transform.position = Vector3.zero; normal.DamageReduction = 0;
                normal.ApplyFrost(.6f, 3); normal.Freeze(3);
                Call(s, "ResolveFireballTick", Vector3.zero, 3.2f, 10);
                int afterSteam = normal.CurrentHealth;
                Call(s, "ResolveFireballTick", Vector3.zero, 3.2f, 10);
                check("Steam burst triggers once per enemy cooldown without recursion", s.SteamBursts == 1 && normal.CurrentHealth == afterSteam - 10);

                Fresh(s, AugmentId.PolarPulse); tower = Build(s, 0, TowerKind.Frost); Tick(s, 0); HideEnemies(s);
                normal = s.ActiveEnemies[0]; normal.transform.position = tower.transform.position;
                s.TryPolarPulse(tower); s.TryPolarPulse(tower);
                check("Polar pulses freeze once within per-enemy immunity", normal.IsFrozen && s.PolarPulses == 1);

                Fresh(s, AugmentId.PiercingRounds); Tick(s, 5); HideEnemies(s);
                var three = s.ActiveEnemies.Take(3).ToArray();
                for (int i = 0; i < 3; i++) three[i].transform.position = Vector3.right * i;
                int behindHealth = three[1].CurrentHealth;
                Call(s, "ResolveShot", three[0], new ShotSpec { kind = TowerKind.Gun, damage = 10, direction = Vector3.right });
                check("Piercing rounds damage two enemies behind target", three[1].CurrentHealth == behindHealth - 4 && three[2].CurrentHealth == behindHealth - 4);
                Fresh(s, AugmentId.Aftershock); Tick(s, 72f); HideEnemies(s);
                normal = Enemy(s, EnemyKind.Heavy); normal.transform.position = Vector3.zero; normal.DamageReduction = 0;
                hp = normal.CurrentHealth;
                Call(s, "ResolveShot", normal, new ShotSpec { kind = TowerKind.Cannon, damage = 10, splashRadius = 2.4f });
                Call(s, "UpdateAdvancedCombat", .6f); Call(s, "UpdateAdvancedCombat", 1f);
                check("Aftershock causes one delayed forty-percent explosion", normal.CurrentHealth == hp - 14 && s.ShattersTriggered == 0);

                Fresh(s, AugmentId.SecondWind); Tick(s, 0); HideEnemies(s);
                var end = s.map.routes[0].waypoints.Last().position;
                normal = s.ActiveEnemies[0]; normal.transform.position = end;
                s.ActiveEnemies[1].transform.position = end;
                Set(s, "BaseHealth", 1); gold = s.Gold; int kills = s.Kills; int mana = s.Mana;
                Call(normal, "Resolve", false);
                check("Second Wind prevents lethal damage and purges without rewards", s.IsCombatRunning
                    && s.BaseHealth == 8 && s.SecondWindUsed && s.Gold == gold && s.Mana == mana && s.Kills == kills
                    && s.ActiveEnemies.Count == 0);
                Tick(s, 5); normal = s.ActiveEnemies[0]; Set(s, "BaseHealth", 1);
                Call(normal, "Resolve", false);
                check("Second Wind cannot revive twice", s.Phase == SessionPhase.Defeat && s.BaseHealth == 0);
            }
            catch (Exception e) { report.error = e.ToString(); }
            finally
            {
                s.augmentSeed = seed; s.enableAugments = true; s.RestartLevel();
                check("Restart clears run-local advanced effects", s.MaxBaseHealth == 20 && !s.FreeFireballReady
                    && !s.Augments.RetrofitPending && !s.SecondWindUsed && s.BuiltTowerCount == 0 && s.SelectedAugments.Count == 0);
                report.checks = rows.ToArray(); report.failures = rows.Count(r => !r.passed) + (string.IsNullOrEmpty(report.error) ? 0 : 1);
                report.passed = report.failures == 0;
                Directory.CreateDirectory("Captures/SecondLevelGameplay");
                File.WriteAllText("Captures/SecondLevelGameplay/boundary-validation.json", JsonUtility.ToJson(report, true));
            }
            return report;
        }
    }
}
