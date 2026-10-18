using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.Gameplay
{
    /// <summary>Small HUD symbols drawn as UI geometry so they stay sharp at every screen size.</summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudIcon : MaskableGraphic
    {
        public enum Symbol { Coin, Heart, Enemy, Flag, Upgrade, Gun, Frost, Cannon, Mana, Fireball, Barracks, FrostSpell, Prismatic, Flying,
            Support, Reinforcements, Battlecry, Boss, Hexagon, Refresh }
        public Symbol symbol;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            float size = Mathf.Min(bounds.width, bounds.height);
            Vector2 center = bounds.center;
            switch (symbol)
            {
                case Symbol.Coin:
                    Disc(mesh, center + Vector2.down * size * .045f, size * .47f, Shade(color, .38f));
                    Disc(mesh, center, size * 0.46f, color);
                    Ring(mesh, center, size * 0.33f, size * 0.027f, Shade(color, 0.57f));
                    Box(mesh, center + Vector2.left * size * 0.025f, new Vector2(size * 0.055f, size * 0.40f), Shade(color, 0.60f));
                    Ring(mesh, center, size * .415f, size * .025f, new Color(1f, .96f, .70f, color.a * .72f));
                    Box(mesh, center + new Vector2(size * .025f, size * .035f), new Vector2(size * .055f, size * .38f), new Color(1f, .95f, .61f, color.a));
                    break;
                case Symbol.Heart:
                    Disc(mesh, center + new Vector2(-size * 0.20f, size * 0.16f), size * 0.235f, color);
                    Disc(mesh, center + new Vector2(size * 0.20f, size * 0.16f), size * 0.235f, color);
                    Triangle(mesh, center + new Vector2(-size * 0.415f, size * 0.09f),
                        center + new Vector2(size * 0.415f, size * 0.09f), center + new Vector2(0f, -size * 0.46f), color);
                    break;
                case Symbol.Enemy:
                    Disc(mesh, center + Vector2.up * size * 0.18f, size * 0.22f, color);
                    Box(mesh, center + Vector2.down * size * 0.23f, new Vector2(size * 0.58f, size * 0.36f), color);
                    break;
                case Symbol.Flag:
                    Box(mesh, center + Vector2.left * size * 0.24f, new Vector2(size * 0.085f, size * 0.86f), color);
                    Triangle(mesh, center + new Vector2(-size * 0.20f, size * 0.42f),
                        center + new Vector2(size * 0.42f, size * 0.23f), center + new Vector2(-size * 0.20f, 0f), color);
                    break;
                case Symbol.Upgrade:
                    Box(mesh, center + Vector2.down * size * 0.15f, new Vector2(size * 0.27f, size * 0.58f), color);
                    Triangle(mesh, center + new Vector2(-size * 0.45f, size * 0.04f),
                        center + new Vector2(size * 0.45f, size * 0.04f), center + Vector2.up * size * 0.46f, color);
                    break;
                case Symbol.Mana:
                    Triangle(mesh, center + Vector2.up * size * 0.46f, center + new Vector2(size * 0.32f, -size * 0.01f),
                        center + Vector2.down * size * 0.45f, color);
                    Triangle(mesh, center + Vector2.up * size * 0.46f, center + Vector2.down * size * 0.45f,
                        center + new Vector2(-size * 0.32f, -size * 0.01f), color);
                    Triangle(mesh, center + Vector2.up * size * 0.34f, center + new Vector2(-size * 0.20f, 0f),
                        center + Vector2.down * size * 0.18f, new Color(0.76f, 0.90f, 1f, color.a));
                    break;
                case Symbol.Fireball:
                    Disc(mesh, center + Vector2.down * size * 0.15f, size * 0.29f, color);
                    Triangle(mesh, center + new Vector2(-size * 0.27f, 0f), center + new Vector2(size * 0.29f, -size * 0.02f),
                        center + new Vector2(size * 0.12f, size * 0.48f), color);
                    Triangle(mesh, center + new Vector2(-size * 0.30f, -size * 0.03f), center + new Vector2(size * 0.08f, size * 0.04f),
                        center + new Vector2(-size * 0.20f, size * 0.33f), color);
                    Color core = new Color(1f, 0.86f, 0.30f, color.a);
                    Disc(mesh, center + Vector2.down * size * 0.18f, size * 0.16f, core);
                    Triangle(mesh, center + new Vector2(-size * 0.15f, -size * 0.14f), center + new Vector2(size * 0.15f, -size * 0.14f),
                        center + new Vector2(size * 0.055f, size * 0.19f), core);
                    break;
                case Symbol.Frost:
                    for (int i = 0; i < 3; i++)
                        Line(mesh, center, Quaternion.Euler(0f, 0f, i * 60f) * Vector2.up, size * 0.42f, size * 0.075f, color);
                    Disc(mesh, center, size * 0.13f, color);
                    break;
                case Symbol.FrostSpell:
                    Ring(mesh, center, size * .45f, size * .035f, color);
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 direction = Quaternion.Euler(0f, 0f, i * 60f) * Vector2.up;
                        Line(mesh, center, direction, size * .30f, size * .05f, color);
                        Vector2 normal = new Vector2(-direction.y, direction.x);
                        for (int end = -1; end <= 1; end += 2)
                        {
                            Vector2 tip = center + direction * (size * .24f * end);
                            Triangle(mesh, tip, tip - direction * (size * .12f * end) + normal * size * .09f,
                                tip - direction * (size * .12f * end) - normal * size * .09f, color);
                        }
                    }
                    Disc(mesh, center, size * .065f, new Color(.86f, 1f, 1f, color.a));
                    break;
                case Symbol.Barracks:
                    Box(mesh, center + Vector2.down * size * .12f, new Vector2(size * .72f, size * .56f), color);
                    Box(mesh, center + new Vector2(-size * .27f, size * .26f), new Vector2(size * .17f, size * .28f), color);
                    Box(mesh, center + Vector2.up * size * .26f, new Vector2(size * .17f, size * .28f), color);
                    Box(mesh, center + new Vector2(size * .27f, size * .26f), new Vector2(size * .17f, size * .28f), color);
                    Box(mesh, center + Vector2.down * size * .24f, new Vector2(size * .21f, size * .31f), Shade(color, .25f));
                    Box(mesh, center + Vector2.down * size * .44f, new Vector2(size * .88f, size * .08f), color);
                    break;
                case Symbol.Support:
                    Ring(mesh, center, size * .43f, size * .045f, color);
                    Box(mesh, center + Vector2.down * size * .19f, new Vector2(size * .11f, size * .48f), color);
                    Disc(mesh, center + Vector2.up * size * .15f, size * .18f, color);
                    Color healing = new Color(.48f, 1f, .67f, color.a);
                    Box(mesh, center + Vector2.up * size * .15f, new Vector2(size * .27f, size * .07f), healing);
                    Box(mesh, center + Vector2.up * size * .15f, new Vector2(size * .07f, size * .27f), healing);
                    Triangle(mesh, center + new Vector2(-size * .19f, -size * .34f), center + new Vector2(size * .19f, -size * .34f),
                        center + new Vector2(0f, -size * .45f), color);
                    break;
                case Symbol.Reinforcements:
                    for (int i = -1; i <= 1; i++)
                    {
                        Vector2 unit = center + new Vector2(i * size * .29f, i == 0 ? size * .09f : -size * .06f);
                        Disc(mesh, unit + Vector2.up * size * .16f, size * .115f, color);
                        Box(mesh, unit + Vector2.down * size * .10f, new Vector2(size * .21f, size * .29f), color);
                    }
                    Box(mesh, center + Vector2.down * size * .40f, new Vector2(size * .70f, size * .07f), color);
                    Triangle(mesh, center + new Vector2(-size * .16f, -size * .41f), center + new Vector2(size * .16f, -size * .41f),
                        center + new Vector2(0f, -size * .29f), color);
                    break;
                case Symbol.Battlecry:
                    Ring(mesh, center, size * .43f, size * .04f, color);
                    Triangle(mesh, center + new Vector2(-size * .34f, -size * .13f), center + new Vector2(-size * .34f, size * .13f),
                        center + new Vector2(size * .29f, 0f), color);
                    Triangle(mesh, center + new Vector2(-size * .17f, -size * .08f), center + new Vector2(size * .29f, -size * .28f),
                        center + new Vector2(size * .29f, size * .28f), color);
                    Box(mesh, center + new Vector2(-size * .28f, -size * .20f), new Vector2(size * .08f, size * .24f), color);
                    break;
                case Symbol.Boss:
                    Box(mesh, center + Vector2.down * size * .24f, new Vector2(size * .70f, size * .21f), color);
                    Triangle(mesh, center + new Vector2(-size * .35f, -size * .15f), center + new Vector2(-size * .43f, size * .32f),
                        center + new Vector2(-size * .08f, -size * .02f), color);
                    Triangle(mesh, center + new Vector2(-size * .23f, -size * .12f), center + Vector2.up * size * .45f,
                        center + new Vector2(size * .23f, -size * .12f), color);
                    Triangle(mesh, center + new Vector2(size * .08f, -size * .02f), center + new Vector2(size * .43f, size * .32f),
                        center + new Vector2(size * .35f, -size * .15f), color);
                    Disc(mesh, center + Vector2.down * size * .24f, size * .055f, new Color(1f, .85f, .35f, color.a));
                    break;
                case Symbol.Hexagon:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                        float b = ((i + 1) * 60f + 30f) * Mathf.Deg2Rad;
                        Vector2 outsideA = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size * .47f;
                        Vector2 outsideB = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * size * .47f;
                        Color bevel = Shade(color, i < 3 ? 1f : .58f);
                        Triangle(mesh, center + outsideA, center + outsideB, center + outsideA * .91f, bevel);
                        Triangle(mesh, center + outsideA * .91f, center + outsideB, center + outsideB * .91f, bevel);
                        Triangle(mesh, center, center + outsideA * .84f, center + outsideB * .84f,
                            new Color(color.r, color.g, color.b, color.a * .10f));
                    }
                    Triangle(mesh, center + Vector2.up * size * .18f, center + Vector2.right * size * .14f,
                        center + Vector2.down * size * .18f, new Color(color.r, color.g, color.b, color.a * .6f));
                    Triangle(mesh, center + Vector2.up * size * .18f, center + Vector2.down * size * .18f,
                        center + Vector2.left * size * .14f, new Color(1f, 1f, 1f, color.a * .38f));
                    break;
                case Symbol.Refresh:
                    Ring(mesh, center, size * .32f, size * .07f, color);
                    Triangle(mesh, center + new Vector2(size * .03f, size * .49f), center + new Vector2(size * .42f, size * .36f),
                        center + new Vector2(size * .12f, size * .17f), color);
                    Triangle(mesh, center + new Vector2(-size * .03f, -size * .49f), center + new Vector2(-size * .42f, -size * .36f),
                        center + new Vector2(-size * .12f, -size * .17f), color);
                    break;
                case Symbol.Prismatic:
                    Color[] spectrum = { new Color(1f, .48f, .67f, color.a), new Color(1f, .81f, .34f, color.a),
                        new Color(.48f, 1f, .73f, color.a), new Color(.41f, .88f, 1f, color.a),
                        new Color(.72f, .59f, 1f, color.a), new Color(1f, .58f, .93f, color.a) };
                    for (int i = 0; i < 6; i++)
                    {
                        float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                        float b = ((i + 1) * 60f + 30f) * Mathf.Deg2Rad;
                        Triangle(mesh, center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size * .46f,
                            center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * size * .46f, spectrum[i]);
                    }
                    Triangle(mesh, center + Vector2.up * size * .28f, center + new Vector2(size * .12f, 0f),
                        center + Vector2.down * size * .28f, new Color(1f, 1f, 1f, color.a));
                    Triangle(mesh, center + Vector2.up * size * .28f, center + Vector2.down * size * .28f,
                        center + new Vector2(-size * .12f, 0f), new Color(1f, 1f, 1f, color.a));
                    break;
                case Symbol.Flying:
                    Disc(mesh, center + Vector2.up * size * .12f, size * .14f, color);
                    Triangle(mesh, center, center + new Vector2(-size * .48f, size * .35f),
                        center + new Vector2(-size * .32f, -size * .18f), color);
                    Triangle(mesh, center, center + new Vector2(size * .32f, -size * .18f),
                        center + new Vector2(size * .48f, size * .35f), color);
                    Triangle(mesh, center + new Vector2(-size * .12f, -.04f * size),
                        center + new Vector2(size * .12f, -.04f * size), center + Vector2.down * size * .40f, color);
                    break;
                case Symbol.Gun:
                case Symbol.Cannon:
                    Box(mesh, center + Vector2.down * size * 0.31f, new Vector2(size * 0.78f, size * 0.17f), color);
                    Disc(mesh, center + Vector2.down * size * 0.05f, size * 0.25f, color);
                    Box(mesh, center + Vector2.up * size * 0.19f,
                        new Vector2(size * (symbol == Symbol.Cannon ? 0.30f : 0.13f), size * 0.54f), color);
                    break;
            }
        }

        private static Color Shade(Color value, float shade) => new Color(value.r * shade, value.g * shade, value.b * shade, value.a);

        private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        private static void Box(VertexHelper mesh, Vector2 center, Vector2 size, Color tint)
        {
            Vector2 half = size * 0.5f;
            Triangle(mesh, center + new Vector2(-half.x, -half.y), center + new Vector2(-half.x, half.y), center + half, tint);
            Triangle(mesh, center + new Vector2(-half.x, -half.y), center + half, center + new Vector2(half.x, -half.y), tint);
        }

        private static void Disc(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                Triangle(mesh, center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, tint);
            }
        }

        private static void Ring(VertexHelper mesh, Vector2 center, float radius, float thickness, Color tint)
        {
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = new Vector2(Mathf.Cos(i * Mathf.PI * 2f / segments), Mathf.Sin(i * Mathf.PI * 2f / segments));
                Vector2 b = new Vector2(Mathf.Cos((i + 1) * Mathf.PI * 2f / segments), Mathf.Sin((i + 1) * Mathf.PI * 2f / segments));
                Triangle(mesh, center + a * radius, center + b * radius, center + a * (radius - thickness), tint);
                Triangle(mesh, center + a * (radius - thickness), center + b * radius, center + b * (radius - thickness), tint);
            }
        }

        private static void Line(VertexHelper mesh, Vector2 center, Vector2 direction, float length, float width, Color tint)
        {
            Vector2 normal = new Vector2(-direction.y, direction.x) * width * 0.5f;
            Vector2 end = direction * length;
            Triangle(mesh, center - end - normal, center - end + normal, center + end + normal, tint);
            Triangle(mesh, center - end - normal, center + end + normal, center + end - normal, tint);
        }
    }
}
