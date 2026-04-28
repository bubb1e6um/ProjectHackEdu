using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class TerminalController : MonoBehaviour
{
    private List<string> _commandHistory = new List<string>();
    private int _historyIndex = -1;

    private VirtualFileSystem _vfs;
    private CommandParser _parser;

    [Header("UI Ссылки")]
    public GameObject terminalPanel;
    public TMP_InputField commandLine;
    public TextMeshProUGUI outputLog;
    public TextMeshProUGUI currentPathLabel;
    public TextMeshProUGUI promptLabel;

    public NetworkGraph GlobalNetwork { get; private set; }
    public NetworkNode LocalNode { get; private set; }
    public NetworkNode ActiveConnection { get; set; }

    private bool isTerminalOpen = false;
    private bool _isAnimating   = false;
    private CanvasGroup _canvasGroup;

    private Image _deathOverlay;
    private Image _chromaticOverlay;

    private bool _isGameOver = false;
    private bool _isVictory  = false;

    [Header("Typewriter")]
    [Tooltip("Символов в секунду (0 = мгновенно)")]
    public float charsPerSecond = 45f;

    private readonly Queue<string> _printQueue    = new Queue<string>();
    private          Coroutine     _typingRoutine;

    private const float AnimDuration = 0.12f;

    void Awake()
    {
        _vfs = new VirtualFileSystem();

        GlobalNetwork = new NetworkGraph();
        LocalNode = new NetworkNode("127.0.0.1", "Player_Deck", false);
        GlobalNetwork.AddNode(LocalNode);
        ActiveConnection = LocalNode;

        _parser = new CommandParser(this, _vfs);
    }

    void Start()
    {
        _canvasGroup = terminalPanel.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = terminalPanel.AddComponent<CanvasGroup>();

        terminalPanel.SetActive(false);
        outputLog.text = "Root session started...\n\n";

        commandLine.onSubmit.AddListener(OnCommandSubmitted);
        UpdatePromptDisplay();
        BuildDeathOverlay();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleTerminal();
        }

        if (isTerminalOpen && _commandHistory.Count > 0)
        {
            if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                _historyIndex = Mathf.Clamp(_historyIndex + 1, 0, _commandHistory.Count - 1);
                commandLine.text = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
                MoveCursorToEnd();
            }
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                _historyIndex--;
                if (_historyIndex < 0)
                {
                    _historyIndex = -1;
                    commandLine.text = "";
                }
                else
                {
                    commandLine.text = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
                    MoveCursorToEnd();
                }
            }
        }
    }

    private void MoveCursorToEnd()
    {
        commandLine.caretPosition = commandLine.text.Length;
    }

    public void ToggleTerminal()
    {
        if (_isAnimating) return;
        if ((_isGameOver || _isVictory) && isTerminalOpen) return;

        isTerminalOpen = !isTerminalOpen;

        if (isTerminalOpen)
        {
            Time.timeScale = 0f;
            if (!_isGameOver && !_isVictory)
                outputLog.text = "Root session started...\n\n";
            commandLine.text = "";
            _historyIndex    = -1;
            StartCoroutine(AnimateOpen());
        }
        else
        {
            if (ActiveConnection != null && LocalNode != null && ActiveConnection.IP != "127.0.0.1")
            {
                ActiveConnection = LocalNode;
                UpdatePromptDisplay();
            }
            outputLog.text   = "";
            commandLine.text = "";
            StartCoroutine(AnimateClose());
        }
    }

    private IEnumerator AnimateOpen()
    {
        _isAnimating = true;
        terminalPanel.SetActive(true);

        var rt = terminalPanel.GetComponent<RectTransform>();
        _canvasGroup.alpha           = 0f;
        _canvasGroup.interactable    = false;
        _canvasGroup.blocksRaycasts  = false;
        rt.localScale                = new Vector3(1f, 0.04f, 1f);

        for (float t = 0f; t < AnimDuration; t += Time.unscaledDeltaTime)
        {
            float p = t / AnimDuration;
            float e = 1f - Mathf.Pow(1f - p, 3f);   // ease-out cubic
            _canvasGroup.alpha = e;
            rt.localScale      = new Vector3(1f, Mathf.Lerp(0.04f, 1f, e), 1f);
            yield return null;
        }

        _canvasGroup.alpha          = 1f;
        rt.localScale               = Vector3.one;
        _canvasGroup.interactable   = true;
        _canvasGroup.blocksRaycasts = true;
        _isAnimating                = false;

        commandLine.ActivateInputField();
    }

    private IEnumerator AnimateClose()
    {
        _isAnimating = true;
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;

        var rt      = terminalPanel.GetComponent<RectTransform>();
        float start = rt.localScale.y;

        for (float t = 0f; t < AnimDuration; t += Time.unscaledDeltaTime)
        {
            float p = t / AnimDuration;
            float e = p * p;   // ease-in quad
            _canvasGroup.alpha = 1f - e;
            rt.localScale      = new Vector3(1f, Mathf.Lerp(start, 0.04f, e), 1f);
            yield return null;
        }

        terminalPanel.SetActive(false);
        rt.localScale  = Vector3.one;
        _isAnimating   = false;
        Time.timeScale = 1f;
    }

    private void OnCommandSubmitted(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) { commandLine.ActivateInputField(); return; }

        if (_isGameOver)
        {
            string cmd = input.Trim().ToLowerInvariant().Split(' ')[0];
            if (cmd != "restart" && cmd != "quit")
            {
                PrintToLog("<color=#FF2222>[LOCKDOWN] Доступны только команды: restart, quit</color>");
                commandLine.text = "";
                commandLine.ActivateInputField();
                return;
            }
        }

        if (_commandHistory.Count == 0 || _commandHistory[_commandHistory.Count - 1] != input)
        {
            _commandHistory.Add(input);
        }
        _historyIndex = -1;

        PrintToLog($"<color=#4AF2CC>{currentPathLabel.text}</color>\n> {input}");

        string response = _parser.ParseAndExecute(input);

        if (!string.IsNullOrEmpty(response))
        {
            PrintToLog(response);
        }

        UpdatePromptDisplay();

        commandLine.text = "";
        commandLine.ActivateInputField();
    }

    public void PrintToLog(string message, bool typewriter = false)
    {
        if (!typewriter || charsPerSecond <= 0f)
        {
            outputLog.text += message + "\n";
            if (outputLog.text.Length > 1500)
                outputLog.text = outputLog.text.Substring(outputLog.text.Length - 1000);
            return;
        }

        _printQueue.Enqueue(message + "\n");
        if (_typingRoutine == null)
            _typingRoutine = StartCoroutine(TypewriterRoutine());
    }

    private IEnumerator TypewriterRoutine()
    {
        float elapsed = 0f;

        while (_printQueue.Count > 0)
        {
            string msg      = _printQueue.Dequeue();
            float  interval = 1f / charsPerSecond;

            foreach (char c in msg)
            {
                elapsed += Time.unscaledDeltaTime;
                while (elapsed < interval)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                }
                elapsed -= interval;

                outputLog.text += c;
                if (outputLog.text.Length > 1500)
                    outputLog.text = outputLog.text.Substring(outputLog.text.Length - 1000);
            }
        }

        _typingRoutine = null;
    }

    public void ClearLog()
    {
        _printQueue.Clear();
        if (_typingRoutine != null)
        {
            StopCoroutine(_typingRoutine);
            _typingRoutine = null;
        }
        outputLog.text = string.Empty;
    }

    private void UpdatePromptDisplay()
    {
        if (_isGameOver) return;

        string path = "";
        VFSNode current = _vfs.CurrentDirectory;

        while (current != null && current.Name != "root")
        {
            path = "/" + current.Name + path;
            current = current.Parent;
        }

        currentPathLabel.text = "~" + path;

        if (ActiveConnection.IP != "127.0.0.1")
        {
            currentPathLabel.color = Color.magenta;
            currentPathLabel.text = $"ssh@{ActiveConnection.IP}:{path}";
        }
        else
        {
            currentPathLabel.color = new Color(0.33f, 1f, 0.33f);
            currentPathLabel.text = "~" + path;
        }
    }

    public void TriggerGameOver()
    {
        StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        for (int i = 0; i < 3; i++)
        {
            yield return StartCoroutine(FadeOverlay(0f, 0.97f, 0.04f));
            yield return StartCoroutine(FadeOverlay(0.97f, 0f, 0.04f));
            yield return new WaitForSecondsRealtime(0.02f);
        }

        yield return StartCoroutine(FadeOverlay(0f, 0.95f, 0.10f));

        _isGameOver = true;
        if (!isTerminalOpen)
            ToggleTerminal();

        StartCoroutine(FadeOverlay(0.65f, 0f, 0.55f));

        yield return new WaitForSecondsRealtime(AnimDuration + 0.05f);

        ApplyDeathTheme();

        PrintToLog("\n[!] CRITICAL ALERT: UNAUTHORIZED ACCESS DETECTED.", true);
        PrintToLog("[!] SYSTEM LOCKDOWN INITIATED. YOU HAVE BEEN CAUGHT.\n", true);
        PrintToLog("Available system overrides: restart, quit", true);
    }

    private IEnumerator FadeOverlay(float fromAlpha, float toAlpha, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(t / duration));
            SetOverlayAlpha(a);
            yield return null;
        }
        SetOverlayAlpha(toAlpha);
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (_deathOverlay     != null) _deathOverlay.color     = new Color(0.50f, 0f, 0f, alpha);
        if (_chromaticOverlay != null) _chromaticOverlay.color = new Color(0f,    0f, 0f, 0f);
    }

    private void ApplyDeathTheme()
    {
        // Shift all terminal colours toward red, preserving relative brightness
        foreach (var tmp in terminalPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            float lum = tmp.color.r * 0.299f + tmp.color.g * 0.587f + tmp.color.b * 0.114f;
            tmp.color = new Color(Mathf.Lerp(0.55f, 1.00f, lum), 0f, 0f, tmp.color.a);
        }

        foreach (var img in terminalPanel.GetComponentsInChildren<Image>(true))
        {
            float lum = img.color.r * 0.299f + img.color.g * 0.587f + img.color.b * 0.114f;
            img.color = new Color(Mathf.Lerp(0.08f, 0.72f, lum), 0f, 0f, img.color.a);
        }

        commandLine.caretColor     = new Color(1f,    0.20f, 0.10f, 1f);
        commandLine.selectionColor = new Color(0.55f, 0f,    0f,    0.45f);
    }

    private void BuildDeathOverlay()
    {
        var canvasGO        = new GameObject("[DeathOverlay]");
        canvasGO.transform.SetParent(transform);
        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        _deathOverlay     = CreateOverlayImage(canvasGO, "Red",  new Color(1f,  0f,  0f,  0f));
        _chromaticOverlay = CreateOverlayImage(canvasGO, "Cyan", new Color(0f, 0.8f, 1f,  0f));

        // slight offset for chromatic aberration effect
        _chromaticOverlay.GetComponent<RectTransform>().anchoredPosition = new Vector2(5f, -3f);
    }

    private static Image CreateOverlayImage(GameObject parent, string name, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt  = img.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }

    public void TriggerVictory()
    {
        StartCoroutine(VictorySequence());
    }

    private IEnumerator VictorySequence()
    {
        for (int i = 0; i < 2; i++)
        {
            yield return StartCoroutine(FadeOverlayColor(0f, 0.85f, 0.18f, new Color(0f, 0.8f, 0.3f)));
            yield return StartCoroutine(FadeOverlayColor(0.85f, 0f, 0.18f, new Color(0f, 0.8f, 0.3f)));
            yield return new WaitForSecondsRealtime(0.08f);
        }

        yield return StartCoroutine(FadeOverlayColor(0f, 0.75f, 0.35f, new Color(0f, 0.8f, 0.3f)));

        _isVictory = true;
        if (!isTerminalOpen) ToggleTerminal();

        StartCoroutine(FadeOverlayColor(0.55f, 0f, 0.7f, new Color(0f, 0.8f, 0.3f)));

        yield return new WaitForSecondsRealtime(AnimDuration + 0.05f);

        outputLog.color = Color.green;
        commandLine.textComponent.color = Color.green;
        currentPathLabel.color = Color.green;

        PrintToLog("\n[+] SUCCESS: REQUIRED FILES OBTAINED.", true);
        PrintToLog("[+] MISSION COMPLETE.\n", true);
        PrintToLog("Type 'next' to proceed to the next sector.", true);
    }

    private IEnumerator FadeOverlayColor(float fromAlpha, float toAlpha, float duration, Color rgb)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(t / duration));
            if (_deathOverlay != null)
                _deathOverlay.color = new Color(rgb.r, rgb.g, rgb.b, a);
            yield return null;
        }
        if (_deathOverlay != null)
            _deathOverlay.color = new Color(rgb.r, rgb.g, rgb.b, toAlpha);
    }
}
