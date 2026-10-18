using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>Rounded gradient, thin metallic rim and restrained highlights, following a Button's tint.</summary>
    [AddComponentMenu("")]
    public sealed class UiSurface : MaskableGraphic
    {
        public Image Source;
        public Color Accent = UiPolish.Trim;
        public float Radius = 7f;
        public bool Selected;
        public float Hover;
        private Color previousTint;
        private Color previousAccent;
        private float previousHover = -1f;
        private bool previousSelected;
        private readonly Vector2[] outer = new Vector2[28];
        private readonly Vector2[] inner = new Vector2[28];

        private void LateUpdate()
        {
            Color tint = Source != null ? Source.color * Source.canvasRenderer.GetColor() : color;
            if (tint == previousTint && Accent == previousAccent && Mathf.Abs(Hover - previousHover) < .005f && Selected == previousSelected) return;
            previousTint = tint;
            previousAccent = Accent;
            previousHover = Hover;
            previousSelected = Selected;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width < 2 || rect.height < 2) return;
            float radius = Mathf.Min(Radius, Mathf.Min(rect.width, rect.height) * .5f);
            Color tint = Source != null ? Source.color * Source.canvasRenderer.GetColor() : color;
            RoundedPoints(rect, radius, outer);
            Rect inset = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            RoundedPoints(inset, Mathf.Max(0, radius - 1f), inner);
            float emphasis = Selected ? 1f : Hover;
            Color rim = Accent;
            rim.a = tint.a * Mathf.Lerp(.28f, .85f, emphasis);
            Color low = Color.Lerp(tint, new Color(.014f, .030f, .041f, tint.a), .15f);
            Color high = Color.Lerp(tint, new Color(.19f, .29f, .33f, tint.a), .19f);
            low.a = high.a = tint.a;
            int center = mesh.currentVertCount;
            mesh.AddVert(rect.center, Color.Lerp(low, high, .5f), Vector2.zero);
            for (int i = 0; i < outer.Length; i++)
                mesh.AddVert(inner[i], Color.Lerp(low, high, Mathf.InverseLerp(rect.yMin, rect.yMax, inner[i].y)), Vector2.zero);
            for (int i = 0; i < outer.Length; i++)
                mesh.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % outer.Length);
            for (int i = 0; i < outer.Length; i++)
            {
                int next = (i + 1) % outer.Length;
                int start = mesh.currentVertCount;
                Color edge = rim;
                edge.a *= Mathf.Lerp(.4f, 1f, Mathf.InverseLerp(rect.yMin, rect.yMax, outer[i].y));
                mesh.AddVert(outer[i], edge, Vector2.zero);
                mesh.AddVert(outer[next], edge, Vector2.zero);
                mesh.AddVert(inner[next], edge, Vector2.zero);
                mesh.AddVert(inner[i], edge, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
            // Corner inlays make the border visible without competing with the text.
            float inlay = Mathf.Min(24f, rect.width * .16f);
            Color glint = Accent;
            glint.a = tint.a * Mathf.Lerp(.50f, .95f, emphasis);
            Quad(mesh, new Rect(rect.xMin + radius + 3f, rect.yMax - 2.4f, inlay, 1.25f), glint);
            Color soft = new Color(.77f, .93f, .93f, tint.a * .035f);
            Quad(mesh, new Rect(rect.xMin + radius, rect.yMax - 6f, Mathf.Max(0, rect.width - radius * 2f), 3f), soft);
        }

        private static void RoundedPoints(Rect rect, float radius, Vector2[] points)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                    corner < 2 ? rect.yMax - radius : rect.yMin + radius);
                for (int step = 0; step <= 6; step++)
                {
                    float angle = (corner * 90f + step * 15f) * Mathf.Deg2Rad;
                    points[corner * 7 + step] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
            }
        }

        private static void Quad(VertexHelper mesh, Rect rect, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector2(rect.xMin, rect.yMin), tint, Vector2.zero);
            mesh.AddVert(new Vector2(rect.xMin, rect.yMax), tint, Vector2.zero);
            mesh.AddVert(new Vector2(rect.xMax, rect.yMax), tint, Vector2.zero);
            mesh.AddVert(new Vector2(rect.xMax, rect.yMin), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
