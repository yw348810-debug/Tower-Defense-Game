using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Maps
{
    [DisallowMultipleComponent]
    public sealed class LevelMap : MonoBehaviour
    {
        public string levelId;
        public string displayName;
        [Min(0)] public int initialGold = 180;
        [Min(1)] public int baseHealth = 20;
        public Transform baseAnchor;
        public EnemyRoute[] routes = Array.Empty<EnemyRoute>();
        public BuildSlot[] buildSlots = Array.Empty<BuildSlot>();

        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(levelId))
                errors.Add("Level ID is empty.");
            if (string.IsNullOrWhiteSpace(displayName))
                errors.Add("Display name is empty.");
            if (initialGold < 0)
                errors.Add("Initial gold must not be negative.");
            if (baseHealth < 1)
                errors.Add("Base health must be positive.");
            if (baseAnchor == null)
                errors.Add("Base anchor is missing.");

            if (routes == null || routes.Length == 0)
            {
                errors.Add("At least one enemy route is required.");
            }
            else
            {
                var routeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < routes.Length; i++)
                {
                    EnemyRoute route = routes[i];
                    if (route == null)
                    {
                        errors.Add($"Route [{i}] is missing.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(route.routeId))
                        errors.Add($"Route [{i}] has an empty ID.");
                    else if (!routeIds.Add(route.routeId.Trim()))
                        errors.Add($"Duplicate route ID: {route.routeId}.");

                    if (route.waypoints == null || route.waypoints.Length < 2)
                    {
                        errors.Add($"Route [{i}] requires at least two ordered waypoints.");
                        continue;
                    }

                    for (int j = 0; j < route.waypoints.Length; j++)
                    {
                        if (route.waypoints[j] == null)
                            errors.Add($"Route [{i}] waypoint [{j}] is missing.");
                    }
                }
            }

            if (buildSlots == null || buildSlots.Length == 0)
            {
                errors.Add("At least one build slot is required.");
            }
            else
            {
                var slotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < buildSlots.Length; i++)
                {
                    BuildSlot slot = buildSlots[i];
                    if (slot == null)
                    {
                        errors.Add($"Build slot [{i}] is missing.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(slot.slotId))
                        errors.Add($"Build slot [{i}] has an empty ID.");
                    else if (!slotIds.Add(slot.slotId.Trim()))
                        errors.Add($"Duplicate build slot ID: {slot.slotId}.");

                    if (slot.previewRadius <= 0f)
                        errors.Add($"Build slot [{i}] preview radius must be positive.");
                }
            }

            return errors;
        }

        private void OnDrawGizmosSelected()
        {
            if (baseAnchor == null)
                return;

            Gizmos.color = new Color(0.25f, 1f, 0.45f, 1f);
            Gizmos.DrawWireCube(baseAnchor.position + Vector3.up * 0.5f, Vector3.one);
        }
    }
}
