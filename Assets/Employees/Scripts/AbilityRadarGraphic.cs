using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Employees
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class AbilityRadarGraphic : MaskableGraphic
    {
        [SerializeField] private int[] values = new int[5];
        [SerializeField] private TextMeshProUGUI[] axisLabels = new TextMeshProUGUI[5];
        private static readonly string[] Names = { "분석", "인맥", "안목", "발품", "관리" };
        private static readonly Color Grid = new Color32(186, 186, 186, 255);
        private static readonly Color Navy = new Color32(36, 72, 126, 255);

        public void SetLabels(TextMeshProUGUI[] labels)
        {
            axisLabels = labels;
            SetValues(values);
        }

        public void SetValues(int[] abilities, int[] comparison = null)
        {
            for (int i = 0; i < 5; i++)
            {
                values[i] = Mathf.Clamp(abilities != null && i < abilities.Length ? abilities[i] : 0, 0, 20);
                if (axisLabels == null || i >= axisLabels.Length || axisLabels[i] == null) continue;
                string hex = "24487E";
                if (comparison != null && i < comparison.Length)
                    hex = values[i] > comparison[i] ? "C92121" : values[i] < comparison[i] ? "3A7FE7" : hex;
                axisLabels[i].text = Names[i] + " <color=#" + hex + ">" + values[i] + "</color>";
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            Vector2[] corners = new Vector2[5];
            Vector2[] points = new Vector2[5];
            for (int i = 0; i < 5; i++)
            {
                float angle = (90f - i * 72f) * Mathf.Deg2Rad;
                corners[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                points[i] = Vector2.Lerp(center, corners[i], Mathf.Clamp01(values[i] / 20f));
            }
            Polygon(vh, center, corners, new Color32(237, 237, 237, 255));
            for (int ring = 1; ring <= 4; ring++)
                for (int i = 0; i < 5; i++)
                    Line(vh, Vector2.Lerp(center, corners[i], ring / 4f),
                        Vector2.Lerp(center, corners[(i + 1) % 5], ring / 4f), 2, Grid);
            for (int i = 0; i < 5; i++) Line(vh, center, corners[i], 2, Grid);
            Polygon(vh, center, points, new Color32(46, 98, 151, 204));
            for (int i = 0; i < 5; i++)
            {
                Line(vh, points[i], points[(i + 1) % 5], 2, new Color32(17, 34, 61, 255));
                Disc(vh, corners[i], 4.76f, Navy);
                Disc(vh, corners[i], 2.76f, Color.white);
            }
        }

        private static void Vertex(VertexHelper vh, Vector2 p, Color c)
        {
            var v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            vh.AddVert(v);
        }

        private static void Polygon(VertexHelper vh, Vector2 center, Vector2[] points, Color color)
        {
            int start = vh.currentVertCount;
            Vertex(vh, center, color);
            for (int i = 0; i < points.Length; i++) Vertex(vh, points[i], color);
            for (int i = 0; i < points.Length; i++)
                vh.AddTriangle(start, start + i + 1, start + (i + 1) % points.Length + 1);
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            int start = vh.currentVertCount;
            Vertex(vh, a - normal, color);
            Vertex(vh, a + normal, color);
            Vertex(vh, b + normal, color);
            Vertex(vh, b - normal, color);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Disc(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            var points = new Vector2[24];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / points.Length;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            Polygon(vh, center, points, color);
        }
    }
}
