using UnityEngine;

namespace TowerDefense.Gameplay
{
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        public EnemyAgent enemy;
        public Transform fill;
        public float width = .95f;
        Camera view;

        void OnEnable() { view = Camera.main; }

        void LateUpdate()
        {
            if (!enemy || !fill) return;
            if (view) transform.rotation = view.transform.rotation;
            float fraction = enemy.MaxHealth > 0 ? Mathf.Clamp01((float)enemy.CurrentHealth / enemy.MaxHealth) : 0f;
            fill.localScale = new Vector3(width * fraction, .075f, .025f);
            fill.localPosition = new Vector3(-width * (1f - fraction) * .5f, 0, -.025f);
        }
    }
}
