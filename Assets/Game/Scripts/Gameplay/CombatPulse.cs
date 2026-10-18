using System;
using UnityEngine;

namespace TowerDefense.Gameplay
{
    /// <summary>A pooled, transparent pair of shockwaves with a short elemental burst.</summary>
    public sealed class CombatPulse : MonoBehaviour
    {
        const float Lifetime = .32f;
        LineRenderer line, echo;
        CombatVisuals effects;
        Color tint;
        float age, radius;
        bool playing;
        Action<CombatPulse> returned;

        void Awake()
        {
            effects = CombatVisuals.For(this);
            line = CombatVisuals.MakeRing("Outer Pulse", transform, effects.RibbonMaterial);
            echo = CombatVisuals.MakeRing("Inner Pulse", transform, effects.RibbonMaterial);
            echo.transform.localPosition = Vector3.up * .012f;
        }
        public void Play(Vector3 position, float size, Color color, Action<CombatPulse> callback)
        {
            transform.position = position + Vector3.up * .05f;
            radius = Mathf.Max(.2f, size);
            tint = color; age = 0f; returned = callback; playing = true;
            effects.AbilityBurst(transform.position, radius, tint);
            Draw();
        }
        void Update()
        {
            if (!playing || Time.deltaTime <= 0f || Time.timeScale <= 0f) return;
            age += Time.deltaTime;
            Draw();
            if (age < Lifetime) return;
            playing = false;
            var callback = returned;
            returned = null;
            callback?.Invoke(this);
        }
        void Draw()
        {
            float progress = Mathf.Clamp01(age / Lifetime);
            float eased = 1f - (1f - progress) * (1f - progress);
            Color outer = tint; outer.a = .7f * (1f - progress);
            Color inner = Color.Lerp(tint, Color.white, .2f); inner.a = .32f * (1f - progress);
            CombatVisuals.DrawRing(line, radius * Mathf.Lerp(.35f, 1f, eased), outer,
                Mathf.Lerp(.09f, .025f, progress));
            CombatVisuals.DrawRing(echo, radius * Mathf.Lerp(.2f, .88f, eased), inner,
                Mathf.Lerp(.045f, .012f, progress));
        }
        void OnDisable() { playing = false; returned = null; }
    }
}
