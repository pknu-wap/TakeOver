using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StartupLineGraph : MaskableGraphic
    {
        [SerializeField] private float lineThickness = 1.35f;
        [SerializeField] private float gridThickness = 1.35f;
        [SerializeField] private Color gridColor = new Color32(186, 186, 186, 255);
        [SerializeField] private Color lineColor = new Color32(36, 72, 126, 255);
        [SerializeField] private Color fillTop = new Color32(180, 228, 255, 255);
        [SerializeField] private Color fillBottom = new Color32(239, 249, 254, 255);

        private readonly List<float> values = new List<float>();
        private readonly List<float> ticks = new List<float>();
        private float minimum;
        private float maximum;

        public int pointCount => values.Count;
        public IReadOnlyList<float> TickValues => ticks;

        public void setValues(IReadOnlyList<float> source)
        {
            values.Clear();
            if (source != null)
                for (int i = 0; i < source.Count && i < 9; i++) values.Add(source[i]);
            calculateTicks();
            SetVerticesDirty();
        }

        private void calculateTicks()
        {
            ticks.Clear();
            if (values.Count < 2) return;

            float low = values[0];
            float high = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                low = Mathf.Min(low, values[i]);
                high = Mathf.Max(high, values[i]);
            }
            if (Mathf.Approximately(low, high))
            {
                low -= Mathf.Max(1f, Mathf.Abs(low) * 0.05f);
                high += Mathf.Max(1f, Mathf.Abs(high) * 0.05f);
            }

            float rawStep = (high - low) / 4f;
            float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(Mathf.Max(rawStep, 0.0001f))));
            float normalized = rawStep / magnitude;
            float nice = normalized <= 1f ? 1f : normalized <= 2f ? 2f : normalized <= 5f ? 5f : 10f;
            float step = nice * magnitude;
            minimum = Mathf.Floor(low / step) * step;
            maximum = minimum + step * 4f;
            while (maximum < high)
            {
                step *= 2f;
                minimum = Mathf.Floor(low / step) * step;
                maximum = minimum + step * 4f;
            }
            for (int i = 0; i <= 4; i++) ticks.Add(minimum + step * i);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (ticks.Count != 5) return;

            Rect rect = GetPixelAdjustedRect();
            if (values.Count >= 2)
            {
                Vector2 areaPrevious = toPoint(0, rect);
                for (int i = 1; i < values.Count; i++)
                {
                    Vector2 current = toPoint(i, rect);
                    addArea(vh, areaPrevious, current, rect.yMin);
                    areaPrevious = current;
                }
            }

            for (int i = 0; i < ticks.Count; i++)
            {
                float y = rect.yMin + rect.height * i / 4f;
                addQuad(vh, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y),
                    gridThickness, gridColor);
            }
            if (values.Count < 2) return;

            Vector2 previous = toPoint(0, rect);
            for (int i = 1; i < values.Count; i++)
            {
                Vector2 current = toPoint(i, rect);
                addSegment(vh, previous, current);
                previous = current;
            }

            for (int i = 0; i < values.Count; i++)
                addMarker(vh, toPoint(i, rect), 2.6f, 1.3f);
        }

        private Vector2 toPoint(int index, Rect rect)
        {
            float x = rect.xMin + rect.width * index / (values.Count - 1);
            float y = rect.yMin + rect.height * Mathf.Clamp01((values[index] - minimum) / (maximum - minimum));
            return new Vector2(x, y);
        }

        private static void addQuad(VertexHelper vh, Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 direction = to - from;
            Vector2 normal = direction.sqrMagnitude < 0.0001f
                ? new Vector2(0f, thickness * 0.5f)
                : new Vector2(-direction.y, direction.x).normalized * (thickness * 0.5f);
            addColorQuad(vh, from - normal, from + normal, to + normal, to - normal, color);
        }

        private void addSegment(VertexHelper vh, Vector2 from, Vector2 to)
        {
            Vector2 direction = to - from;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (lineThickness * 0.5f);
            addColorQuad(vh, from - normal, from + normal, to + normal, to - normal, lineColor);
        }

        private void addArea(VertexHelper vh, Vector2 from, Vector2 to, float bottom)
        {
            int start = vh.currentVertCount;
            addVert(vh, new Vector2(from.x, bottom), fillBottom);
            addVert(vh, from, fillTop);
            addVert(vh, to, fillTop);
            addVert(vh, new Vector2(to.x, bottom), fillBottom);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private void addMarker(VertexHelper vh, Vector2 center, float radius, float border)
        {
            const int segments = 24;
            float innerRadius = Mathf.Max(0f, radius - border);
            int outerStart = vh.currentVertCount;
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                addVert(vh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, lineColor);
            }
            int innerBorderStart = vh.currentVertCount;
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                addVert(vh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * innerRadius, lineColor);
            }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                vh.AddTriangle(outerStart + i, outerStart + next, innerBorderStart + next);
                vh.AddTriangle(outerStart + i, innerBorderStart + next, innerBorderStart + i);
            }
            int innerFillStart = vh.currentVertCount;
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                addVert(vh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * innerRadius, Color.white);
            }
            int centerIndex = vh.currentVertCount;
            addVert(vh, center, Color.white);
            for (int i = 0; i < segments; i++)
                vh.AddTriangle(innerFillStart + i, innerFillStart + (i + 1) % segments, centerIndex);
        }

        private static void addColorQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = vh.currentVertCount;
            addVert(vh, a, color); addVert(vh, b, color); addVert(vh, c, color); addVert(vh, d, color);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        private static void addVert(VertexHelper vh, Vector2 position, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            vh.AddVert(vertex);
        }
    }
}