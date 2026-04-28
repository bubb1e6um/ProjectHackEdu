using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Terminal-style main menu. Attach to an empty GameObject in the MainMenu scene.
// Builds the full UI programmatically on Awake — no prefab or hierarchy setup required.
public class MainMenuController : MonoBehaviour
{
    static readonly Color CGreenBright  = new Color(0.00f, 1.00f, 0.25f, 1f);
    static readonly Color CGreenNormal  = new Color(0.00f, 0.80f, 0.15f, 1f);
    static readonly Color CGreenDim     = new Color(0.00f, 0.42f, 0.08f, 1f);
    static readonly Color CGreenVeryDim = new Color(0.00f, 0.22f, 0.04f, 1f);
    static readonly Color CPanelBg      = new Color(0.00f, 0.02f, 0.00f, 0.94f);
    static readonly Color CHeaderBg     = new Color(0.00f, 0.18f, 0.04f, 1f);
    static readonly Color CGreenDimBtn  = new Color(0.00f, 0.10f, 0.02f, 0.70f);

    // Phosphor colour palettes — [scheme][level], levels: 0=bright 1=normal 2=dim 3=veryDim
    static readonly Color[][] Phosphors =
    {
        // 0 — Green (default)
        new[]{ new Color(0.00f,1.00f,0.25f,1f), new Color(0.00f,0.80f,0.15f,1f),
               new Color(0.00f,0.42f,0.08f,1f), new Color(0.00f,0.22f,0.04f,1f) },
        // 1 — Amber
        new[]{ new Color(1.00f,0.75f,0.00f,1f), new Color(0.90f,0.58f,0.00f,1f),
               new Color(0.55f,0.28f,0.00f,1f), new Color(0.26f,0.12f,0.00f,1f) },
        // 2 — White
        new[]{ new Color(0.90f,0.95f,1.00f,1f), new Color(0.70f,0.76f,0.82f,1f),
               new Color(0.38f,0.42f,0.46f,1f), new Color(0.16f,0.18f,0.20f,1f) },
        // 3 — Red
        new[]{ new Color(1.00f,0.22f,0.22f,1f), new Color(0.85f,0.08f,0.08f,1f),
               new Color(0.45f,0.04f,0.04f,1f), new Color(0.20f,0.02f,0.02f,1f) },
    };

    Text              _statusText;
    Text              _promptText;
    bool              _locked;
    bool              _blinkOn;

    string            _inputBuffer = "";
    readonly List<string> _history = new List<string>();
    int               _historyIdx  = 0;

    CommandParser     _parser;
    Font              _uiFont;

    const string Prefix = "root@hackedu:~$ ";

    GameObject        _settingsOverlay;
    RectTransform     _settingsPanel;
    int               _phosphorIdx = 0;
    int               _resIdx;

    float             _masterVolume = 1.0f;
    float             _sfxVolume    = 1.0f;
    float             _musicVolume  = 1.0f;

    /// <summary>Assign in the Inspector once a music AudioSource exists in the scene.</summary>
    [Header("Audio — wire when ready")]
    public AudioSource MusicSource;

    // Texts that get recoloured when the phosphor scheme changes
    readonly List<Text> _pBright = new List<Text>();
    readonly List<Text> _pNormal = new List<Text>();
    readonly List<Text> _pDim    = new List<Text>();

    void Awake()
    {
        BuildUI();
        _parser = new CommandParser(OnBootSystem, OnShutdown, OnSettings);
        StartCoroutine(BlinkCursor());
    }

    void Update()
    {
        if (_locked) return;

        // Settings window absorbs all key input; only ESC passes through
        if (_settingsOverlay != null && _settingsOverlay.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseSettings();
            return;
        }

        foreach (char c in Input.inputString)
        {
            if (c == '\b')
            {
                if (_inputBuffer.Length > 0)
                    _inputBuffer = _inputBuffer.Remove(_inputBuffer.Length - 1);
            }
            else if (c == '\r' || c == '\n')
            {
                ExecuteInput();
            }
            else if (c >= ' ')
            {
                _inputBuffer += c;
            }
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (_history.Count > 0)
            {
                _historyIdx = Mathf.Max(0, _historyIdx - 1);
                _inputBuffer = _history[_historyIdx];
            }
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            _historyIdx = Mathf.Min(_history.Count, _historyIdx + 1);
            _inputBuffer = _historyIdx < _history.Count ? _history[_historyIdx] : "";
        }

        RefreshPrompt();
    }

