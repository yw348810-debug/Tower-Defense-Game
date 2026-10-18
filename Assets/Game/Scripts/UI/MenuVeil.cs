using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>A translucent veil leaves the scene diorama visible behind readable navigation.</summary>
    public sealed class MenuVeil : MaskableGraphic
    {
        public bool fullShade;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            var left = new Color(.018f, .042f, .064f, fullShade ? .94f : .98f);
            var middle = new Color(.018f, .042f, .064f, fullShade ? .94f : .82f);
            var right = new Color(.018f, .042f, .064f, fullShade ? .94f : .12f);
            AddQuad(vh, rect.xMin, rect.yMin, Mathf.Lerp(rect.xMin, rect.xMax, .43f), rect.yMax, left, middle);
            AddQuad(vh, Mathf.Lerp(rect.xMin, rect.xMax, .43f), rect.yMin, rect.xMax, rect.yMax, middle, right);
        }

        private static void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, Color c0, Color c1)
        {
            int index = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), c0, Vector2.zero);
            vh.AddVert(new Vector3(x0, y1), c0, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), c1, Vector2.zero);
            vh.AddVert(new Vector3(x1, y0), c1, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
