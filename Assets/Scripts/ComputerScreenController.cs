using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Manages view switching and tab navigation for the computer screen UI.
/// Design all UI visually in the Unity Editor on a World Space Canvas,
/// then wire up views and tabs in the Inspector.
///
/// To add a new page:
///   1. Create a child GameObject under the interactive UI root, design it visually.
///   2. Add a new entry to the Views array — assign the tab button and view root.
///   3. Wire any action buttons' onClick to your game system methods in the Inspector.
/// </summary>
public class ComputerScreenController : MonoBehaviour
{
    // ── Serializable view definition ────────────────────────────────
    [Serializable]
    public class ScreenView
    {
        [Tooltip("Display name for this view (used for lookups and debugging).")]
        public string viewName;

        [Tooltip("Button that activates this view. Can be null if the view is only opened programmatically.")]
        public Button tabButton;

        [Tooltip("Root GameObject for this view's content.")]
        public GameObject viewRoot;
    }

    // ── Inspector Fields ────────────────────────────────────────────
    [Header("Views")]
    [Tooltip("List of all views. First entry is treated as the main/home view.")]
    [SerializeField] private ScreenView[] views;

    [Header("Tab Highlight Colors")]
    [SerializeField] private Color activeTabColor = new Color(0.24f, 0.52f, 0.88f, 1f);
    [SerializeField] private Color inactiveTabColor = new Color(0.2f, 0.2f, 0.26f, 1f);

    [Header("Events")]
    [Tooltip("Fires when the active view changes. Receives the view name.")]
    public UnityEvent<string> OnViewChanged;

    // ── Runtime State ───────────────────────────────────────────────
    private int _currentViewIndex = -1;

    /// <summary>Name of the currently active view, or empty if none.</summary>
    public string CurrentViewName =>
        _currentViewIndex >= 0 && _currentViewIndex < views.Length
            ? views[_currentViewIndex].viewName
            : string.Empty;

    // ── Unity Lifecycle ─────────────────────────────────────────────
    void Awake()
    {
        Debug.Log($"[ComputerScreenController] Awake — {(views != null ? views.Length : 0)} views configured.");

        if (views == null || views.Length == 0)
        {
            Debug.LogWarning("[ComputerScreenController] No views assigned! " +
                "Open this GameObject in the Inspector and add entries to the 'Views' array.");
            return;
        }

        // Log each view so you can verify the setup
        for (int i = 0; i < views.Length; i++)
        {
            string tabInfo = views[i].tabButton != null ? views[i].tabButton.gameObject.name : "(no tab button)";
            string rootInfo = views[i].viewRoot != null ? views[i].viewRoot.name : "(no view root!)";
            Debug.Log($"[ComputerScreenController]   View [{i}] \"{views[i].viewName}\" — tab: {tabInfo}, root: {rootInfo}");
        }

        WireTabButtons();
        HideAll();
    }

    // ── DEBUG: Click diagnostic (remove once tabs work) ─────────────
    private bool _loggedCanvasInfo = false;

    void Update()
    {
        if (_currentViewIndex < 0)
        {
            _loggedCanvasInfo = false;
            return; // only log while a view is active
        }

        // Log canvas info once when a view becomes active
        if (!_loggedCanvasInfo)
        {
            _loggedCanvasInfo = true;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Camera evtCam = canvas.worldCamera;
                Debug.Log($"[DEBUG-CANVAS] Canvas: \"{canvas.gameObject.name}\" " +
                    $"renderMode={canvas.renderMode} " +
                    $"sortOrder={canvas.sortingOrder}");
                Debug.Log($"[DEBUG-CANVAS] Canvas pos={canvas.transform.position} " +
                    $"forward={canvas.transform.forward} " +
                    $"scale={canvas.transform.lossyScale}");
                if (evtCam != null)
                    Debug.Log($"[DEBUG-CANVAS] Event Camera: \"{evtCam.gameObject.name}\" " +
                        $"pos={evtCam.transform.position} forward={evtCam.transform.forward}");
                else
                    Debug.LogError("[DEBUG-CANVAS] Event Camera is NULL! Clicks will never work.");

                // Check if canvas is facing the camera
                if (evtCam != null)
                {
                    Vector3 camToCanvas = canvas.transform.position - evtCam.transform.position;
                    float dot = Vector3.Dot(canvas.transform.forward, camToCanvas);
                    Debug.Log($"[DEBUG-CANVAS] Canvas-forward · cam-to-canvas dot = {dot:F3} " +
                        $"(negative = canvas faces camera ✓, positive = faces AWAY ✗)");
                }

                // Check GraphicRaycaster settings
                GraphicRaycaster gr = canvas.GetComponent<GraphicRaycaster>();
                if (gr != null)
                    Debug.Log($"[DEBUG-CANVAS] GraphicRaycaster enabled={gr.enabled} " +
                        $"blockingObjects={gr.blockingObjects}");
                else
                    Debug.LogError("[DEBUG-CANVAS] No GraphicRaycaster on canvas!");
            }
            else
            {
                Debug.LogError("[DEBUG-CANVAS] No parent Canvas found on controller!");
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[DEBUG-CLICK] Mouse clicked at screen pos: {Input.mousePosition} " +
                $"(screen size: {Screen.width}x{Screen.height})");

            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null) return;