    void ExecuteInput()
    {
        string cmd = _inputBuffer.Trim();
        _inputBuffer = "";
        _historyIdx  = _history.Count + 1;

        if (string.IsNullOrEmpty(cmd)) return;

        _history.Add(cmd);
        _historyIdx = _history.Count;

        string result = _parser.ParseAndExecute(cmd);
        if (!string.IsNullOrEmpty(result))
            SetStatus(result);
    }

    void RefreshPrompt()
    {
        if (_promptText == null) return;
        _promptText.text = Prefix + _inputBuffer + (_blinkOn ? "█" : " ");
    }

    void OnBootSystem()
    {
        if (_locked) return;
        _locked = true;
        SetStatus("// BOOTING SYSTEM...");
        StartCoroutine(BootSequence());
    }

    void OnShutdown()
    {
        if (_locked) return;
        _locked = true;
        SetStatus("// INITIATING SHUTDOWN...");
        StartCoroutine(ShutdownSequence());
    }

    void OnSettings()
    {
        if (_settingsOverlay == null) return;
        StartCoroutine(OpenSettingsAnim());
    }

    void CloseSettings()
    {
        StartCoroutine(CloseSettingsAnim());
    }

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

    IEnumerator OpenSettingsAnim()
    {
        _settingsOverlay.SetActive(true);
        var cg = _settingsOverlay.GetComponent<CanvasGroup>();
        const float dur = 0.18f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float ease = 1f - Mathf.Pow(1f - t / dur, 3f);   // ease-out cubic
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
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            cg.alpha = 1f - p * p;                            // ease-in quad
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
        for (float t = 0; t < downDur; t += Time.deltaTime)
        { rt.localScale = Vector3.Lerp(orig, small, t / downDur); yield return null; }
        rt.localScale = small;
        for (float t = 0; t < upDur; t += Time.deltaTime)
        { rt.localScale = Vector3.Lerp(small, orig, t / upDur); yield return null; }
        rt.localScale = orig;
    }

    IEnumerator BlinkCursor()
    {
        while (true)
        {
            _blinkOn = !_blinkOn;
            RefreshPrompt();
            yield return new WaitForSeconds(0.5f);
        }
    }

    IEnumerator BootSequence()
    {
        string[] lines =
        {
            "// CHECKING HARDWARE INTEGRITY...",
            "// LOADING HACKEDU KERNEL v2.1.0...",
            "// MOUNTING VIRTUAL FILE SYSTEM...",
            "// INITIALIZING NETWORK SUBSYSTEM...",
            "// STARTING GAME ENGINE...",
            "// BOOT COMPLETE ─ LAUNCHING..."
        };
        foreach (var line in lines)
        {
            SetStatus(line);
            yield return new WaitForSeconds(0.28f);
        }
        yield return new WaitForSeconds(0.15f);
        SceneManager.LoadScene(1);
    }

