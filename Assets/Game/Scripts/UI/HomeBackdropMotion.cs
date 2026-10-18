using UnityEngine;

namespace TowerDefense.UI
{
    /// <summary>Subtle unscaled motion adds life to the decorative menu scene.</summary>
    [DisallowMultipleComponent]
    public sealed class HomeBackdropMotion : MonoBehaviour
    {
        private Vector3 origin;
        private Quaternion orientation;

        private void Awake()
        {
            origin = transform.position;
            orientation = transform.rotation;
        }

        private void LateUpdate()
        {
            float time = Time.unscaledTime;
            transform.position = origin + new Vector3(Mathf.Sin(time * .12f) * .3f, Mathf.Sin(time * .16f) * .16f, 0);
            transform.rotation = orientation * Quaternion.Euler(Mathf.Sin(time * .1f) * .14f, Mathf.Sin(time * .12f) * .28f, 0);
        }
    }
}
