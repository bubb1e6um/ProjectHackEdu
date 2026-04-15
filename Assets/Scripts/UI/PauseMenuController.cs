using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game pause menu prefab. Press Esc to open; press Esc again or click Shutdown to close.
/// Opens  → Time.timeScale = 0 (game frozen).
/// Closes → Time.timeScale = 1 (game resumed).
///
/// Does NOT open if something else has already frozen time (e.g. the terminal on Tab).
/// Attach to an empty GameObject, then save as a prefab and drop into any game scene.
/// Requires an EventSystem in the scene (the terminal/UI canvas usually provides one).
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    // ── Palette ───────────────────────────────────────────────────────────────
    static readonly Color CGreenBright  = new Color(0.00f, 1.00f, 0.25f, 1f);
    static readonly Color CGreenNormal  = new Color(0.00f, 0.80f, 0.15f, 1f);
    static readonly Color CGreenDim     = new Color(0.00f, 0.42f, 0.08f, 1f);
    static readonly Color CGreenVeryDim = new Color(0.00f, 0.22f, 0.04f, 1f);
    static readonly Color CPanelBg      = new Color(0.00f, 0.02f, 0.00f, 0.94f);
    static readonly Color CHeaderBg     = new Color(0.00f, 0.18f, 0.04f, 1f);
    static readonly Color CGreenDimBtn  = new Color(0.00f, 0.10f, 0.02f, 0.70f);

    // ── Phosphor colour schemes [scheme][0=bright 1=normal 2=dim 3=veryDim] ──
    static readonly Color[][] Phosphors =
    {
        new[]{ new Color(0.00f,1.00f,0.25f,1f), new Color(0.00f,0.80f,0.15f,1f),
               new Color(0.00f,0.42f,0.08f,1f), new Color(0.00f,0.22f,0.04f,1f) },
        new[]{ new Color(1.00f,0.75f,0.00f,1f), new Color(0.90f,0.58f,0.00f,1f),
               new Color(0.55f,0.28f,0.00f,1f), new Color(0.26f,0.12f,0.00f,1f) },
        new[]{ new Color(0.90f,0.95f,1.00f,1f), new Color(0.70f,0.76f,0.82f,1f),
               new Color(0.38f,0.42f,0.46f,1f), new Color(0.16f,0.18f,0.20f,1f) },
        new[]{ new Color(1.00f,0.22f,0.22f,1f), new Color(0.85f,0.08f,0.08f,1f),
               new Color(0.45f,0.04f,0.04f,1f), new Color(0.20f,0.02f,0.02f,1f) },
    };

    // ── Runtime state ─────────────────────────────────────────────────────────
    Text   _statusText;
    bool   _blinkOn;
    bool   _isPaused;
    Font   _uiFont;

    // Root panel that is toggled — kept separate so this MonoBehaviour's
    // Update() keeps running even when the menu is hidden.
    GameObject _pauseRoot;

    // ── Settings window ───────────────────────────────────────────────────────
    GameObject    _settingsOverlay;
    RectTransform _settingsPanel;
    int           _phosphorIdx = 0;
    int           _resIdx;

    float _masterVolume = 1.0f;
    float _sfxVolume    = 1.0f;
    float _musicVolume  = 1.0f;

    /// <summary>Assign in the Inspector once a music AudioSource exists in the scene.</summary>
    [Header("Audio — wire when ready")]
    public AudioSource MusicSource;

    readonly List<Text> _pBright = new List<Text>();
    readonly List<Text> _pNormal = new List<Text>();
    readonly List<Text> _pDim    = new List<Text>();

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        BuildUI();
        _pauseRoot.SetActive(false);
        StartCoroutine(BlinkCursor());
    }

    // =========================================================================
    //  Input
    // =========================================================================
    void Update()
    {
        // When the settings overlay is visible, only Esc passes through.
        if (_settingsOverlay != null && _settingsOverlay.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseSettings();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_isPaused)
                ClosePauseMenu();
            else if (Time.timeScale > 0f)   // don't open if something else already froze time
                OpenPauseMenu();
        }
    }

    // =========================================================================
    //  Open / Close
    // =========================================================================
    void OpenPauseMenu()
    {
        _isPaused = true;
        _pauseRoot.SetActive(true);
        Time.timeScale = 0f;
        SetStatus("// SYSTEM PAUSED \u2500 ALL PROCESSES SUSPENDED");
    }

    void ClosePauseMenu()
    {
        _isPaused = false;
        _pauseRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    // =========================================================================
    //  Button callbacks
    // =========================================================================
    void OnResume() => ClosePauseMenu();

    void OnMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    void OnShutdown()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void OnSettings()
    {
        if (_settingsOverlay == null) return;
        StartCoroutine(OpenSettingsAnim());
    }

    void CloseSettings() => StartCoroutine(CloseSettingsAnim());

    // =========================================================================
    //  Settings: apply
    // =========================================================================
    void ApplyPhosphor()
    {
        var p = Phosphors[_phosphorIdx];
        foreach (var t in _pBright) if (t) t.color = p[0];
        foreach (var t in _pNormal) if (t) t.color = p[1];
        foreach (var t in _pDim)    if (t) t.color = p[2];
    }

    void ApplyResolution(int idx)
    {
        var res = Screen.resolutions;
        if (res == null || res.Length == 0 || idx < 0 || idx >= res.Length) return;
        Screen.SetResolution(res[idx].width, res[idx].height, Screen.fullScreen);
    }

    string[] BuildResNames()
    {
        var res = Screen.resolutions;
        if (res == null || res.Length == 0)
            return new[] { $"{Screen.width}x{Screen.height}" };
        var names = new string[res.Length];
        for (int i = 0; i < res.Length; i++)
            names[i] = $"{res[i].width}x{res[i].height}";
        return names;
    }

    int FindCurRes()
    {
        var res = Screen.resolutions;
        if (res == null || res.Length == 0) return 0;
        for (int i = 0; i < res.Length; i++)
            if (res[i].width == Screen.width && res[i].height == Screen.height)
                return i;
        return Mathf.Max(0, res.Length - 1);
    }

    // =========================================================================
    //  Coroutines — all use UNSCALED time so they work when timeScale == 0
    // =========================================================================
    IEnumerator OpenSettingsAnim()
    {
        _settingsOverlay.SetActive(true);
        var cg = _settingsOverlay.GetComponent<CanvasGroup>();
        const float dur = 0.18f;
        for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
        {
            float ease = 1f - Mathf.Pow(1f - t / dur, 3f);
            cg.alpha = ease;
            _settingsPanel.localScale = Vector3.Lerp(new Vector3(0.86f, 0.86f, 1f), Vector3.one, ease);
            yield return null;
        }
        cg.alpha = 1f;
        _settingsPanel.localScale = Vector3.one;
    }

    IEnumerator CloseSettingsAnim()
    {
        var cg = _settingsOverlay.GetComponent<CanvasGroup>();
        const float dur = 0.14f;
        for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
        {
            float p = t / dur;
            cg.alpha = 1f - p * p;
            _settingsPanel.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.86f, 0.86f, 1f), p);
            yield return null;
        }
        cg.alpha = 0f;
        _settingsOverlay.SetActive(false);
    }

    IEnumerator PunchScale(RectTransform rt)
    {
        const float downDur = 0.07f, upDur = 0.13f, squish = 0.91f;
        Vector3 orig = rt.localScale, small = orig * squish;
        for (float t = 0; t < downDur; t += Time.unscaledDeltaTime)
        { rt.localScale = Vector3.Lerp(orig, small, t / downDur); yield return null; }
        rt.localScale = small;
        for (float t = 0; t < upDur; t += Time.unscaledDeltaTime)
        { rt.localScale = Vector3.Lerp(small, orig, t / upDur); yield return null; }
        rt.localScale = orig;
    }

    IEnumerator BlinkCursor()
    {
        while (true)
        {
            _blinkOn = !_blinkOn;
            if (_statusText) _statusText.color = _blinkOn ? CGreenDim : CGreenVeryDim;
            yield return new WaitForSecondsRealtime(0.6f);
        }
    }

    // =========================================================================
    //  UI construction
    // =========================================================================
    void BuildUI()
    {
        // Canvas lives on this GameObject; it stays active so Update runs.
        var cv = gameObject.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 20;   // above terminal (10) and other UI
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        Font font = null;
#if UNITY_EDITOR
        font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/consolas.ttf");
#endif
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _uiFont = font;

        // ── Root panel (toggled on/off) ──────────────────────────────────────
        var rootGo = new GameObject("PauseRoot");
        rootGo.transform.SetParent(transform, false);
        _pauseRoot = rootGo;

        // Darkened screen overlay
        var overlay = MakePanel("Overlay", rootGo.transform, new Color(0f, 0f, 0f, 0.78f));
        Stretch(overlay.GetRT());

        // ── Terminal window ──────────────────────────────────────────────────
        const float W = 720f, H = 440f;
        var win = MakePanel("TerminalWindow", rootGo.transform, CPanelBg);
        var winRT = win.GetRT();
        winRT.anchorMin = winRT.anchorMax = winRT.pivot = new Vector2(0.5f, 0.5f);
        winRT.sizeDelta = new Vector2(W, H);
        win.Go.AddComponent<Outline>().effectColor    = CGreenNormal;
        win.Go.GetComponent<Outline>().effectDistance = new Vector2(2f, 2f);

        // Header
        var hdr = MakePanel("Header", win.Tr, CHeaderBg);
        var hdrRT = hdr.GetRT();
        hdrRT.anchorMin = new Vector2(0, 1); hdrRT.anchorMax = new Vector2(1, 1);
        hdrRT.pivot     = new Vector2(0.5f, 1f);
        hdrRT.offsetMin = new Vector2(0, -46); hdrRT.offsetMax = Vector2.zero;

        var titleT = MakeText("Title", hdr.Tr, font,
            "[ HACKEDU OS v2.1.0 ]  \u2500\u2500  SESSION PAUSED  \u2500\u2500  [STANDBY]",
            14, CGreenBright, TextAnchor.MiddleCenter);
        titleT.Stretch();
        _pBright.Add(titleT.Comp);

        // Sys-info line
        var sysT = MakeText("SysInfo", win.Tr, font,
            "user@hackedu:~   \u2502   addr: 127.0.0.1   \u2502   state: PAUSED   \u2502   [ESC] to resume",
            11, CGreenDim, TextAnchor.MiddleLeft);
        AnchorTop(sysT.RT, 22, -84, -50);
        _pDim.Add(sysT.Comp);

        MakeHRule(win.Tr, -92, CGreenVeryDim);

        // Hint line
        var hintT = MakeText("Hint", win.Tr, font,
            "  press: [ESC] resume  \u2502  mainmenu  \u2502  settings  \u2502  shutdown",
            11, CGreenVeryDim, TextAnchor.MiddleLeft);
        AnchorTop(hintT.RT, 22, -118, -94);

        MakeHRule(win.Tr, -126, CGreenVeryDim);

        // ── Buttons ──────────────────────────────────────────────────────────
        //  centreY values measured from window centre (positive = up)
        float[] btnY = { 72f, 14f, -44f, -102f };

        MakeButton(win.Tr, font,
            "  \u25B6  [   RESUME   ]     \u2500  CONTINUE CURRENT SESSION",
            btnY[0], OnResume);

        MakeButton(win.Tr, font,
            "  \u25B6  [ MAIN  MENU ]     \u2500  EXIT TO MAIN TERMINAL",
            btnY[1], OnMainMenu);

        MakeButton(win.Tr, font,
            "  \u25B6  [  SETTINGS  ]     \u2500  CONFIGURE SYSTEM PARAMETERS",
            btnY[2], OnSettings);

        MakeButton(win.Tr, font,
            "  \u25B6  [  SHUTDOWN  ]     \u2500  TERMINATE ALL PROCESSES",
            btnY[3], OnShutdown);

        MakeHRule(win.Tr, -156, CGreenVeryDim);

        // Status bar
        var statusT = MakeText("Status", win.Tr, font,
            "// SYSTEM PAUSED \u2500 ALL PROCESSES SUSPENDED",
            11, CGreenDim, TextAnchor.MiddleLeft);
        statusT.RT.anchorMin = new Vector2(0, 0); statusT.RT.anchorMax = new Vector2(1, 0);
        statusT.RT.pivot     = new Vector2(0.5f, 0f);
        statusT.RT.offsetMin = new Vector2(22, 14); statusT.RT.offsetMax = new Vector2(-22, 38);
        _statusText = statusT.Comp;
        _pDim.Add(_statusText);

        BuildSettingsWindow(font);
    }

    // =========================================================================
    //  Settings window construction (identical layout to MainMenuController)
    // =========================================================================
    void BuildSettingsWindow(Font font)
    {
        const float W     = 560f;
        const float H     = 380f;
        const float HDR_H = 36f;
        const float PAD   = 22f;
        const float ROW_H = 30f;
        const float SEC_H = 26f;
        const float SPLIT = 220f;

        var dimGo = new GameObject("SettingsOverlay");
        dimGo.transform.SetParent(_pauseRoot.transform, false);
        var dimImg = dimGo.AddComponent<Image>();
        dimImg.color         = new Color(0f, 0f, 0f, 0.55f);
        dimImg.raycastTarget = true;
        Stretch(dimGo.GetComponent<RectTransform>());
        var cg = dimGo.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        var win = MakePanel("SettingsWindow", dimGo.transform, CPanelBg);
        win.Go.GetComponent<Image>().raycastTarget = true;
        var winRT = win.GetRT();
        winRT.anchorMin = winRT.anchorMax = winRT.pivot = new Vector2(0.5f, 0.5f);
        winRT.sizeDelta = new Vector2(W, H);
        win.Go.AddComponent<Outline>().effectColor    = CGreenNormal;
        win.Go.GetComponent<Outline>().effectDistance = new Vector2(2f, 2f);

        var hdr = MakePanel("SHdr", win.Tr, CHeaderBg);
        var hdrRT = hdr.GetRT();
        hdrRT.anchorMin = new Vector2(0, 1); hdrRT.anchorMax = new Vector2(1, 1);
        hdrRT.pivot     = new Vector2(0.5f, 1f);
        hdrRT.offsetMin = new Vector2(0, -HDR_H); hdrRT.offsetMax = Vector2.zero;
        MakeText("STitle", hdr.Tr, font,
            "[ SYSTEM CONFIGURATION ]", 13, CGreenBright, TextAnchor.MiddleCenter).Stretch();

        // Close (✕) button
        var xGo  = new GameObject("CloseBtn");
        xGo.transform.SetParent(hdr.Tr, false);
        var xImg = xGo.AddComponent<Image>();
        xImg.color = new Color(0.00f, 0.28f, 0.06f, 0.90f);
        var xRT  = xGo.GetComponent<RectTransform>();
        xRT.anchorMin = xRT.anchorMax = xRT.pivot = new Vector2(1f, 0.5f);
        xRT.sizeDelta        = new Vector2(28f, 22f);
        xRT.anchoredPosition = new Vector2(-6f, 0f);
        var xBtn  = xGo.AddComponent<Button>();
        xBtn.targetGraphic = xImg;
        var xCols = xBtn.colors;
        xCols.normalColor      = Color.white;
        xCols.highlightedColor = new Color(0f, 3.5f, 0.5f, 1f);
        xCols.pressedColor     = new Color(0f, 6f,   0.8f, 1f);
        xCols.fadeDuration     = 0.07f;
        xBtn.colors = xCols;
        xBtn.onClick.AddListener(CloseSettings);
        var xLbl = new GameObject("X");
        xLbl.transform.SetParent(xGo.transform, false);
        var xTxt = xLbl.AddComponent<Text>();
        xTxt.font = font; xTxt.fontSize = 13; xTxt.text = "\u2715";
        xTxt.color = CGreenNormal; xTxt.alignment = TextAnchor.MiddleCenter; xTxt.raycastTarget = false;
        var xLblRT = xLbl.GetComponent<RectTransform>();
        xLblRT.anchorMin = Vector2.zero; xLblRT.anchorMax = Vector2.one;
        xLblRT.offsetMin = xLblRT.offsetMax = Vector2.zero;

        float y = HDR_H + 8f;

        y = SWinSection(win.Tr, font, "DISPLAY", y, W, PAD, SEC_H);

        _resIdx = FindCurRes();
        y = SWinCycle(win.Tr, font, "Resolution", y,
            BuildResNames(), _resIdx, ROW_H, PAD, SPLIT, W,
            i => ApplyResolution(i));

        y = SWinCycle(win.Tr, font, "Phosphor Color", y,
            new[] { "Green", "Amber", "White", "Red" }, _phosphorIdx, ROW_H, PAD, SPLIT, W,
            i => { _phosphorIdx = i; ApplyPhosphor(); });

        y += 10f;

        y = SWinSection(win.Tr, font, "AUDIO", y, W, PAD, SEC_H);

        y = SWinStep(win.Tr, font, "Master Volume", y,
            _masterVolume, 0f, 1f, 0.1f, ROW_H, PAD, SPLIT, W,
            v => { _masterVolume = v; AudioListener.volume = v; });

        y = SWinStep(win.Tr, font, "SFX Volume", y,
            _sfxVolume, 0f, 1f, 0.1f, ROW_H, PAD, SPLIT, W,
            v => { _sfxVolume = v; AudioManager.Instance?.SetSfxVolume(v); });

        y = SWinStep(win.Tr, font, "Music Volume", y,
            _musicVolume, 0f, 1f, 0.1f, ROW_H, PAD, SPLIT, W,
            v => { _musicVolume = v; AudioManager.Instance?.SetMusicVolume(v); });

        _settingsOverlay = dimGo;
        _settingsPanel   = winRT;
        dimGo.SetActive(false);
    }

    void SetStatus(string msg) { if (_statusText) _statusText.text = msg; }

    // ── Settings row helpers ──────────────────────────────────────────────────

    float SWinSection(Transform parent, Font font, string title,
        float y, float winW, float pad, float secH)
    {
        MakeHRule(parent, -y, CGreenVeryDim);
        y += 4f;
        var t = MakeText($"Sec_{title}", parent, font,
            $"\u2500\u2500 {title} ", 10, CGreenDim, TextAnchor.MiddleLeft);
        t.RT.anchorMin = t.RT.anchorMax = new Vector2(0f, 1f);
        t.RT.pivot = new Vector2(0f, 1f);
        t.RT.sizeDelta        = new Vector2(winW - pad * 2f, secH);
        t.RT.anchoredPosition = new Vector2(pad, -y);
        return y + secH + 4f;
    }

    float SWinCycle(Transform parent, Font font, string label, float y,
        string[] options, int initial,
        float rowH, float pad, float split, float winW,
        System.Action<int> onChange)
    {
        var lbl = MakeText($"L_{label}", parent, font, label, 12, CGreenDim, TextAnchor.MiddleLeft);
        lbl.RT.anchorMin = lbl.RT.anchorMax = new Vector2(0f, 1f);
        lbl.RT.pivot = new Vector2(0f, 1f);
        lbl.RT.sizeDelta        = new Vector2(split - pad - 8f, rowH);
        lbl.RT.anchoredPosition = new Vector2(pad, -y);

        float ctrlX = split, ctrlW = winW - split - pad;
        const float BTN_W = 26f;
        int idx = Mathf.Clamp(initial, 0, options.Length - 1);

        var prevGo  = new GameObject($"Prev_{label}");
        prevGo.transform.SetParent(parent, false);
        var prevImg = prevGo.AddComponent<Image>(); prevImg.color = CGreenDimBtn;
        var prevRT  = prevGo.GetComponent<RectTransform>();
        prevRT.anchorMin = prevRT.anchorMax = new Vector2(0f, 1f); prevRT.pivot = new Vector2(0f, 1f);
        prevRT.sizeDelta = new Vector2(BTN_W, rowH - 4f);
        prevRT.anchoredPosition = new Vector2(ctrlX, -(y + 2f));
        var pT = new GameObject("T"); pT.transform.SetParent(prevGo.transform, false);
        var pTxt = pT.AddComponent<Text>();
        pTxt.font = font; pTxt.fontSize = 13; pTxt.text = "<";
        pTxt.color = CGreenNormal; pTxt.alignment = TextAnchor.MiddleCenter; pTxt.raycastTarget = false;
        Stretch(pT.GetComponent<RectTransform>());

        var valGo  = new GameObject($"Val_{label}");
        valGo.transform.SetParent(parent, false);
        var valImg = valGo.AddComponent<Image>(); valImg.color = Color.clear; valImg.raycastTarget = false;
        var valRT  = valGo.GetComponent<RectTransform>();
        valRT.anchorMin = valRT.anchorMax = new Vector2(0f, 1f); valRT.pivot = new Vector2(0f, 1f);
        valRT.sizeDelta = new Vector2(ctrlW - BTN_W * 2f, rowH - 4f);
        valRT.anchoredPosition = new Vector2(ctrlX + BTN_W, -(y + 2f));
        var vT = new GameObject("T"); vT.transform.SetParent(valGo.transform, false);
        var valTxt = vT.AddComponent<Text>();
        valTxt.font = font; valTxt.fontSize = 12; valTxt.text = options[idx];
        valTxt.color = CGreenBright; valTxt.alignment = TextAnchor.MiddleCenter; valTxt.raycastTarget = false;
        Stretch(vT.GetComponent<RectTransform>());

        var nextGo  = new GameObject($"Next_{label}");
        nextGo.transform.SetParent(parent, false);
        var nextImg = nextGo.AddComponent<Image>(); nextImg.color = CGreenDimBtn;
        var nextRT  = nextGo.GetComponent<RectTransform>();
        nextRT.anchorMin = nextRT.anchorMax = new Vector2(0f, 1f); nextRT.pivot = new Vector2(0f, 1f);
        nextRT.sizeDelta = new Vector2(BTN_W, rowH - 4f);
        nextRT.anchoredPosition = new Vector2(ctrlX + ctrlW - BTN_W, -(y + 2f));
        var nT = new GameObject("T"); nT.transform.SetParent(nextGo.transform, false);
        var nTxt = nT.AddComponent<Text>();
        nTxt.font = font; nTxt.fontSize = 13; nTxt.text = ">";
        nTxt.color = CGreenNormal; nTxt.alignment = TextAnchor.MiddleCenter; nTxt.raycastTarget = false;
        Stretch(nT.GetComponent<RectTransform>());

        var prevBtn = prevGo.AddComponent<Button>();
        prevBtn.targetGraphic = prevImg; ApplyCycleColors(prevBtn);
        prevBtn.onClick.AddListener(() => { idx = (idx - 1 + options.Length) % options.Length; valTxt.text = options[idx]; onChange(idx); });

        var nextBtn = nextGo.AddComponent<Button>();
        nextBtn.targetGraphic = nextImg; ApplyCycleColors(nextBtn);
        nextBtn.onClick.AddListener(() => { idx = (idx + 1) % options.Length; valTxt.text = options[idx]; onChange(idx); });

        return y + rowH + 2f;
    }

    float SWinStep(Transform parent, Font font, string label, float y,
        float initial, float minVal, float maxVal, float step,
        float rowH, float pad, float split, float winW,
        System.Action<float> onChange)
    {
        var lbl = MakeText($"L_{label}", parent, font, label, 12, CGreenDim, TextAnchor.MiddleLeft);
        lbl.RT.anchorMin = lbl.RT.anchorMax = new Vector2(0f, 1f);
        lbl.RT.pivot = new Vector2(0f, 1f);
        lbl.RT.sizeDelta        = new Vector2(split - pad - 8f, rowH);
        lbl.RT.anchoredPosition = new Vector2(pad, -y);

        float ctrlX = split, ctrlW = winW - split - pad;
        const float BTN_W = 28f;
        float cur = Mathf.Clamp(initial, minVal, maxVal);

        var minusGo  = new GameObject($"Minus_{label}");
        minusGo.transform.SetParent(parent, false);
        var minusImg = minusGo.AddComponent<Image>(); minusImg.color = CGreenDimBtn;
        var minusRT  = minusGo.GetComponent<RectTransform>();
        minusRT.anchorMin = minusRT.anchorMax = new Vector2(0f, 1f); minusRT.pivot = new Vector2(0f, 1f);
        minusRT.sizeDelta = new Vector2(BTN_W, rowH - 4f);
        minusRT.anchoredPosition = new Vector2(ctrlX, -(y + 2f));
        var mL = new GameObject("T"); mL.transform.SetParent(minusGo.transform, false);
        var mTxt = mL.AddComponent<Text>();
        mTxt.font = font; mTxt.fontSize = 15; mTxt.text = "\u2212";
        mTxt.color = CGreenNormal; mTxt.alignment = TextAnchor.MiddleCenter; mTxt.raycastTarget = false;
        Stretch(mL.GetComponent<RectTransform>());

        var valGo  = new GameObject($"Val_{label}");
        valGo.transform.SetParent(parent, false);
        var valImg = valGo.AddComponent<Image>(); valImg.color = Color.clear; valImg.raycastTarget = false;
        var valRT  = valGo.GetComponent<RectTransform>();
        valRT.anchorMin = valRT.anchorMax = new Vector2(0f, 1f); valRT.pivot = new Vector2(0f, 1f);
        valRT.sizeDelta = new Vector2(ctrlW - BTN_W * 2f, rowH - 4f);
        valRT.anchoredPosition = new Vector2(ctrlX + BTN_W, -(y + 2f));
        var vL = new GameObject("T"); vL.transform.SetParent(valGo.transform, false);
        var vTxt = vL.AddComponent<Text>();
        vTxt.font = font; vTxt.fontSize = 12; vTxt.text = FormatPct(cur);
        vTxt.color = CGreenBright; vTxt.alignment = TextAnchor.MiddleCenter; vTxt.raycastTarget = false;
        Stretch(vL.GetComponent<RectTransform>());

        var plusGo  = new GameObject($"Plus_{label}");
        plusGo.transform.SetParent(parent, false);
        var plusImg = plusGo.AddComponent<Image>(); plusImg.color = CGreenDimBtn;
        var plusRT  = plusGo.GetComponent<RectTransform>();
        plusRT.anchorMin = plusRT.anchorMax = new Vector2(0f, 1f); plusRT.pivot = new Vector2(0f, 1f);
        plusRT.sizeDelta = new Vector2(BTN_W, rowH - 4f);
        plusRT.anchoredPosition = new Vector2(ctrlX + ctrlW - BTN_W, -(y + 2f));
        var pL = new GameObject("T"); pL.transform.SetParent(plusGo.transform, false);
        var pTxt2 = pL.AddComponent<Text>();
        pTxt2.font = font; pTxt2.fontSize = 14; pTxt2.text = "+";
        pTxt2.color = CGreenNormal; pTxt2.alignment = TextAnchor.MiddleCenter; pTxt2.raycastTarget = false;
        Stretch(pL.GetComponent<RectTransform>());

        var minusBtn = minusGo.AddComponent<Button>();
        minusBtn.targetGraphic = minusImg; ApplyCycleColors(minusBtn);
        minusBtn.onClick.AddListener(() =>
        {
            cur = Mathf.Round(Mathf.Clamp(cur - step, minVal, maxVal) / step) * step;
            vTxt.text = FormatPct(cur); onChange(cur);
        });

        var plusBtn = plusGo.AddComponent<Button>();
        plusBtn.targetGraphic = plusImg; ApplyCycleColors(plusBtn);
        plusBtn.onClick.AddListener(() =>
        {
            cur = Mathf.Round(Mathf.Clamp(cur + step, minVal, maxVal) / step) * step;
            vTxt.text = FormatPct(cur); onChange(cur);
        });

        return y + rowH + 2f;
    }

    static string FormatPct(float v) => $"{Mathf.RoundToInt(v * 100f)}%";

    void ApplyCycleColors(Button btn)
    {
        var c = btn.colors;
        c.normalColor      = Color.white;
        c.highlightedColor = new Color(3f, 3f, 3f, 1f);
        c.pressedColor     = new Color(5f, 5f, 5f, 1f);
        c.fadeDuration     = 0.07f;
        btn.colors = c;
    }

    // =========================================================================
    //  UI helpers
    // =========================================================================
    struct PanelRef
    {
        public GameObject    Go;
        public Transform     Tr;
        public RectTransform GetRT() => Go.GetComponent<RectTransform>();
    }

    PanelRef MakePanel(string name, Transform parent, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
        return new PanelRef { Go = go, Tr = go.transform };
    }

    struct TextRef
    {
        public Text          Comp;
        public RectTransform RT;
        public void Stretch(Vector2 min = default, Vector2 max = default)
        {
            RT.anchorMin = Vector2.zero; RT.anchorMax = Vector2.one;
            RT.offsetMin = min;          RT.offsetMax = max;
        }
    }

    TextRef MakeText(string name, Transform parent, Font font,
        string text, int size, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font               = font;
        t.fontSize           = size;
        t.text               = text;
        t.color              = color;
        t.alignment          = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget      = false;
        return new TextRef { Comp = t, RT = go.GetComponent<RectTransform>() };
    }

    void MakeHRule(Transform parent, float yOffset, Color color)
    {
        var rule = MakePanel($"HRule_{yOffset}", parent, color);
        var rt   = rule.GetRT();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(10f, yOffset - 1f);
        rt.offsetMax = new Vector2(-10f, yOffset);
    }

    static void AnchorTop(RectTransform rt, float xMargin, float yBottom, float yTop)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(xMargin, yBottom);
        rt.offsetMax = new Vector2(-xMargin, yTop);
    }

    void MakeButton(Transform parent, Font font, string label,
        float centerY, System.Action onClick)
    {
        const float BTN_W = 660f, BTN_H = 46f;

        var go  = new GameObject($"Btn_{centerY}");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.color = CGreenDimBtn;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(BTN_W, BTN_H);
        rt.anchoredPosition = new Vector2(0f, centerY);
        go.AddComponent<Outline>().effectColor    = CGreenDim;
        go.GetComponent<Outline>().effectDistance = new Vector2(1f, 1f);

        var lbl = new GameObject("Label");
        lbl.transform.SetParent(go.transform, false);
        var txt = lbl.AddComponent<Text>();
        txt.font               = font;
        txt.fontSize           = 14;
        txt.text               = label;
        txt.color              = CGreenNormal;
        txt.alignment          = TextAnchor.MiddleLeft;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.raycastTarget      = false;
        var lblRT = lbl.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = new Vector2(18, 0); lblRT.offsetMax = new Vector2(-18, 0);

        var btn  = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cols = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(3.5f, 3.5f, 3.5f, 1f);
        cols.pressedColor     = new Color(5f,   5f,   5f,   1f);
        cols.fadeDuration     = 0.06f;
        btn.colors = cols;
        var capturedRt = rt;
        btn.onClick.AddListener(() =>
        {
            txt.color = CGreenBright;
            StartCoroutine(PunchScale(capturedRt));
            onClick();
        });

        var et    = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var enter = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => txt.color = CGreenBright);
        var exit  = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => txt.color = CGreenNormal);
        et.triggers.Add(enter);
        et.triggers.Add(exit);
    }

    static void Stretch(RectTransform rt,
        Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