    IEnumerator ShutdownSequence()
    {
        string[] lines =
        {
            "// SENDING SIGTERM TO ALL PROCESSES...",
            "// FLUSHING BUFFERS...",
            "// UNMOUNTING FILE SYSTEMS...",
            "// SYSTEM HALT."
        };
        foreach (var line in lines)
        {
            SetStatus(line);
            yield return new WaitForSeconds(0.35f);
        }
        yield return new WaitForSeconds(0.4f);
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void SetStatus(string msg)
    {
        if (_statusText) _statusText.text = msg;
    }

    void BuildUI()
    {
        var cv = gameObject.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 10;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        Font font = null;
#if UNITY_EDITOR
        font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/consolas.ttf");
#endif
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _uiFont = font;

        var overlay = MakePanel("Overlay", transform, new Color(0f, 0f, 0f, 0.72f));
        Stretch(overlay.GetRT());

        const float W = 720f, H = 480f;
        var win = MakePanel("TerminalWindow", transform, CPanelBg);
        var winRT = win.GetRT();
        winRT.anchorMin = winRT.anchorMax = winRT.pivot = new Vector2(0.5f, 0.5f);
        winRT.sizeDelta = new Vector2(W, H);
        win.Go.AddComponent<Outline>().effectColor    = CGreenNormal;
        win.Go.GetComponent<Outline>().effectDistance = new Vector2(2f, 2f);

        var hdr = MakePanel("Header", win.Tr, CHeaderBg);
        var hdrRT = hdr.GetRT();
        hdrRT.anchorMin = new Vector2(0, 1); hdrRT.anchorMax = new Vector2(1, 1);
        hdrRT.pivot     = new Vector2(0.5f, 1f);
        hdrRT.offsetMin = new Vector2(0, -46); hdrRT.offsetMax = Vector2.zero;

        var titleT = MakeText("Title", hdr.Tr, font,
            "[ HACKEDU OS v2.1.0 ]  ──  SECURE TERMINAL INTERFACE  ──  [ONLINE]",
            14, CGreenBright, TextAnchor.MiddleCenter);
        titleT.Stretch();
        _pBright.Add(titleT.Comp);

        var sysT = MakeText("SysInfo", win.Tr, font,
            "user@hackedu:~   │   addr: 192.168.0.1   │   uptime: 00:00:00   │   mem: OK",
            11, CGreenDim, TextAnchor.MiddleLeft);
        AnchorTop(sysT.RT, 22, -84, -50);
        _pDim.Add(sysT.Comp);

        MakeHRule(win.Tr, -92, CGreenVeryDim);

        var promptT = MakeText("Prompt", win.Tr, font, Prefix + "█",
            14, CGreenNormal, TextAnchor.MiddleLeft);
        AnchorTop(promptT.RT, 28, -138, -98);
        _promptText = promptT.Comp;
        _pNormal.Add(_promptText);

        MakeHRule(win.Tr, -146, CGreenVeryDim);

        var hintT = MakeText("Hint", win.Tr, font,
            "  type: bootsystem | shutdown | settings | help",
            11, CGreenVeryDim, TextAnchor.MiddleLeft);
        AnchorTop(hintT.RT, 28, -172, -148);

        MakeHRule(win.Tr, -178, CGreenVeryDim);

        float[] btnY = { 60f, -4f, -68f };
        MakeButton(win.Tr, font,
            "  ▶  [ BOOT SYSTEM ]     ─  INITIALIZE KERNEL AND START",
            btnY[0], OnBootSystem);
        MakeButton(win.Tr, font,
            "  ▶  [  SHUTDOWN   ]     ─  TERMINATE ALL PROCESSES",
            btnY[1], OnShutdown);
        MakeButton(win.Tr, font,
            "  ▶  [  SETTINGS   ]     ─  CONFIGURE SYSTEM PARAMETERS",
            btnY[2], OnSettings);

        MakeHRule(win.Tr, -158, CGreenVeryDim);

        var statusT = MakeText("Status", win.Tr, font,
            "// SYSTEM NOMINAL ─ ALL DIAGNOSTICS PASSED ─ AWAITING COMMAND",
            11, CGreenDim, TextAnchor.MiddleLeft);
        statusT.RT.anchorMin = new Vector2(0, 0); statusT.RT.anchorMax = new Vector2(1, 0);
        statusT.RT.pivot     = new Vector2(0.5f, 0f);
        statusT.RT.offsetMin = new Vector2(22, 18); statusT.RT.offsetMax = new Vector2(-22, 44);
        _statusText = statusT.Comp;
        _pDim.Add(_statusText);

        BuildSettingsWindow(font);
    }

    void BuildSettingsWindow(Font font)
    {
        const float W     = 560f;
        const float H     = 380f;
        const float HDR_H = 36f;
        const float PAD   = 22f;
        const float ROW_H = 30f;
        const float SEC_H = 26f;
        const float SPLIT = 220f;   // px from window left where the control column starts

        var dimGo = new GameObject("SettingsOverlay");
        dimGo.transform.SetParent(transform, false);
        var dimImg = dimGo.AddComponent<Image>();
        dimImg.color        = new Color(0f, 0f, 0f, 0.55f);
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
        var xLblGo = new GameObject("X");
        xLblGo.transform.SetParent(xGo.transform, false);
        var xTxt = xLblGo.AddComponent<Text>();
        xTxt.font = font; xTxt.fontSize = 13; xTxt.text = "✕";
        xTxt.color = CGreenNormal;
        xTxt.alignment = TextAnchor.MiddleCenter; xTxt.raycastTarget = false;
        var xLblRT = xLblGo.GetComponent<RectTransform>();
        xLblRT.anchorMin = Vector2.zero; xLblRT.anchorMax = Vector2.one;
        xLblRT.offsetMin = xLblRT.offsetMax = Vector2.zero;

        // y = distance from window top (positive, grows downward)
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
            v => { _musicVolume = v; AudioManager.Instance?.SetMusicVolume(v); if (MusicSource) MusicSource.volume = v; });

        _settingsOverlay = dimGo;
        _settingsPanel   = winRT;
        dimGo.SetActive(false);
    }

