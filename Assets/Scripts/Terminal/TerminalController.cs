using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

    // Animation settings
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
        // Get or add CanvasGroup for fade animation
        _canvasGroup = terminalPanel.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = terminalPanel.AddComponent<CanvasGroup>();

        terminalPanel.SetActive(false);
        outputLog.text = "Root session started...\n\n";

        commandLine.onSubmit.AddListener(OnCommandSubmitted);
        UpdatePromptDisplay();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleTerminal();
        }

        // --- HISTORY LOGIC ---
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

        isTerminalOpen = !isTerminalOpen;

        if (isTerminalOpen)
        {
            Time.timeScale = 0f;
            outputLog.text   = "Root session started...\n\n";
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
            // Ease-out cubic
            float e = 1f - Mathf.Pow(1f - p, 3f);
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
            // Ease-in quad
            float e = p * p;
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

    public void PrintToLog(string message)
    {
        outputLog.text += message + "\n";
        
        if (outputLog.text.Length > 1500)
        {
            outputLog.text = outputLog.text.Substring(outputLog.text.Length - 1000);
        }
    }
    public void ClearLog()
    {
        outputLog.text = string.Empty;
    }
    private void UpdatePromptDisplay()
    {
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
        if (!isTerminalOpen)
        {
            ToggleTerminal();
        }

        outputLog.color = Color.red;
        commandLine.textComponent.color = Color.red;
        currentPathLabel.color = Color.red;
        promptLabel.color = Color.red;


        PrintToLog("\n[!] CRITICAL ALERT: UNAUTHORIZED ACCESS DETECTED.");
        PrintToLog("[!] SYSTEM LOCKDOWN INITIATED. YOU HAVE BEEN CAUGHT.\n");
        PrintToLog("Available system overrides: restart, quit");
    }

    public void TriggerVictory()
    {
        if (!isTerminalOpen) ToggleTerminal();

        outputLog.color = Color.green;
        commandLine.textComponent.color = Color.green;
        currentPathLabel.color = Color.green;

        PrintToLog("\n[+] SUCCESS: REQUIRED FILES OBTAINED.");
        PrintToLog("[+] MISSION COMPLETE.\n");
        PrintToLog("Type 'next' to proceed to the next sector.");

        Time.timeScale = 0f;
    }
}
