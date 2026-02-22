using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Records all dialogue exchanges and provides a toggleable scrollable history panel.
/// Press ENTER to toggle the history view.
/// Attach alongside or as a child of DialogueManager.
/// </summary>
public class DialogueHistory : MonoBehaviour
{
    [Header("History UI")]
    [Tooltip("Root panel for the conversation history. Toggled with ENTER.")]
    [SerializeField] private GameObject historyPanel;

    [Tooltip("TextMeshPro text for the scrollable history content.")]
    [SerializeField] private TextMeshProUGUI historyText;

    [Tooltip("ScrollRect for scrolling through history.")]
    [SerializeField] private ScrollRect historyScrollRect;

    [Header("Settings")]
    [Tooltip("Key to toggle the history panel.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Return;

    [Tooltip("Maximum number of lines to keep in history.")]
    [SerializeField] private int maxLines = 200;

    [Header("Formatting")]
    [Tooltip("Color hex for speaker names (without #).")]
    [SerializeField] private string speakerColorHex = "FFD700";

    [Tooltip("Color hex for player responses (without #).")]
    [SerializeField] private string playerColorHex = "87CEEB";

    // ── Runtime ─────────────────────────────────────────────────────
    private List<string> _lines = new List<string>();
    private bool _isVisible;

    void Awake()
    {
        if (historyPanel != null)
            historyPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleHistory();
        }
    }

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// Record a line of dialogue in the history.
    /// </summary>
    public void RecordLine(string speaker, string text)
    {
        string colorHex = speaker == "You" ? playerColorHex : speakerColorHex;
        string formatted = $"<color=#{colorHex}><b>{speaker}:</b></color> {text}";

        _lines.Add(formatted);

        // Trim if over limit
        while (_lines.Count > maxLines)
            _lines.RemoveAt(0);

        // Update display if visible
        if (_isVisible)
            RefreshDisplay();
    }

    /// <summary>
    /// Toggle the history panel visibility.
    /// </summary>
    public void ToggleHistory()
    {
        _isVisible = !_isVisible;

        if (historyPanel != null)
            historyPanel.SetActive(_isVisible);

        if (_isVisible)
        {
            RefreshDisplay();
            ScrollToBottom();
        }

        Debug.Log($"[DialogueHistory] History panel {(_isVisible ? "shown" : "hidden")}.");
    }

    /// <summary>
    /// Clear all history.
    /// </summary>
    public void ClearHistory()
    {
        _lines.Clear();
        if (historyText != null)
            historyText.text = "";
    }

    /// <summary>
    /// Returns whether the history panel is currently visible.
    /// </summary>
    public bool IsVisible => _isVisible;

    // ── Private ─────────────────────────────────────────────────────

    private void RefreshDisplay()
    {
        if (historyText != null)
            historyText.text = string.Join("\n\n", _lines);
    }

    private void ScrollToBottom()
    {
        if (historyScrollRect != null)
        {
            // Force layout rebuild then scroll to bottom
            Canvas.ForceUpdateCanvases();
            historyScrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
