using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a placeholder UI for the computer screen at runtime.
/// Attach this to the same Canvas that ComputerScreen references as the interactive UI root.
/// Creates: a tab bar at the top, hero image area, and action buttons.
/// All generated at runtime so you don't need to set up UI elements manually.
/// Replace this with your real UI layout once you've settled on a design.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class ComputerScreenUI : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color backgroundColor = new Color(0.12f, 0.12f, 0.16f, 1f);
    [SerializeField] private Color tabBarColor = new Color(0.08f, 0.08f, 0.12f, 1f);
    [SerializeField] private Color activeTabColor = new Color(0.24f, 0.52f, 0.88f, 1f);
    [SerializeField] private Color inactiveTabColor = new Color(0.2f, 0.2f, 0.26f, 1f);
    [SerializeField] private Color buttonColor = new Color(0.24f, 0.52f, 0.88f, 1f);
    [SerializeField] private Color buttonHoverColor = new Color(0.30f, 0.58f, 0.94f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color imagePlaceholderColor = new Color(0.18f, 0.18f, 0.24f, 1f);

    [Header("Tab Names")]
    [SerializeField] private string[] tabNames = { "Dashboard", "Inventory", "Orders", "Settings" };

    [Header("Button Labels")]
    [SerializeField] private string[] buttonLabels = { "New Order", "View Reports", "Manage Stock" };

    void Start()
    {
        // Ensure this panel fills the entire canvas
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        BuildUI();
    }

    private void BuildUI()
    {
        RectTransform root = GetComponent<RectTransform>();

        // --- Background panel ---
        GameObject bgObj = CreatePanel("Background", root, backgroundColor);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        StretchFull(bgRect);

        // --- Tab bar at top ---
        GameObject tabBar = CreatePanel("TabBar", bgRect, tabBarColor);
        RectTransform tabBarRect = tabBar.GetComponent<RectTransform>();
        tabBarRect.anchorMin = new Vector2(0f, 0.88f);
        tabBarRect.anchorMax = new Vector2(1f, 1f);
        tabBarRect.offsetMin = Vector2.zero;
        tabBarRect.offsetMax = Vector2.zero;

        // Add horizontal layout for tabs
        HorizontalLayoutGroup tabLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 4f;
        tabLayout.padding = new RectOffset(8, 8, 4, 4);
        tabLayout.childAlignment = TextAnchor.MiddleLeft;
        tabLayout.childForceExpandWidth = false;
        tabLayout.childForceExpandHeight = true;

        for (int i = 0; i < tabNames.Length; i++)
        {
            CreateTab(tabBarRect, tabNames[i], i == 0);
        }

        // --- Image placeholder area ---
        GameObject imageArea = CreatePanel("ImageArea", bgRect, imagePlaceholderColor);
        RectTransform imageRect = imageArea.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.05f, 0.35f);
        imageRect.anchorMax = new Vector2(0.95f, 0.85f);
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        // Image label
        CreateLabel("ImageLabel", imageRect, "[ Image Preview ]",
            TextAnchor.MiddleCenter, 18);

        // --- Button row at bottom ---
        GameObject buttonRow = CreatePanel("ButtonRow", bgRect, Color.clear);
        RectTransform buttonRowRect = buttonRow.GetComponent<RectTransform>();
        buttonRowRect.anchorMin = new Vector2(0.05f, 0.05f);
        buttonRowRect.anchorMax = new Vector2(0.95f, 0.28f);
        buttonRowRect.offsetMin = Vector2.zero;
        buttonRowRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup btnLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        btnLayout.spacing = 12f;
        btnLayout.padding = new RectOffset(4, 4, 4, 4);
        btnLayout.childAlignment = TextAnchor.MiddleCenter;
        btnLayout.childForceExpandWidth = true;
        btnLayout.childForceExpandHeight = true;

        for (int i = 0; i < buttonLabels.Length; i++)
        {
            CreateButton(buttonRowRect, buttonLabels[i]);
        }
    }

    // ---------- Helper Methods ----------

    private GameObject CreatePanel(string name, RectTransform parent, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        return obj;
    }

    private void CreateTab(RectTransform parent, string label, bool isActive)
    {
        GameObject tab = CreatePanel("Tab_" + label, parent, isActive ? activeTabColor : inactiveTabColor);
        RectTransform tabRect = tab.GetComponent<RectTransform>();
        tabRect.sizeDelta = new Vector2(100f, 0f);

        // Make it a button
        Button btn = tab.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = activeTabColor;
        btn.colors = colors;

        CreateLabel("Label", tabRect, label, TextAnchor.MiddleCenter, 12);
    }

    private void CreateButton(RectTransform parent, string label)
    {
        GameObject btnObj = CreatePanel("Btn_" + label, parent, buttonColor);
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();

        // Round the button slightly
        Image img = btnObj.GetComponent<Image>();
        img.type = Image.Type.Sliced;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonHoverColor;
        colors.pressedColor = activeTabColor;
        btn.colors = colors;
        btn.targetGraphic = img;

        // Click feedback — just log for now
        string capturedLabel = label;
        btn.onClick.AddListener(() =>
        {
            Debug.Log($"[ComputerScreenUI] Button clicked: {capturedLabel}");
        });

        CreateLabel("Label", btnRect, label, TextAnchor.MiddleCenter, 14);
    }

    private void CreateLabel(string name, RectTransform parent, string text,
        TextAnchor alignment, int fontSize)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        StretchFull(rect);

        Text txt = obj.GetComponent<Text>();
        txt.text = text;
        txt.color = textColor;
        txt.fontSize = fontSize;
        txt.alignment = alignment;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
