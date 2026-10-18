using System;
using UnityEngine;

namespace TowerDefense.Maps
{
    [DisallowMultipleComponent]
    public sealed class EnemyRoute : MonoBehaviour
    {
        public string routeId;
        public Transform[] waypoints = Array.Empty<Transform>();

        private void OnDrawGizmos()
        {
            if (waypoints == null)
                return;

            Gizmos.color = new Color(0.2f, 0.85f, 1f, 1f);
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null)
                    continue;

                Vector3 start = waypoints[i].position;
                Gizmos.DrawWireSphere(start, 0.12f);
                if (i + 1 >= waypoints.Length || waypoints[i + 1] == null)
                    continue;

                Vector3 end = waypoints[i + 1].position;
                Gizmos.DrawLine(start, end);
                Vector3 segment = end - start;
                if (segment.sqrMagnitude < 0.0001f)
                    continue;

                Vector3 direction = segment.normalized;
                Vector3 referenceUp = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.95f
                    ? Vector3.forward
                    : Vector3.up;
                Vector3 side = Vector3.Cross(direction, referenceUp).normalized;
                float arrowSize = Mathf.Min(0.55f, segment.magnitude * 0.25f);
                Vector3 tip = Vector3.Lerp(start, end, 0.6f);
                Vector3 tail = tip - direction * arrowSize;
                Gizmos.DrawLine(tip, tail + side * arrowSize * 0.5f);
                Gizmos.DrawLine(tip, tail - side * arrowSize * 0.5f);
            }
        }
    }
}