    float SWinSection(Transform parent, Font font, string title,
        float y, float winW, float pad, float secH)
    {
        MakeHRule(parent, -y, CGreenVeryDim);
        y += 4f;
        var t = MakeText($"Sec_{title}", parent, font,
            $"── {title} ", 10, CGreenDim, TextAnchor.MiddleLeft);
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

        float ctrlX = split;
        float ctrlW = winW - split - pad;
        const float BTN_W = 26f;
        int idx = Mathf.Clamp(initial, 0, options.Length - 1);

        var prevGo  = new GameObject($"Prev_{label}");
        prevGo.transform.SetParent(parent, false);
        var prevImg = prevGo.AddComponent<Image>();
        prevImg.color = CGreenDimBtn;
        var prevRT  = prevGo.GetComponent<RectTransform>();
        prevRT.anchorMin = prevRT.anchorMax = new Vector2(0f, 1f);
        prevRT.pivot = new Vector2(0f, 1f);
        prevRT.sizeDelta        = new Vector2(BTN_W, rowH - 4f);
        prevRT.anchoredPosition = new Vector2(ctrlX, -(y + 2f));
        var prevLblGo = new GameObject("T"); prevLblGo.transform.SetParent(prevGo.transform, false);
        var prevTxt = prevLblGo.AddComponent<Text>();
        prevTxt.font = font; prevTxt.fontSize = 13; prevTxt.text = "<";
        prevTxt.color = CGreenNormal; prevTxt.alignment = TextAnchor.MiddleCenter; prevTxt.raycastTarget = false;
        Stretch(prevLblGo.GetComponent<RectTransform>());

        var valGo  = new GameObject($"Val_{label}");
        valGo.transform.SetParent(parent, false);
        var valImg = valGo.AddComponent<Image>();
        valImg.color = Color.clear; valImg.raycastTarget = false;
        var valRT  = valGo.GetComponent<RectTransform>();
        valRT.anchorMin = valRT.anchorMax = new Vector2(0f, 1f);
        valRT.pivot = new Vector2(0f, 1f);
        valRT.sizeDelta        = new Vector2(ctrlW - BTN_W * 2f, rowH - 4f);
        valRT.anchoredPosition = new Vector2(ctrlX + BTN_W, -(y + 2f));
        var valLblGo = new GameObject("T"); valLblGo.transform.SetParent(valGo.transform, false);
        var valTxt = valLblGo.AddComponent<Text>();
        valTxt.font = font; valTxt.fontSize = 12; valTxt.text = options[idx];
        valTxt.color = CGreenBright; valTxt.alignment = TextAnchor.MiddleCenter; valTxt.raycastTarget = false;
        Stretch(valLblGo.GetComponent<RectTransform>());

        var nextGo  = new GameObject($"Next_{label}");
        nextGo.transform.SetParent(parent, false);
        var nextImg = nextGo.AddComponent<Image>();
        nextImg.color = CGreenDimBtn;
        var nextRT  = nextGo.GetComponent<RectTransform>();
        nextRT.anchorMin = nextRT.anchorMax = new Vector2(0f, 1f);
        nextRT.pivot = new Vector2(0f, 1f);
        nextRT.sizeDelta        = new Vector2(BTN_W, rowH - 4f);
        nextRT.anchoredPosition = new Vector2(ctrlX + ctrlW - BTN_W, -(y + 2f));
        var nextLblGo = new GameObject("T"); nextLblGo.transform.SetParent(nextGo.transform, false);
        var nextTxt = nextLblGo.AddComponent<Text>();
        nextTxt.font = font; nextTxt.fontSize = 13; nextTxt.text = ">";
        nextTxt.color = CGreenNormal; nextTxt.alignment = TextAnchor.MiddleCenter; nextTxt.raycastTarget = false;
        Stretch(nextLblGo.GetComponent<RectTransform>());

        var prevBtn = prevGo.AddComponent<Button>();
        prevBtn.targetGraphic = prevImg;
        ApplyCycleColors(prevBtn);
        prevBtn.onClick.AddListener(() =>
        {
            idx = (idx - 1 + options.Length) % options.Length;
            valTxt.text = options[idx];
            onChange(idx);
        });

        var nextBtn = nextGo.AddComponent<Button>();
        nextBtn.targetGraphic = nextImg;
        ApplyCycleColors(nextBtn);
        nextBtn.onClick.AddListener(() =>
        {
            idx = (idx + 1) % options.Length;
            valTxt.text = options[idx];
            onChange(idx);
        });

        return y + rowH + 2f;
    }

    // Step control for float values — displays percentage, snaps to step increments
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

