using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations
{
    public sealed class StartupLineGraph : MaskableGraphic
    {
        [SerializeField] private float thickness = 3f;
        private readonly List<float> values = new List<float>();

        public int pointCount => values.Count;

        public void setValues(IReadOnlyList<float> source)
        {
            values.Clear();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++) values.Add(source[i]);
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (values.Count < 2) return;

            Rect rect = GetPixelAdjustedRect();
            float max = 0f;
            foreach (float value in values) max = Mathf.Max(max, value);
            if (max <= 0f) max = 1f;
            max *= 1.15f;

            Vector2 previous = toPoint(0, rect, max);
            for (int i = 1; i < values.Count; i++)
            {
                Vector2 current = toPoint(i, rect, max);
                addSegment(vh, previous, current);
                previous = current;
            }
        }

        private Vector2 toPoint(int index, Rect rect, float max)
        {
            float x = rect.xMin + rect.width * index / (values.Count - 1);
            float y = rect.yMin + rect.height * Mathf.Clamp01(values[index] / max);
            return new Vector2(x, y);
        }

        private void addSegment(VertexHelper vh, Vector2 from, Vector2 to)
        {
            Vector2 direction = to - from;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (thickness * 0.5f);

            int start = vh.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = from - normal; vh.AddVert(vertex);
            vertex.position = from + normal; vh.AddVert(vertex);
            vertex.position = to + normal; vh.AddVert(vertex);
            vertex.position = to - normal; vh.AddVert(vertex);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
