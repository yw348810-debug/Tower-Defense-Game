using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Maps
{
    /// <summary>Frames the playable area in perspective while keeping pads and routes visible.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class MapCameraRig : MonoBehaviour
    {
        public LevelMap map;
        [Range(25f, 60f)] public float pitch = 36f;
        [Range(-15f, 15f)] public float yaw = -7f;
        [Range(30f, 65f)] public float fieldOfView = 44f;
        public Vector3 focusOffset = new Vector3(0f, .5f, -2.5f);
        public Rect gameplayViewport = new Rect(.025f, .10f, .95f, .77f);
        public bool alignToViewportCenter;
        [Min(1f)] public float routeFramingHeight = 3.5f;

        readonly List<Vector3> framingPoints = new List<Vector3>();
        Camera view;
        float previousAspect = -1f;

        void OnEnable() { ApplyFraming(); }
        void LateUpdate() { RefreshAspect(); }
        void OnPreCull() { RefreshAspect(); }

        void RefreshAspect()
        {
            if (!view) view = GetComponent<Camera>();
            if (Mathf.Abs(view.aspect - previousAspect) > .001f) ApplyFraming();
        }

        public void ApplyFraming()
        {
            if (!map) return;
            if (!view) view = GetComponent<Camera>();
            framingPoints.Clear();
            if (map.routes != null)
                foreach (var route in map.routes)
                    if (route && route.waypoints != null)
                        foreach (var waypoint in route.waypoints)
                            if (waypoint) AddFootprint(waypoint.position, 1.8f, routeFramingHeight);
            if (map.buildSlots != null)
                foreach (var slot in map.buildSlots)
                    if (slot) AddFootprint(slot.transform.position, .95f, 2.6f);
            if (map.baseAnchor) AddFootprint(map.baseAnchor.position, 1.4f, 4f);
            if (framingPoints.Count == 0) return;

            var bounds = new Bounds(framingPoints[0], Vector3.zero);
            foreach (var point in framingPoints) bounds.Encapsulate(point);
            Vector3 focus = new Vector3(bounds.center.x, map.transform.position.y, bounds.center.z) + focusOffset;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            view.orthographic = false;
            view.fieldOfView = fieldOfView;
            view.nearClipPlane = .1f;
            view.farClipPlane = 220f;
            view.rect = new Rect(0, 0, 1, 1);
            transform.rotation = rotation;
            float near = 15f, far = 120f;
            // Only runs on setup or an aspect-ratio change, not each frame.
            for (int i = 0; i < 18; i++)
            {
                float distance = (near + far) * .5f;
                PlaceCamera(focus, distance);
                if (FitsViewport()) far = distance;
                else near = distance;
            }
            PlaceCamera(focus, far + .15f);
            previousAspect = view.aspect;
        }

        void PlaceCamera(Vector3 focus, float distance)
        {
            Vector3 position = focus - transform.forward * distance;
            if (alignToViewportCenter)
            {
                // Place the focus in the usable region when a side panel occupies part of the screen.
                float halfHeight = distance * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * .5f);
                Vector2 center = gameplayViewport.center;
                position += transform.right * ((.5f - center.x) * 2f * halfHeight * view.aspect)
                    + transform.up * ((.5f - center.y) * 2f * halfHeight);
            }
            transform.position = position;
        }

        void AddFootprint(Vector3 center, float radius, float height)
        {
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var corner = center + new Vector3(x * radius, 0, z * radius);
                    framingPoints.Add(corner);
                    framingPoints.Add(corner + Vector3.up * height);
                }
        }

        bool FitsViewport()
        {
            foreach (var point in framingPoints)
            {
                Vector3 projected = view.WorldToViewportPoint(point);
                if (projected.z <= view.nearClipPlane || projected.x < gameplayViewport.xMin ||
                    projected.x > gameplayViewport.xMax || projected.y < gameplayViewport.yMin ||
                    projected.y > gameplayViewport.yMax) return false;
            }
            return true;
        }
    }
}
