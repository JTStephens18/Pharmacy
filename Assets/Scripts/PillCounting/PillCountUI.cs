using TMPro;
using UnityEngine;

/// <summary>
/// World-space UI that displays the current pill count.
/// Subscribes to PillCountingChute events for updates.
/// </summary>
public class PillCountUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The TextMeshPro component showing the count. Auto-found in children if left empty.")]
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Completion")]
    [Tooltip("Optional GameObject to show when the target is reached (e.g. Confirm button).")]
    [SerializeField] private GameObject confirmButton;

    [Header("Audio")]
    [Tooltip("Optional AudioSource for the completion chime.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip completionChime;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color completedColor = Color.green;

    private PillCountingChute _chute;

    void Awake()
    {
        if (countText == null)
            countText = GetComponentInChildren<TextMeshProUGUI>();

        if (confirmButton != null)
            confirmButton.SetActive(false);

        // Hide the entire UI until the mini-game activates
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Call this to bind the UI to a chute for the current session.
    /// </summary>
    public void Bind(PillCountingChute chute)
    {
        // Unbind previous
        if (_chute != null)
        {
            _chute.OnPillCounted -= UpdateCount;
            _chute.OnTargetReached -= OnCompleted;
        }

        _chute = chute;

        if (_chute != null)
        {
            _chute.OnPillCounted += UpdateCount;
            _chute.OnTargetReached += OnCompleted;
            UpdateCount(_chute.GetCurrentCount(), _chute.GetTargetCount());
        }
    }

    void OnDisable()
    {
        if (_chute != null)
        {
            _chute.OnPillCounted -= UpdateCount;
            _chute.OnTargetReached -= OnCompleted;
        }
    }

    private void UpdateCount(int current, int target)
    {
        if (countText != null)
        {
            countText.text = $"Count: {current} / {target}";
            countText.color = normalColor;
        }
    }

    private void OnCompleted()
    {
        if (countText != null)
        {
            countText.color = completedColor;
        }

        if (confirmButton != null)
        {
            confirmButton.SetActive(true);
        }

        // Play completion chime
        if (audioSource != null && completionChime != null)
        {
            audioSource.PlayOneShot(completionChime);
        }

        Debug.Log("[PillCountUI] Completion UI shown.");
    }

    /// <summary>
    /// Resets the UI for a new session.
    /// </summary>
    public void ResetUI()
    {
        if (countText != null)
        {
            countText.text = "Count: 0 / 0";
            countText.color = normalColor;
        }

        if (confirmButton != null)
            confirmButton.SetActive(false);
    }
}
