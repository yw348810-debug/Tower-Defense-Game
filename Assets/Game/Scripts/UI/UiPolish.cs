using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>Shared, code-generated surfaces. No downloaded textures or per-frame allocations.</summary>
    public static class UiPolish
    {
        public static readonly Color Trim = new Color(.73f, .68f, .52f);
        public static readonly Color Teal = new Color(.23f, .91f, .78f);
        private static Sprite roundedSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGeneratedResources()
        {
            // Enter Play Mode may retain static fields; a domain reload may lose them.
            // Both paths retire only the two named, transient resources owned here.
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                if (sprite.name == "UI Rounded Nine Slice (Generated)" && sprite.hideFlags == HideFlags.HideAndDontSave)
                    Object.DestroyImmediate(sprite);
            foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
                if (texture.name == "UI Rounded Surface (Generated)" && texture.hideFlags == HideFlags.HideAndDontSave)
                    Object.DestroyImmediate(texture);
            roundedSprite = null;
        }

        public static UiSurface Skin(Image image, Color accent, float radius = 7f)
        {
            if (image == null) return null;
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            var shadow = image.GetComponent<Shadow>();
            if (shadow == null) shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.005f, .015f, .022f, .40f);
            shadow.effectDistance = new Vector2(0f, -3f);
            shadow.useGraphicAlpha = true;
            var existing = image.transform.Find("Polished Surface");
            UiSurface surface;
            if (existing != null) surface = existing.GetComponent<UiSurface>();
            else
            {
                var child = new GameObject("Polished Surface", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiSurface));
                var rect = child.GetComponent<RectTransform>();
                rect.SetParent(image.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.SetAsFirstSibling();
                surface = child.GetComponent<UiSurface>();
            }
            surface.Source = image;
            surface.Accent = accent;
            surface.Radius = radius;
            surface.raycastTarget = false;
            surface.SetVerticesDirty();
            return surface;
        }

        public static void SetAccent(Image image, Color accent, bool selected = false)
        {
            if (image == null) return;
            Transform child = image.transform.Find("Polished Surface");
            if (child == null) return;
            var surface = child.GetComponent<UiSurface>();
            if (surface == null) return;
            if (surface.Accent == accent && surface.Selected == selected) return;
            surface.Accent = accent;
            surface.Selected = selected;
            surface.SetVerticesDirty();
        }

        public static UiFeedback Feedback(Button button, float lift = 1.015f)
        {
            UiSound.Attach(button);
            var feedback = button.GetComponent<UiFeedback>();
            if (feedback == null) feedback = button.gameObject.AddComponent<UiFeedback>();
            feedback.HoverScale = lift;
            return feedback;
        }

        public static UiEntry Entrance(GameObject owner, float delay = 0f)
        {
            var entry = owner.GetComponent<UiEntry>();
            if (entry == null) entry = owner.AddComponent<UiEntry>();
            entry.Delay = delay;
            return entry;
        }

        private static Sprite RoundedSprite
        {
            get
            {
                if (roundedSprite != null) return roundedSprite;
                const int size = 32;
                const float radius = 7f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "UI Rounded Surface (Generated)";
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 point = new Vector2(x + .5f, y + .5f);
                        Vector2 nearest = new Vector2(Mathf.Clamp(point.x, radius, size - radius), Mathf.Clamp(point.y, radius, size - radius));
                        float alpha = Mathf.Clamp01(radius - Vector2.Distance(point, nearest) + .5f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                    }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
                roundedSprite.name = "UI Rounded Nine Slice (Generated)";
                roundedSprite.hideFlags = HideFlags.HideAndDontSave;
                return roundedSprite;
            }
        }
    }
}