        float ctrlX = split;
        float ctrlW = winW - split - pad;
        const float BTN_W = 28f;
        float cur = Mathf.Clamp(initial, minVal, maxVal);

        var minusGo  = new GameObject($"Minus_{label}");
        minusGo.transform.SetParent(parent, false);
        var minusImg = minusGo.AddComponent<Image>();
        minusImg.color = CGreenDimBtn;
        var minusRT  = minusGo.GetComponent<RectTransform>();
        minusRT.anchorMin = minusRT.anchorMax = new Vector2(0f, 1f);
        minusRT.pivot = new Vector2(0f, 1f);
        minusRT.sizeDelta        = new Vector2(BTN_W, rowH - 4f);
        minusRT.anchoredPosition = new Vector2(ctrlX, -(y + 2f));
        var mLbl = new GameObject("T"); mLbl.transform.SetParent(minusGo.transform, false);
        var mTxt = mLbl.AddComponent<Text>();
        mTxt.font = font; mTxt.fontSize = 15; mTxt.text = "−";
        mTxt.color = CGreenNormal; mTxt.alignment = TextAnchor.MiddleCenter; mTxt.raycastTarget = false;
        Stretch(mLbl.GetComponent<RectTransform>());

        var valGo  = new GameObject($"Val_{label}");
        valGo.transform.SetParent(parent, false);
        var valImg = valGo.AddComponent<Image>();
        valImg.color = Color.clear; valImg.raycastTarget = false;
        var valRT  = valGo.GetComponent<RectTransform>();
        valRT.anchorMin = valRT.anchorMax = new Vector2(0f, 1f);
        valRT.pivot = new Vector2(0f, 1f);
        valRT.sizeDelta        = new Vector2(ctrlW - BTN_W * 2f, rowH - 4f);
        valRT.anchoredPosition = new Vector2(ctrlX + BTN_W, -(y + 2f));
        var vLbl = new GameObject("T"); vLbl.transform.SetParent(valGo.transform, false);
        var vTxt = vLbl.AddComponent<Text>();
        vTxt.font = font; vTxt.fontSize = 12; vTxt.text = FormatPct(cur);
        vTxt.color = CGreenBright; vTxt.alignment = TextAnchor.MiddleCenter; vTxt.raycastTarget = false;
        Stretch(vLbl.GetComponent<RectTransform>());

        var plusGo  = new GameObject($"Plus_{label}");
        plusGo.transform.SetParent(parent, false);
        var plusImg = plusGo.AddComponent<Image>();
        plusImg.color = CGreenDimBtn;
        var plusRT  = plusGo.GetComponent<RectTransform>();
        plusRT.anchorMin = plusRT.anchorMax = new Vector2(0f, 1f);
        plusRT.pivot = new Vector2(0f, 1f);
        plusRT.sizeDelta        = new Vector2(BTN_W, rowH - 4f);
        plusRT.anchoredPosition = new Vector2(ctrlX + ctrlW - BTN_W, -(y + 2f));
        var pLbl = new GameObject("T"); pLbl.transform.SetParent(plusGo.transform, false);
        var pTxt = pLbl.AddComponent<Text>();
        pTxt.font = font; pTxt.fontSize = 14; pTxt.text = "+";
        pTxt.color = CGreenNormal; pTxt.alignment = TextAnchor.MiddleCenter; pTxt.raycastTarget = false;
        Stretch(pLbl.GetComponent<RectTransform>());

        var minusBtn = minusGo.AddComponent<Button>();
        minusBtn.targetGraphic = minusImg;
        ApplyCycleColors(minusBtn);
        minusBtn.onClick.AddListener(() =>
        {
            cur = Mathf.Round(Mathf.Clamp(cur - step, minVal, maxVal) / step) * step;
            vTxt.text = FormatPct(cur);
            onChange(cur);
        });

        var plusBtn = plusGo.AddComponent<Button>();
        plusBtn.targetGraphic = plusImg;
        ApplyCycleColors(plusBtn);
        plusBtn.onClick.AddListener(() =>
        {
            cur = Mathf.Round(Mathf.Clamp(cur + step, minVal, maxVal) / step) * step;
            vTxt.text = FormatPct(cur);
            onChange(cur);
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
        var rt = rule.GetRT();
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

        var go = new GameObject($"Btn_{centerY}");
        go.transform.SetParent(parent, false);

        var img   = go.AddComponent<Image>();
        img.color = CGreenDimBtn;

        var rt = go.GetComponent<RectTransform>();
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
