using UnityEngine;

namespace TowerDefense.Maps
{
    [DisallowMultipleComponent]
    public sealed class BuildSlot : MonoBehaviour
    {
        public string slotId;
        public string serviceArea;
        [Min(0.1f)] public float previewRadius = 5.5f;

        private void OnDrawGizmosSelected()
        {
            if (previewRadius <= 0f)
                return;

            const int segments = 48;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 1f);
            Vector3 center = transform.position + Vector3.up * 0.05f;
            Vector3 previous = center + Vector3.right * previewRadius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * previewRadius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
