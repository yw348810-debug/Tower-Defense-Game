using UnityEngine;

namespace TowerDefense.UI
{
    /// <summary>Replays a brief fade on opening a page/card, including when the battle is paused.</summary>
    [AddComponentMenu("")]
    public sealed class UiEntry : MonoBehaviour
    {
        public float Delay;
        private CanvasGroup group;
        private float elapsed;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            elapsed = 0f;
            if (group != null) group.alpha = 0f;
        }

        private void Update()
        {
            if (elapsed >= Delay + .20f) return;
            elapsed += Time.unscaledDeltaTime;
            if (group == null) return;
            float progress = Mathf.Clamp01((elapsed - Delay) / .20f);
            group.alpha = 1f - Mathf.Pow(1f - progress, 3f);
        }
    }
}
