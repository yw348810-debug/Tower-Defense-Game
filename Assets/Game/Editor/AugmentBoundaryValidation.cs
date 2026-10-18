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
    /// <summary>Deterministic effect fixtures plus real opening/midpoint choice lifecycle checks.</summary>
    public static class AugmentBoundaryValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Serializable]
        public sealed class CheckResult
        {
            public string name;
            public bool passed;
            public string detail;
        }

        [Serializable]
        public sealed class Report
        {
            public bool passed;
            public int failures;
            public string error;
            public CheckResult[] checks;
        }

        static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);

        static void Set(object target, string property, object value) =>
            target.GetType().GetProperty(property).SetValue(target, value);

        static void Tick(LevelSession session, float at)
        {
            Set(session, "AssaultTime", at);
            Call(session, "Update");
        }

        // Disable the random choice gate only within effect fixtures. Use the real application hook,
        // so immediate grants and trigger counters still follow the production implementation.
        static void Fresh(LevelSession session, params AugmentId[] cards)
        {
            session.enableAugments = false;
            session.RestartLevel();
            foreach (AugmentId card in cards) Call(session, "ApplyAugment", card);
        }

        static EnemyProfile Profile(LevelSession session, EnemyKind kind) =>
            session.enemyProfiles.First(p => p.kind == kind);

        static EnemyAgent FindEnemy(LevelSession session, EnemyKind kind)
        {
            var definitions = (Dictionary<EnemyAgent, EnemyProfile>)typeof(LevelSession)
                .GetField("enemyDefinitions", Private).GetValue(session);
            return session.ActiveEnemies.First(e => definitions[e].kind == kind);
        }

        static void PlaceOnly(LevelSession session, EnemyAgent target, Vector3 point)
        {
            foreach (EnemyAgent enemy in session.ActiveEnemies)
                enemy.transform.position = enemy == target ? point : new Vector3(100, 0, 100);
        }

        static int Armored(int rawDamage, EnemyAgent enemy) =>
            Mathf.Max(1, Mathf.RoundToInt(rawDamage * (1f - enemy.DamageReduction)));

        static ShotSpec Gun(int damage = 10) => new ShotSpec
            { kind = TowerKind.Gun, damage = damage, color = Color.yellow };

        static void Hit(LevelSession session, EnemyAgent target, ShotSpec shot) =>
            Call(session, "ResolveShot", target, shot);

        static TowerAgent Build(LevelSession session, int pad, TowerKind kind)
        {
            if (!session.TryBuildTower(session.map.buildSlots[pad], kind))
                throw new InvalidOperationException("Fixture could not build " + kind + " on pad " + pad);
            return session.GetTower(session.map.buildSlots[pad]);
        }

        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Requires Play Mode.");
            var session = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            if (!session) throw new InvalidOperationException("No LevelSession in the active scene.");
            var rows = new List<CheckResult>();
            var report = new Report();
            int originalSeed = session.augmentSeed;
            Action<string, bool, string> check = (name, passed, detail) => rows.Add(new CheckResult
                { name = name, passed = passed, detail = detail });
            try
            {
                var firstLevelPool = AugmentCatalog.All.Where(d => d.MinimumLevel <= 1).ToArray();
                check("First-level pools contain ten unique cards per tier",
                    firstLevelPool.Count(d => d.Tier == AugmentTier.Silver) == 10
                    && firstLevelPool.Count(d => d.Tier == AugmentTier.Gold) == 10
                    && firstLevelPool.All(d => d.Tier != AugmentTier.Prismatic)
                    && AugmentCatalog.All.Select(d => d.Id).Distinct().Count() == AugmentCatalog.All.Count,
                    "20 first-level definitions; barracks cards are reserved for later levels.");
                session.enableAugments = true;
                bool offersValid = true;
                for (int run = 0; run < 32; run++)
                {
                    session.augmentSeed = 13000 + run;
                    session.RestartLevel();
                    offersValid &= session.AugmentOffers.Count == 3
                        && session.AugmentOffers.All(d => d.Tier == AugmentTier.Silver
                            && d.Id != AugmentId.FastReinforcements && d.Id != AugmentId.VeteranTraining)
                        && session.AugmentOffers.Select(d => d.Id).Distinct().Count() == 3
                        && session.AugmentOffers.Any(d => d.IsGeneric);
                }
                check("Opening offers are unique silver choices with a generic option", offersValid,
                    "Validated 32 opening selections; no mixed tier or prismatic offer.");
                check("Opening waits before spawning or advancing combat",
                    session.IsChoosingAugment && !session.IsCombatRunning && session.ActiveEnemies.Count == 0
                    && session.AssaultTime == 0 && Time.timeScale == 0 && session.SelectedAugments.Count == 0,
                    "Choice overlay owns a completely idle battle.");
                Set(session, "Mana", session.FireballCost);
                int openingGold = session.Gold;
                bool buildBlocked = !session.TryBuildTower(session.map.buildSlots[0], TowerKind.Gun);
                bool spellBlocked = !session.TryCastFireball(Vector3.zero);
                session.TogglePause(); session.ToggleSpeed(); Call(session, "Update");
                check("Opening gates building, spells, pause and speed", buildBlocked && spellBlocked
                    && session.Gold == openingGold && session.Mana == session.FireballCost
                    && session.GameSpeed == 1 && Time.timeScale == 0 && session.AssaultTime == 0,
                    "Affordability is supplied deliberately; the choice gate rejects the actions.");
                Set(session, "Mana", 0);
                check("Invalid choice indices cannot start combat", !session.TryChooseAugment(-1)
                    && !session.TryChooseAugment(3) && session.IsChoosingAugment,
                    "The selection remains pending after invalid input.");
                bool chosen = session.TryChooseAugment(0);
                check("One valid opening choice starts combat once", chosen && session.SelectedAugments.Count == 1
                    && session.IsCombatRunning && !session.TryChooseAugment(0) && Time.timeScale == 1,
                    "An extra click cannot grant a second opening card.");
                session.augmentSeed = 1729;

                Fresh(session, AugmentId.StoredMana);
                Call(session, "ApplyAugment", AugmentId.StoredMana);
                check("Stored Mana grants twelve mana only once", session.Mana == 12
                    && session.SelectedAugments.Count == 1, "Duplicate activation cannot repeat an immediate grant.");

                Fresh(session, AugmentId.ConstructionGrant); Set(session, "Gold", 5000);
                int subsidizedPaid = 0;
                for (int pad = 0; pad < 3; pad++)
                {
                    int before = session.Gold;
                    Build(session, pad, TowerKind.Gun);
                    subsidizedPaid += before - session.Gold;
                }
                int fourthBefore = session.Gold;
                Build(session, 3, TowerKind.Gun);
                check("Construction grant discounts exactly three future builds",
                    subsidizedPaid == (session.towerCosts[(int)TowerKind.Gun] - 15) * 3
                    && fourthBefore - session.Gold == session.towerCosts[(int)TowerKind.Gun]
                    && session.Augments.SubsidizedBuildsRemaining == 0,
                    "The fourth build pays full cost; discounts do not refund prior purchases.");

                Fresh(session); Set(session, "Gold", 5000);
                TowerAgent existing = Build(session, 0, TowerKind.Gun);
                float prefabRange = session.towerPrefabs[(int)TowerKind.Gun].range;
                float oldRange = existing.EffectiveRange;
                Call(session, "ApplyAugment", AugmentId.LongRange);
                TowerAgent future = Build(session, 1, TowerKind.Gun);
                check("Long Range applies to existing and future towers without mutating prefabs",
                    Mathf.Approximately(existing.EffectiveRange, oldRange + .5f)
                    && Mathf.Approximately(future.EffectiveRange, prefabRange + .5f)
                    && Mathf.Approximately(session.towerPrefabs[(int)TowerKind.Gun].range, prefabRange),
                    "Runtime effective range receives +0.5m; shared serialized range stays unchanged.");

                Fresh(session, AugmentId.LastingFrost, AugmentId.BlastCalibration); Set(session, "Gold", 5000);
                TowerAgent frostTower = Build(session, 0, TowerKind.Frost);
                TowerAgent blastTower = Build(session, 1, TowerKind.Cannon);
                ShotSpec frost = frostTower.GetShotSpec(); ShotSpec blast = blastTower.GetShotSpec();
                check("Frost duration and ordinary Blast radius use their separate modifiers",
                    Mathf.Approximately(frost.slowDuration, 3.25f) && Mathf.Approximately(blast.splashRadius, 2.65f)
                    && Mathf.Approximately(blast.shatterRadius, 3),
                    "The Blast calibration does not increase shatter radius.");
                Tick(session, 0); EnemyAgent first = session.ActiveEnemies[0];
                Hit(session, first, frost); Hit(session, first, frost);
                float slow = (float)typeof(EnemyAgent).GetField("frostMultiplier", Private).GetValue(first);
                float remaining = (float)typeof(EnemyAgent).GetField("frostRemaining", Private).GetValue(first);
                check("Repeated Frost refreshes the augmented duration without stacking slow",
                    first.IsFrosted && Mathf.Approximately(slow, .6f) && Mathf.Approximately(remaining, 3.25f),
                    "Two hits retain the same movement multiplier and refresh to 3.25 seconds.");

                Fresh(session, AugmentId.EmergencyShield); Tick(session, 0);
                first = session.ActiveEnemies[0]; int health = session.BaseHealth;
                int leak = first.LeakDamage; Call(first, "Resolve", false);
                check("Shield absorbs leaks before core health and leaks give no rewards",
                    session.BaseShield == Mathf.Max(0, 3 - leak)
                    && session.BaseHealth == health - Mathf.Max(0, leak - 3)
                    && session.Mana == 0 && session.Kills == 0 && session.Leaks == 1,
                    "Leak damage is spent once against shield, then health.");

                Fresh(session, AugmentId.ManaScavenger); Tick(session, 1000);
                var eight = session.ActiveEnemies.Take(8).ToArray();
                var enemyDefinitions = (Dictionary<EnemyAgent, EnemyProfile>)typeof(LevelSession)
                    .GetField("enemyDefinitions", Private).GetValue(session);
                int expectedMana = eight.Sum(e => enemyDefinitions[e].manaReward) + 1;
                foreach (EnemyAgent enemy in eight) enemy.TakeDamage(100000);
                check("Mana Scavenger triggers on the eighth kill", session.Kills == 8 && session.Mana == expectedMana,
                    "The extra mana is added to each profile's ordinary mana drop.");

                Fresh(session, AugmentId.LuckyLoot); Tick(session, 1000);
                enemyDefinitions = (Dictionary<EnemyAgent, EnemyProfile>)typeof(LevelSession)
                    .GetField("enemyDefinitions", Private).GetValue(session);
                bool validDrops = true; int bonusDrops = 0; int normalMana = 0;
                foreach (EnemyAgent enemy in session.ActiveEnemies.Take(100).ToArray())
                {
                    int before = session.Gold; int reward = enemy.Reward;
                    normalMana += enemyDefinitions[enemy].manaReward;
                    enemy.TakeDamage(100000);
                    int received = session.Gold - before;
                    int bonus = Mathf.RoundToInt(reward * .5f);
                    validDrops &= received == reward || received == reward + bonus;
                    if (received > reward) bonusDrops++;
                    int paid = session.Gold;
                    enemy.TakeDamage(100000);
                    validDrops &= session.Gold == paid;
                }
                check("Lucky Loot modifies only one base gold reward per kill", validDrops && bonusDrops > 0 && bonusDrops < 100 && session.Kills == 100
                    && session.Mana == normalMana,
                    "100 seeded kill resolutions: each reward is base or base+50%; mana is unchanged; bonus drops=" + bonusDrops);

                Fresh(session, AugmentId.CombinedArms); Set(session, "Gold", 5000);
                Build(session, 0, TowerKind.Gun); Build(session, 1, TowerKind.Frost);
                int goldBefore = session.Gold; int manaBefore = session.Mana;
                Build(session, 2, TowerKind.Cannon);
                check("Combined Arms grants its reward when the third type is built",
                    session.Gold == goldBefore - session.towerCosts[(int)TowerKind.Cannon] + 30
                    && session.Mana == manaBefore + 6 && session.Augments.CombinedArmsClaimed,
                    "Exactly 30 gold and 6 mana after the full roster exists.");
                manaBefore = session.Mana; goldBefore = session.Gold;
                Build(session, 3, TowerKind.Gun);
                check("Combined Arms cannot repeat on another build", session.Mana == manaBefore
                    && session.Gold == goldBefore - session.towerCosts[(int)TowerKind.Gun], "The roster reward is run-local and one-shot.");

                Fresh(session, AugmentId.FocusedFlame); Tick(session, 1000);
                var normals = session.ActiveEnemies.Where(e => e.MaxHealth == Profile(session, EnemyKind.Normal).health).Take(3).ToArray();
                foreach (EnemyAgent enemy in session.ActiveEnemies) enemy.transform.position = new Vector3(100, 0, 100);
                normals[0].transform.position = Vector3.zero;
                normals[1].transform.position = Vector3.right * (session.FireballRadius * .75f);
                normals[2].transform.position = Vector3.right * (session.FireballRadius + 1);
                int innerHP = normals[0].CurrentHealth, outerHP = normals[1].CurrentHealth, outsideHP = normals[2].CurrentHealth;
                Call(session, "ResolveFireballTick", Vector3.zero, session.FireballRadius, session.fireballTickDamage);
                check("Focused Flame rewards precise inner-half placement",
                    normals[0].CurrentHealth == innerHP - Armored(session.fireballTickDamage + 2, normals[0])
                    && normals[1].CurrentHealth == outerHP - Armored(session.fireballTickDamage, normals[1])
                    && normals[2].CurrentHealth == outsideHP,
                    "Inner, outer and outside targets receive the expected distinct damage.");

                Fresh(session, AugmentId.HeavyHunter); Tick(session, 1000);
                EnemyAgent heavy = FindEnemy(session, EnemyKind.Heavy); int heavyHP = heavy.CurrentHealth;
                Hit(session, heavy, Gun());
                check("Heavy Hunter boosts tower damage before heavy armor",
                    heavy.CurrentHealth == heavyHP - Armored(13, heavy), "+30% tower damage still passes through the profile's reduction.");
                first = FindEnemy(session, EnemyKind.Normal); int firstHP = first.CurrentHealth;
                Hit(session, first, Gun());
                check("Heavy Hunter does not boost ordinary targets", first.CurrentHealth == firstHP - Armored(10, first),
                    "The conditional modifier is limited to Heavy profiles.");

                Fresh(session, AugmentId.GunRhythm); Set(session, "Gold", 5000);
                TowerAgent rhythmic = Build(session, 0, TowerKind.Gun); Tick(session, 0);
                first = session.ActiveEnemies[0]; PlaceOnly(session, first, rhythmic.transform.position + Vector3.forward);
                for (int shotIndex = 0; shotIndex < 3; shotIndex++)
                {
                    typeof(TowerAgent).GetField("cooldown", Private).SetValue(rhythmic, 0f);
                    Call(rhythmic, "Update");
                }
                var shotSpecs = session.GetComponentsInChildren<ProjectileAgent>()
                    .Select(p => (ShotSpec)typeof(ProjectileAgent).GetField("shot", Private).GetValue(p)).ToArray();
                check("Gun Rhythm adds one 60% bullet after three ordinary shots",
                    session.ShotsFired == 4 && shotSpecs.Count(p => p.damage == rhythmic.damage) == 3
                    && shotSpecs.Count(p => p.damage == Mathf.RoundToInt(rhythmic.damage * .6f)) == 1,
                    "The echo does not advance or recursively trigger the ordinary-shot counter.");
                typeof(TowerAgent).GetField("rhythmShotCount", Private).SetValue(rhythmic, 2);
                rhythmic.gameObject.SetActive(false);
                check("Rhythm counter resets when a tower is disabled",
                    (int)typeof(TowerAgent).GetField("rhythmShotCount", Private).GetValue(rhythmic) == 0,
                    "A recycled or restarted actor cannot inherit a pending echo.");

                Fresh(session, AugmentId.FrostMark); Tick(session, 1000);
                first = FindEnemy(session, EnemyKind.Normal); first.ApplyFrost(.6f, 2.5f); firstHP = first.CurrentHealth;
                Hit(session, first, Gun());
                check("Frost Mark boosts Gun attacks against slowed targets",
                    first.CurrentHealth == firstHP - Armored(13, first) && first.IsFrosted,
                    "Gun damage receives +30% without consuming Frost.");
                firstHP = first.CurrentHealth;
                Hit(session, first, new ShotSpec { kind = TowerKind.Frost, damage = 6, slowMultiplier = .6f, slowDuration = 2.5f });
                check("Frost Mark does not boost Frost's own damage", first.CurrentHealth == firstHP - Armored(6, first),
                    "The vulnerability belongs specifically to Gun attacks.");

                Fresh(session, AugmentId.ShatterResonance); Tick(session, 1000);
                heavy = FindEnemy(session, EnemyKind.Heavy); PlaceOnly(session, heavy, Vector3.zero);
                heavy.ApplyFrost(.6f, 2.5f); heavyHP = heavy.CurrentHealth;
                blast = session.towerPrefabs[(int)TowerKind.Cannon].GetShotSpec(); Hit(session, heavy, blast);
                check("Shatter Resonance boosts only the one shatter explosion",
                    heavy.CurrentHealth == heavyHP - Armored(blast.damage, heavy)
                        - Armored(Mathf.RoundToInt(blast.shatterDamage * 1.5f), heavy)
                    && session.ShattersTriggered == 1 && !heavy.IsFrosted,
                    "Normal Blast damage is unchanged; the direct target consumes Frost once.");

                Fresh(session, AugmentId.ExtendedBurn); Tick(session, 1000);
                heavy = FindEnemy(session, EnemyKind.Heavy); PlaceOnly(session, heavy, Vector3.zero);
                Set(session, "Mana", 24); bool cast = session.TryCastFireball(Vector3.zero);
                FireballArea spell = session.GetComponentsInChildren<FireballArea>().First(); heavyHP = heavy.CurrentHealth;
                spell.Advance(.249f);
                check("Falling Fireball is excluded from Burning Mark queries", cast && !spell.IsBurning
                    && !spell.ContainsBurningPoint(Vector3.zero) && heavy.CurrentHealth == heavyHP,
                    "The falling visual does not create a burn vulnerability.");
                spell.Advance(.002f);
                check("Burning point containment is planar and bounded", spell.IsBurning
                    && spell.ContainsBurningPoint(Vector3.up * 20)
                    && !spell.ContainsBurningPoint(Vector3.right * (session.FireballRadius + .1f)),
                    "Vertical position does not change the circular XZ coverage.");
                spell.Advance(4f);
                check("Extended Burn deals eight ticks and returns to its pool",
                    session.SpellDamageTicks == 8 && spell.TicksApplied == 8 && session.ActiveSpellCount == 0
                    && !spell.IsBurning && !spell.ContainsBurningPoint(Vector3.zero)
                    && heavy.CurrentHealth == heavyHP - Armored(session.fireballTickDamage, heavy) * 8,
                    "Four seconds preserve the configured half-second tick interval.");

                Fresh(session, AugmentId.FireDividend); Set(session, "Mana", 24); goldBefore = session.Gold;
                bool invalidCast = session.TryCastFireball(new Vector3(100, 0, 100));
                bool paidCast = session.TryCastFireball(Vector3.zero);
                check("Fire Dividend pays only a successful paid cast", !invalidCast && paidCast
                    && session.Gold == goldBefore + 8 && session.Mana == 24 - session.FireballCost && session.SpellsCast == 1,
                    "An invalid location grants neither gold nor a spell and does not spend mana.");

                Fresh(session); Set(session, "Gold", 5000); TowerAgent workshop = Build(session, 0, TowerKind.Gun);
                int spentBefore = session.Gold; Call(session, "ApplyAugment", AugmentId.FieldWorkshop);
                int discountedCost = Mathf.CeilToInt(workshop.upgradeCost * .8f); int manaAtUpgrade = session.Mana;
                bool upgraded = session.TryUpgradeTower(session.map.buildSlots[0]);
                check("Field Workshop discounts future upgrades without retrospective refunds", upgraded
                    && workshop.Level == 2 && session.Gold == spentBefore - discountedCost && session.Mana == manaAtUpgrade
                    && workshop.UpgradeCost == Mathf.CeilToInt(workshop.secondUpgradeCost * .8f),
                    "Both upgrade stages use ceiling-rounded gold fees; mana is unchanged.");
                upgraded = session.TryUpgradeTower(session.map.buildSlots[0]);
                goldBefore = session.Gold;
                check("Two upgrades remain the cap with Workshop", upgraded && workshop.Level == 3
                    && workshop.UpgradeCost == 0 && !session.TryUpgradeTower(session.map.buildSlots[0]) && session.Gold == goldBefore,
                    "The discount never introduces a fourth tower level or extra payment.");

                Fresh(session, AugmentId.TypeSpecialization); Set(session, "Gold", 5000);
                Build(session, 0, TowerKind.Gun); Build(session, 1, TowerKind.Gun); Tick(session, 1000);
                heavy = FindEnemy(session, EnemyKind.Heavy); heavyHP = heavy.CurrentHealth; Hit(session, heavy, Gun());
                check("Type Specialization stays inactive below three towers",
                    heavy.CurrentHealth == heavyHP - Armored(10, heavy), "Two towers do not satisfy the threshold.");
                Build(session, 2, TowerKind.Gun); heavyHP = heavy.CurrentHealth; Hit(session, heavy, Gun());
                check("Type Specialization boosts the qualifying type at three towers",
                    heavy.CurrentHealth == heavyHP - Armored(Mathf.RoundToInt(10 * 1.25f), heavy), "Three Gun towers unlock +25% Gun damage.");
                heavyHP = heavy.CurrentHealth;
                Hit(session, heavy, new ShotSpec { kind = TowerKind.Frost, damage = 6, slowMultiplier = .6f, slowDuration = 2.5f });
                check("Type Specialization does not boost other tower types", heavy.CurrentHealth == heavyHP - Armored(6, heavy),
                    "An unqualified Frost tower retains ordinary damage.");

                Fresh(session, AugmentId.BurningMark); Tick(session, 1000);
                heavy = FindEnemy(session, EnemyKind.Heavy); PlaceOnly(session, heavy, Vector3.zero); Set(session, "Mana", 24);
                session.TryCastFireball(Vector3.zero); spell = session.GetComponentsInChildren<FireballArea>().First();
                spell.Advance(.251f); heavyHP = heavy.CurrentHealth; Hit(session, heavy, Gun());
                int markedDamage = Armored(Mathf.RoundToInt(10 * 1.25f), heavy);
                check("Burning Mark boosts tower hits inside an active burn", heavy.CurrentHealth == heavyHP - markedDamage,
                    "The Fireball's current coverage changes tower damage.");
                session.TryCastFireball(Vector3.zero);
                var secondSpell = session.GetComponentsInChildren<FireballArea>().First(f => f != spell);
                secondSpell.Advance(.251f); heavyHP = heavy.CurrentHealth; Hit(session, heavy, Gun());
                check("Overlapping burns do not stack Burning Mark", heavy.CurrentHealth == heavyHP - markedDamage,
                    "Two active areas still apply a single +25% vulnerability.");
                heavy.transform.position = Vector3.right * (session.FireballRadius + 1); heavyHP = heavy.CurrentHealth; Hit(session, heavy, Gun());
                check("Leaving the burn immediately removes Burning Mark", heavy.CurrentHealth == heavyHP - Armored(10, heavy),
                    "The modifier is spatial, not a persistent enemy debuff.");

                Fresh(session, AugmentId.FieldMedic); Tick(session, 1000);
                Set(session, "BaseHealth", session.map.baseHealth - 8);
                foreach (EnemyAgent enemy in session.ActiveEnemies.Take(100).ToArray()) enemy.TakeDamage(100000);
                check("Field Medic heals on kill milestones up to six times", session.BaseHealth == session.map.baseHealth - 2,
                    "100 kills include six completed 15-kill milestones; no seventh payment.");
                Fresh(session, AugmentId.FieldMedic); Tick(session, 1000);
                foreach (EnemyAgent enemy in session.ActiveEnemies.Take(15).ToArray()) enemy.TakeDamage(100000);
                check("Field Medic cannot exceed the core's configured maximum", session.BaseHealth == session.map.baseHealth,
                    "Healing at full health does not create excess health or shield.");

                session.enableAugments = true; session.RestartLevel(); session.TryChooseAugment(0);
                session.ToggleSpeed(); Set(session, "Mana", 120); Set(session, "Gold", 5000);
                Build(session, 0, TowerKind.Gun);
                float midpoint = session.waves[3].startTime;
                Tick(session, midpoint - 1f);
                first = session.ActiveEnemies[0]; session.FireProjectile(first.AimPosition + Vector3.up * 15, first, Gun());
                session.TryCastFireball(Vector3.zero); spell = session.GetComponentsInChildren<FireballArea>().First();
                Tick(session, midpoint);
                check("Midpoint choice is gold, unique and freezes the assault",
                    session.IsChoosingAugment && !session.IsCombatRunning && Time.timeScale == 0
                    && session.AugmentOffers.Count == 3 && session.AugmentOffers.All(d => d.Tier == AugmentTier.Gold)
                    && session.AugmentOffers.Select(d => d.Id).Distinct().Count() == 3
                    && session.AugmentOffers.Any(d => d.IsGeneric),
                    "The second choice occurs before the fourth assault phase.");
                float heldTime = session.AssaultTime; int heldGold = session.Gold, heldMana = session.Mana;
                int heldTicks = session.SpellDamageTicks; spell.Advance(0); Call(session, "Update");
                bool blockedMidBuild = !session.TryBuildTower(session.map.buildSlots[1], TowerKind.Gun);
                bool blockedMidUpgrade = !session.TryUpgradeTower(session.map.buildSlots[0]);
                bool blockedMidCast = !session.TryCastFireball(Vector3.zero);
                check("Midpoint choice blocks actions without spending or advancing burns",
                    blockedMidBuild && blockedMidUpgrade && blockedMidCast && session.Gold == heldGold
                    && session.Mana == heldMana && session.AssaultTime == heldTime && session.SpellDamageTicks == heldTicks,
                    "A live battle remains frozen while the player reads cards.");
                bool secondChosen = session.TryChooseAugment(0);
                check("Second choice restores the previous double speed", secondChosen
                    && session.SelectedAugments.Count == 2 && session.GameSpeed == 2 && Time.timeScale == 2 && session.IsCombatRunning,
                    "Choosing a card does not silently reset the player's 2x setting.");
                Tick(session, midpoint + 1f);
                check("The midpoint selection is not offered twice", !session.IsChoosingAugment && session.SelectedAugments.Count == 2,
                    "Advancing past the trigger cannot grant another card.");
                session.RestartLevel(); spell.Advance(5);
                check("Restart clears effects, rewards, callbacks and battle objects",
                    session.IsChoosingAugment && session.SelectedAugments.Count == 0 && session.BaseShield == 0
                    && session.Mana == 0 && session.Gold == session.map.initialGold
                    && session.ActiveEnemies.Count == 0 && session.ActiveProjectileCount == 0 && session.ActiveSpellCount == 0
                    && session.SpellDamageTicks == 0 && session.GameSpeed == 1 && Time.timeScale == 0,
                    "An old pooled burn cannot damage the fresh opening choice.");
            }
            catch (Exception error)
            {
                report.error = error.ToString();
                check("Validation completed", false, error.Message);
                Debug.LogException(error);
            }
            finally
            {
                session.augmentSeed = originalSeed;
                session.enableAugments = true;
                session.RestartLevel();
                var hud = session.GetComponent<GameHud>();
                if (hud) hud.CancelFireballTargeting();
            }
            report.checks = rows.ToArray(); report.failures = rows.Count(r => !r.passed); report.passed = report.failures == 0;
            Directory.CreateDirectory("Captures/AugmentGameplay");
            File.WriteAllText("Captures/AugmentGameplay/AugmentBoundaryChecks.json", JsonUtility.ToJson(report, true));
            Debug.Log("Augment boundary checks: " + (rows.Count - report.failures) + "/" + rows.Count + " passed.");
        }
    }
}