            var pointerData = new UnityEngine.EventSystems.PointerEventData(eventSystem)
            {
                position = Input.mousePosition
            };
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            eventSystem.RaycastAll(pointerData, results);

            if (results.Count == 0)
            {
                // Also try manual ray to see if it at least points at the canvas area
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null && canvas.worldCamera != null)
                {
                    Ray ray = canvas.worldCamera.ScreenPointToRay(Input.mousePosition);
                    Debug.LogWarning($"[DEBUG-CLICK] Raycast hit NOTHING. " +
                        $"Camera ray: origin={ray.origin}, dir={ray.direction}");
                }
                else
                {
                    Debug.LogWarning("[DEBUG-CLICK] Raycast hit NOTHING and canvas/camera is null.");
                }
            }
            else
            {
                foreach (var r in results)
                {
                    Debug.Log($"[DEBUG-CLICK] Hit: \"{r.gameObject.name}\" " +
                        $"(layer: {LayerMask.LayerToName(r.gameObject.layer)}, distance: {r.distance:F2})");
                }
            }
        }
    }

    // ── Public API (called by ComputerScreen.cs) ────────────────────

    /// <summary>
    /// Show the main (first) view. Call this when the player focuses on the computer.
    /// </summary>
    public void ResetToMain()
    {
        if (views == null || views.Length == 0)
        {
            Debug.LogWarning("[ComputerScreenController] ResetToMain called but no views configured.");
            return;
        }
        Debug.Log("[ComputerScreenController] ResetToMain — showing first view.");
        ShowView(0);
    }

    /// <summary>
    /// Hide all views. Call this when the player exits the computer.
    /// </summary>
    public void HideAll()
    {
        if (views == null) return;

        Debug.Log("[ComputerScreenController] HideAll — deactivating all views.");

        for (int i = 0; i < views.Length; i++)
        {
            if (views[i].viewRoot != null)
                views[i].viewRoot.SetActive(false);

            SetTabHighlight(views[i], false);
        }
        _currentViewIndex = -1;
    }

    /// <summary>Show a view by its index in the views array.</summary>
    public void ShowView(int index)
    {
        if (views == null || index < 0 || index >= views.Length)
        {
            Debug.LogWarning($"[ComputerScreenController] Invalid view index: {index}");
            return;
        }

        Debug.Log($"[ComputerScreenController] ShowView({index}) — switching to \"{views[index].viewName}\"");

        // Deactivate all views, activate the target
        for (int i = 0; i < views.Length; i++)
        {
            bool isTarget = i == index;
            if (views[i].viewRoot != null)
                views[i].viewRoot.SetActive(isTarget);

            SetTabHighlight(views[i], isTarget);
        }

        _currentViewIndex = index;
        OnViewChanged?.Invoke(views[index].viewName);
    }

    /// <summary>Show a view by name (case-insensitive).</summary>
    public void ShowView(string viewName)
    {
        if (views == null) return;

        for (int i = 0; i < views.Length; i++)
        {
            if (string.Equals(views[i].viewName, viewName, StringComparison.OrdinalIgnoreCase))
            {
                ShowView(i);
                return;
            }
        }
        Debug.LogWarning($"[ComputerScreenController] View not found: \"{viewName}\"");
    }

    // ── Private Helpers ─────────────────────────────────────────────

    /// <summary>
    /// Automatically wires each tab button's onClick to show its corresponding view.
    /// </summary>
    private void WireTabButtons()
    {
        if (views == null) return;

        for (int i = 0; i < views.Length; i++)
        {
            if (views[i].tabButton == null)
            {
                Debug.Log($"[ComputerScreenController] View \"{views[i].viewName}\" has no tab button — it can only be opened via code.");
                continue;
            }

            int capturedIndex = i; // capture for closure
            string capturedName = views[i].viewName;
            views[i].tabButton.onClick.AddListener(() =>
            {
                Debug.Log($"[ComputerScreenController] Tab clicked: \"{capturedName}\"");
                ShowView(capturedIndex);
            });

            Debug.Log($"[ComputerScreenController] Wired tab button \"{views[i].tabButton.gameObject.name}\" → view \"{views[i].viewName}\"");
        }
    }

    /// <summary>
    /// Sets the tab button's normal color to indicate active/inactive state.
    /// </summary>
    private void SetTabHighlight(ScreenView view, bool isActive)
    {
        if (view.tabButton == null) return;

        Image tabImage = view.tabButton.GetComponent<Image>();
        if (tabImage != null)
            tabImage.color = isActive ? activeTabColor : inactiveTabColor;
    }
}
