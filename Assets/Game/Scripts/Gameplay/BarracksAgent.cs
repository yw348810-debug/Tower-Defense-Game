using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Gameplay
{
    /// <summary>Maintains independent troop slots; death and respawn never create extra troops.</summary>
    [DisallowMultipleComponent]
    public sealed class BarracksAgent : MonoBehaviour
    {
        public SoldierAgent soldierPrefab;
        public SoldierAgent heroPrefab;
        [Min(1f)] public float rallySearchDistance = 7f;
        [Min(1f)] public float recruitRadius = 2.5f;
        [Min(1f)] public float soldierRespawnSeconds = 8f;
        [Min(1f)] public float heroRespawnSeconds = 15f;
        public TowerAgent Tower { get; private set; }
        public Vector3 RallyPosition { get; private set; }
        public int Tier { get; private set; }
        public int SoldierCapacity => Tier >= 2 ? 3 : 2;
        public int HeroCapacity => Tier >= 3 ? 1 : 0;
        public int AliveSoldierCount => CountAlive(false);
        public int AliveHeroCount => CountAlive(true);
        public float RespawnSeconds => soldierRespawnSeconds * (session != null ? session.Augments.RespawnMultiplier : 1f);
        public float HeroRespawnSeconds => heroRespawnSeconds * (session != null ? session.Augments.RespawnMultiplier : 1f);
        public int SoldierHealth => Mathf.RoundToInt((Tier >= 3 ? 180 : Tier == 2 ? 140 : 90)
            * (session != null ? session.Augments.SoldierHealthMultiplier : 1f));
        public int SoldierDamage => Mathf.RoundToInt((Tier >= 3 ? 24 : Tier == 2 ? 18 : 12)
            * (session != null ? session.Augments.SoldierDamageMultiplier : 1f));
        public int HeroHealth => Mathf.RoundToInt(520 * (session != null ? session.Augments.SoldierHealthMultiplier : 1f));
        public int HeroDamage => Mathf.RoundToInt(52 * (session != null ? session.Augments.SoldierDamageMultiplier : 1f));
        public string RecruitSummary => $"{AliveSoldierCount}/{SoldierCapacity} soldiers"
            + (HeroCapacity > 0 ? $" · {AliveHeroCount}/1 hero" : "");
        public IReadOnlyList<SoldierAgent> Troops => troopView;

        sealed class TroopSlot
        {
            public bool hero;
            public SoldierAgent agent;
            public float respawn;
            public int formation;
        }
        readonly List<TroopSlot> slots = new List<TroopSlot>(4);
        readonly List<SoldierAgent> troopView = new List<SoldierAgent>(4);
        LevelSession session;
        bool initialized;
        bool hasRally;

        public void Initialize(LevelSession levelSession, TowerAgent tower)
        {
            Shutdown();
            session = levelSession;
            Tower = tower;
            initialized = true;
            Vector3 rally = transform.position;
            hasRally = session != null && session.TryGetRallyPoint(transform.position, out rally);
            RallyPosition = hasRally ? rally : transform.position;
            if (hasRally)
            {
                Vector3 delta = RallyPosition - transform.position;
                delta.y = 0f;
                hasRally = delta.sqrMagnitude <= rallySearchDistance * rallySearchDistance;
            }
            SetTier(tower != null ? tower.Level : 1);
        }

        public void SetTier(int tier)
        {
            Tier = Mathf.Clamp(tier, 1, 3);
            if (!initialized) return;
            while (CountSlots(false) < SoldierCapacity)
                slots.Add(new TroopSlot { formation = CountSlots(false) });
            if (HeroCapacity > 0 && CountSlots(true) == 0)
                slots.Add(new TroopSlot { hero = true, formation = 3 });
            // Existing dead slots retain their countdown; an upgrade does not revive them early.
            RefreshTroopStats();
            if (session != null && session.IsCombatRunning) SpawnReadySlots();
        }

        public void RefreshTroopStats()
        {
            foreach (TroopSlot slot in slots)
            {
                if (slot.agent == null || !slot.agent.IsAlive) continue;
                GetStats(slot.hero, out int health, out int damage, out float interval);
                slot.agent.SetStats(health, damage, interval);
                slot.agent.SetVisualTier(Tier);
            }
        }

        void GetStats(bool hero, out int health, out int damage, out float interval)
        {
            health = hero ? 520 : Tier >= 3 ? 180 : Tier == 2 ? 140 : 90;
            damage = hero ? 52 : Tier >= 3 ? 24 : Tier == 2 ? 18 : 12;
            interval = hero ? .75f : Tier >= 2 ? .8f : .9f;
            if (session == null) return;
            health = Mathf.Max(1, Mathf.RoundToInt(health * session.Augments.SoldierHealthMultiplier));
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * session.Augments.SoldierDamageMultiplier));
        }

        void Update()
        {
            if (!initialized || session == null || !session.IsCombatRunning || !hasRally || Time.deltaTime <= 0f) return;
            foreach (TroopSlot slot in slots)
            {
                if (slot.agent != null && slot.agent.IsAlive) continue;
                slot.respawn = Mathf.Max(0f, slot.respawn - Time.deltaTime);
            }
            SpawnReadySlots();
        }

        void SpawnReadySlots()
        {
            if (!hasRally) return;
            foreach (TroopSlot slot in slots)
            {
                if (slot.agent != null && slot.agent.IsAlive || slot.respawn > 0f) continue;
                SoldierAgent prefab = slot.hero ? heroPrefab : soldierPrefab;
                if (prefab == null) continue;
                Vector3 offset = slot.formation switch
                {
                    0 => new Vector3(-.6f, 0, -.35f),
                    1 => new Vector3(.6f, 0, .35f),
                    2 => new Vector3(0, 0, -.9f),
                    _ => new Vector3(0, 0, .9f)
                };
                Vector3 guard = RallyPosition + offset;
                if (session.TryGetRallyPoint(guard, out Vector3 road)) guard = road;
                GetStats(slot.hero, out int health, out int damage, out float interval);
                SoldierAgent agent = Instantiate(prefab, guard, transform.rotation, transform);
                slot.agent = agent;
                troopView.Add(agent);
                agent.Initialize(session, this, guard, slot.hero, health, damage, interval,
                    recruitRadius, dead => OnTroopDeath(slot, dead));
                agent.SetVisualTier(Tier);
            }
        }

        void OnTroopDeath(TroopSlot slot, SoldierAgent dead)
        {
            if (slot.agent != dead) return;
            slot.agent = null;
            troopView.Remove(dead);
            slot.respawn = (slot.hero ? heroRespawnSeconds : soldierRespawnSeconds)
                * (session != null ? session.Augments.RespawnMultiplier : 1f);
            if (dead != null)
            {
                dead.gameObject.SetActive(false);
                Destroy(dead.gameObject);
            }
        }

        int CountAlive(bool hero)
        {
            int count = 0;
            foreach (TroopSlot slot in slots)
                if (slot.hero == hero && slot.agent != null && slot.agent.IsAlive) count++;
            return count;
        }

        int CountSlots(bool hero)
        {
            int count = 0;
            foreach (TroopSlot slot in slots) if (slot.hero == hero) count++;
            return count;
        }

        public void Shutdown()
        {
            initialized = false;
            foreach (TroopSlot slot in slots)
            {
                if (slot.agent == null) continue;
                slot.agent.gameObject.SetActive(false);
                Destroy(slot.agent.gameObject);
                slot.agent = null;
            }
            slots.Clear();
            troopView.Clear();
            session = null;
            Tower = null;
            Tier = 1;
            hasRally = false;
        }

        void OnDisable() => Shutdown();
    }
}
