using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TowerDefense.UI
{
    /// <summary>Localizes runtime-created labels without replacing their original English sources.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class LocalizedCanvas : MonoBehaviour
    {
        sealed class LabelState
        {
            public string source;
            public string lastApplied;
        }

        readonly Dictionary<TMP_Text, LabelState> labels = new Dictionary<TMP_Text, LabelState>();
        TMP_Text[] children;
        Canvas canvas;
        float nextScan;
        int lastScreenWidth = -1, lastScreenHeight = -1;
        float lastScale = -1f;

        void OnEnable()
        {
            GameSettings.Changed += RefreshNow;
            canvas = GetComponent<Canvas>();
            lastScale = -1f;
            RefreshNow();
        }

        void OnDisable() => GameSettings.Changed -= RefreshNow;

        void OnTransformChildrenChanged() => nextScan = 0f;

        public void RefreshNow()
        {
            Scan();
            Apply();
        }

        void Scan()
        {
            children = GetComponentsInChildren<TMP_Text>(true);
            // Canvases are small and rebuilt as a whole; discard only destroyed label records.
            var removed = new List<TMP_Text>();
            foreach (var pair in labels) if (!pair.Key) removed.Add(pair.Key);
            foreach (var label in removed) labels.Remove(label);
            nextScan = Time.unscaledTime + .35f;
        }

        void LateUpdate()
        {
            if (children == null || Time.unscaledTime >= nextScan) Scan();
            Apply();
            RefreshPixelDensity();
        }

        void RefreshPixelDensity()
        {
            if (!canvas || !canvas.isActiveAndEnabled || children == null) return;
            if (lastScreenWidth == Screen.width && lastScreenHeight == Screen.height
                && Mathf.Approximately(lastScale, canvas.scaleFactor)) return;
            // Rebuild static and hidden text too when the Game view changes pixel density.
            Canvas.ForceUpdateCanvases();
            foreach (var label in children)
            {
                if (!label) continue;
                label.UpdateMeshPadding();
                label.SetAllDirty();
                label.ForceMeshUpdate(true, true);
            }
            Canvas.ForceUpdateCanvases();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastScale = canvas.scaleFactor;
        }

        void Apply()
        {
            if (children == null) return;
            foreach (var label in children)
            {
                if (!label) continue;
                if (!labels.TryGetValue(label, out var state))
                {
                    state = new LabelState { source = label.text, lastApplied = label.text };
                    labels.Add(label, state);
                    Localization.ApplyFont(label);
                }
                else if (label.text != state.lastApplied)
                    state.source = label.text;
                string translated = Localization.T(state.source);
                if (label.text != translated) label.text = translated;
                state.lastApplied = translated;
            }
        }
    }
}
