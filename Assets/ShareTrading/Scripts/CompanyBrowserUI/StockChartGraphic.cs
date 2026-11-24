using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class StockChartGraphic : MaskableGraphic
{
    [Header("모양")]
    [SerializeField] private float lineThickness = 2f;
    [SerializeField] private Color lineColor = new Color(0.92f, 0.95f, 1f, 1f);
    [SerializeField] private Color fillTopColor = new Color(0.72f, 0.82f, 0.95f, 0.45f);
    [SerializeField] private Color fillBottomColor = new Color(0.72f, 0.82f, 0.95f, 0f);

    [Header("축 선")]
    [SerializeField] private bool showAxisLines = true;
    [SerializeField] private float axisThickness = 1.5f;
    [SerializeField] private Color axisColor = new Color(0.85f, 0.9f, 1f, 0.6f);
    [SerializeField] private float plotGap = 6f;

    [Header("Y축 눈금 (Label0 = 맨 위 = 가장 큰 값)")]
    [SerializeField] private TMP_Text[] axisLabels;
    [SerializeField] private float minimumStep = 1f;
    [SerializeField] private float labelGap = 6f;

    [Header("기록이 없을 때 안내 (선택)")]
    [SerializeField] private TMP_Text emptyText;

    private readonly List<float> values = new List<float>();
    private float axisMin;
    private float axisMax = 1f;
    private bool hasData;

    public bool hasLine => values.Count >= 2;

    public void setValues(IReadOnlyList<float> source)
    {
        if (hasData && isSame(source))
        {
            return;
        }
        hasData = true;

        values.Clear();
        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                values.Add(source[i]);
            }
        }

        calculateAxis();
        updateLabels();
        SetVerticesDirty();
    }

    private bool isSame(IReadOnlyList<float> source)
    {
        int count = source != null ? source.Count : 0;
        if (count != values.Count)
        {
            return false;
        }
        for (int i = 0; i < count; i++)
        {
            if (!Mathf.Approximately(source[i], values[i]))
            {
                return false;
            }
        }
        return true;
    }

    private void calculateAxis()
    {
        int gridCount = axisLabels != null && axisLabels.Length >= 2 ? axisLabels.Length - 1 : 4;

        if (values.Count == 0)
        {
            axisMin = 0f;
            axisMax = minimumStep * gridCount;
            return;
        }

        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (float value in values)
        {
            min = Mathf.Min(min, value);
            max = Mathf.Max(max, value);
        }

        float range = max - min;
        float padding = range > 0f ? range * 0.1f : minimumStep;
        float paddedMin = Mathf.Max(0f, min - padding);
        float paddedMax = max + padding;

        float step = Mathf.Max(minimumStep, niceStep((paddedMax - paddedMin) / gridCount));
        for (int tries = 0; tries < 20; tries++)
        {
            axisMin = Mathf.Floor(paddedMin / step) * step;
            axisMax = axisMin + step * gridCount;
            if (axisMax >= paddedMax)
            {
                break;
            }
            step = nextNiceStep(step);
        }
    }

    private static float niceStep(float raw)
    {
        if (raw <= 0f)
        {
            return 1f;
        }
        float power = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raw)));
        float fraction = raw / power;
        float nice = fraction <= 1f ? 1f : fraction <= 2f ? 2f : fraction <= 5f ? 5f : 10f;
        return nice * power;
    }

    private static float nextNiceStep(float step)
    {
        return niceStep(step * 1.01f);
    }

    private void updateLabels()
    {
        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(!hasLine);
        }
        if (axisLabels == null || axisLabels.Length == 0)
        {
            return;
        }

        int last = axisLabels.Length - 1;
        for (int i = 0; i <= last; i++)
        {
            TMP_Text label = axisLabels[i];
            if (label == null)
            {
                continue;
            }

            float t = last > 0 ? 1f - (float)i / last : 0.5f;
            label.text = hasLine ? (axisMin + (axisMax - axisMin) * t).ToString("N0") : "";

            label.alignment = TextAlignmentOptions.Right;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;

            RectTransform rect = label.rectTransform;
            float height = rect.rect.height > 0f ? rect.rect.height : 28f;
            rect.anchorMin = new Vector2(0f, t);
            rect.anchorMax = new Vector2(1f, t);
            rect.offsetMin = new Vector2(0f, -height * 0.5f);
            rect.offsetMax = new Vector2(-labelGap, height * 0.5f);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();

        if (!hasLine)
        {
            addAxisLines(vh, rect);
            return;
        }

        float plotLeft = showAxisLines ? rect.xMin + axisThickness + plotGap : rect.xMin;
        float plotWidth = rect.xMax - plotLeft;
        float axisRange = Mathf.Max(axisMax - axisMin, 0.0001f);

        Vector2[] points = new Vector2[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            float x = plotLeft + plotWidth * i / (values.Count - 1);
            float y = rect.yMin + rect.height * Mathf.Clamp01((values[i] - axisMin) / axisRange);
            points[i] = new Vector2(x, y);
        }

        Color top = fillTopColor * color;
        Color bottom = fillBottomColor * color;
        for (int i = 0; i < points.Length - 1; i++)
        {
            int start = vh.currentVertCount;
            addVertex(vh, points[i], top);
            addVertex(vh, points[i + 1], top);
            addVertex(vh, new Vector2(points[i + 1].x, rect.yMin), bottom);
            addVertex(vh, new Vector2(points[i].x, rect.yMin), bottom);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        Color line = lineColor * color;
        for (int i = 0; i < points.Length - 1; i++)
        {
            addSegment(vh, points[i], points[i + 1], line);
        }

        addAxisLines(vh, rect);
    }

    private void addAxisLines(VertexHelper vh, Rect rect)
    {
        if (!showAxisLines)
        {
            return;
        }

        Color axis = axisColor * color;
        addQuad(vh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin + axisThickness, rect.yMax), axis);
        addQuad(vh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin + axisThickness), axis);
    }

    private static void addQuad(VertexHelper vh, Vector2 min, Vector2 max, Color quadColor)
    {
        int start = vh.currentVertCount;
        addVertex(vh, new Vector2(min.x, min.y), quadColor);
        addVertex(vh, new Vector2(min.x, max.y), quadColor);
        addVertex(vh, new Vector2(max.x, max.y), quadColor);
        addVertex(vh, new Vector2(max.x, min.y), quadColor);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }

    private void addSegment(VertexHelper vh, Vector2 from, Vector2 to, Color segmentColor)
    {
        Vector2 direction = (to - from).normalized;
        Vector2 extend = direction * (lineThickness * 0.5f);
        Vector2 normal = new Vector2(-direction.y, direction.x) * (lineThickness * 0.5f);
        from -= extend;
        to += extend;

        int start = vh.currentVertCount;
        addVertex(vh, from - normal, segmentColor);
        addVertex(vh, from + normal, segmentColor);
        addVertex(vh, to + normal, segmentColor);
        addVertex(vh, to - normal, segmentColor);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }

    private static void addVertex(VertexHelper vh, Vector2 position, Color vertexColor)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vh.AddVert(vertex);
    }
}
