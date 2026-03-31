using TMPro;
using UnityEngine;

/// <summary>
/// World-space UI displaying the current pill count and target for the filling station.
/// Subscribes to DispensingController events. Changes color on target reached / overfill.
/// </summary>
public class FillCounterUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TMP text showing the count. Auto-found in children if left empty.")]
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color targetReachedColor = Color.green;
    [SerializeField] private Color overfilledColor = Color.yellow;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip targetReachedChime;

    private DispensingController _controller;

    void Awake()
    {
        if (countText == null)
            countText = GetComponentInChildren<TextMeshProUGUI>();

        gameObject.SetActive(false);
    }

    /// <summary>Bind to a dispensing controller for the current session.</summary>
    public void Bind(DispensingController controller)
    {
        if (_controller != null)
        {
            _controller.OnPillDispensed -= UpdateCount;
            _controller.OnTargetReached -= OnCompleted;
        }

        _controller = controller;

        if (_controller != null)
        {
            _controller.OnPillDispensed += UpdateCount;
            _controller.OnTargetReached += OnCompleted;
            UpdateCount(_controller.CurrentCount, _controller.TargetCount);
        }
    }

    void OnDisable()
    {
        if (_controller != null)
        {
            _controller.OnPillDispensed -= UpdateCount;
            _controller.OnTargetReached -= OnCompleted;
        }
    }

    private void UpdateCount(int current, int target)
    {
        if (countText == null) return;

        countText.text = target > 0 ? $"{current} / {target}" : $"{current}";

        if (target > 0 && current > target)
            countText.color = overfilledColor;
        else if (target > 0 && current >= target)
            countText.color = targetReachedColor;
        else
            countText.color = normalColor;
    }

    private void OnCompleted()
    {
        if (countText != null)
            countText.color = targetReachedColor;

        if (audioSource != null && targetReachedChime != null)
            audioSource.PlayOneShot(targetReachedChime);
    }

    /// <summary>Reset text and color for a new session.</summary>
    public void ResetUI()
    {
        if (countText != null)
        {
            countText.text = "0";
            countText.color = normalColor;
        }
    }
}
