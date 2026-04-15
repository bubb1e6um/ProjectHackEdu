using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class MatrixRain : MonoBehaviour
{
    [Header("Layout")]
    [Range(30, 100)] public int columnCount = 55;
    [Range(10, 22)]  public int charFontSize = 15;

    [Header("Timing")]
    [Range(0.02f, 0.15f)] public float baseTickSec = 0.05f;

    private static readonly string Charset =
        "\u30A2\u30A4\u30A6\u30A8\u30AA\u30AB\u30AD\u30AF\u30B1\u30B3" +
        "\u30B5\u30B7\u30B9\u30BB\u30BD\u30BF\u30C1\u30C4\u30C6\u30C8" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@#$%^&*<>/\\|+-=[]{}";

    private Font   _font;
    private float  _cw, _rh;
    private int    _rows;

    private class Col
    {
        public Text[] cells;
        public int    head;
        public int    tailLen;
        public float  speedMult;
        public float  acc;
        public bool   sleeping;
        public float  sleepLeft;
    }

    private readonly List<Col> _cols = new List<Col>();

    void Awake()
    {
        var cv = GetComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 0;

        if (!TryGetComponent<CanvasScaler>(out _))
            gameObject.AddComponent<CanvasScaler>();

#if UNITY_EDITOR
        _font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/consolas.ttf");
#endif
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        Build();
    }

    void Build()
    {
        _cw   = (float)Screen.width  / columnCount;
        _rh   = charFontSize * 1.35f;
        _rows = Mathf.CeilToInt(Screen.height / _rh) + 3;

        for (int c = 0; c < columnCount; c++)
        {
            var col = new Col
            {
                cells     = new Text[_rows],
                tailLen   = Random.Range(7, 22),
                speedMult = Random.Range(0.45f, 2.8f),
                head      = -Random.Range(0, _rows)
            };

            for (int r = 0; r < _rows; r++)
            {
                var go = new GameObject($"rc_{c}_{r}");
                go.transform.SetParent(transform, false);

                var t = go.AddComponent<Text>();
                t.font          = _font;
                t.fontSize      = charFontSize;
                t.alignment     = TextAnchor.MiddleCenter;
                t.text          = RndCh();
                t.color         = Color.clear;
                t.raycastTarget = false;

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(_cw, _rh);
                rt.anchoredPosition = new Vector2(
                    c * _cw + _cw * 0.5f,
                    -r * _rh - _rh * 0.5f);

                col.cells[r] = t;
            }
            _cols.Add(col);
        }
    }

    void Update()
    {
        foreach (var col in _cols)
        {
            float interval = baseTickSec / col.speedMult;
            col.acc += Time.deltaTime;
            if (col.acc < interval) continue;
            col.acc -= interval;

            if (col.sleeping)
            {
                col.sleepLeft -= interval;
                if (col.sleepLeft <= 0f)
                {
                    col.sleeping = false;
                    col.head     = -col.tailLen;
                }
                continue;
            }

            col.head++;

            for (int r = 0; r < _rows; r++)
            {
                var cell = col.cells[r];
                if (Random.value < 0.09f) cell.text = RndCh();

                int dist = col.head - r;

                if (dist == 0)
                {
                    // Leading character — near-white glow
                    cell.color = new Color(0.82f, 1f, 0.82f, 1f);
                }
                else if (dist > 0 && dist < col.tailLen)
                {
                    float ratio = 1f - (float)dist / col.tailLen;
                    float g = Mathf.Lerp(0.08f, 0.85f, ratio);
                    float a = Mathf.Lerp(0.15f, 1f,    ratio);
                    cell.color = new Color(0f, g, 0f, a);
                }
                else
                {
                    cell.color = Color.clear;
                }
            }

            if (col.head > _rows + col.tailLen)
            {
                col.sleeping  = true;
                col.sleepLeft = Random.Range(0.3f, 5.5f);
                foreach (var c in col.cells) c.color = Color.clear;
            }
        }
    }

    string RndCh() => Charset[Random.Range(0, Charset.Length)].ToString();
}
