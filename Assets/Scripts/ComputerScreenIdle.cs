using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple idle/powered-on display for the computer screen.
/// Shows a basic "desktop" wallpaper when the player is not focused on the monitor.
/// Attach to the idle screen root GameObject referenced by ComputerScreen.
/// </summary>
public class ComputerScreenIdle : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color wallpaperColor = new Color(0.05f, 0.12f, 0.28f, 1f);
    [SerializeField] private Color accentColor = new Color(0.24f, 0.52f, 0.88f, 0.6f);
    [SerializeField] private Color textColor = new Color(0.7f, 0.75f, 0.85f, 1f);

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

        BuildIdleScreen();
    }

    private void BuildIdleScreen()
    {
        RectTransform root = GetComponent<RectTransform>();
        if (root == null) return;

        // Background gradient (solid color stand-in)
        Image bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = wallpaperColor;

        // Center accent circle / logo placeholder
        GameObject circle = new GameObject("AccentCircle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        circle.transform.SetParent(root, false);
        RectTransform circleRect = circle.GetComponent<RectTransform>();
        circleRect.anchorMin = new Vector2(0.35f, 0.35f);
        circleRect.anchorMax = new Vector2(0.65f, 0.75f);
        circleRect.offsetMin = Vector2.zero;
        circleRect.offsetMax = Vector2.zero;
        Image circleImg = circle.GetComponent<Image>();
        circleImg.color = accentColor;

        // "PharmOS" label
        GameObject label = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        label.transform.SetParent(root, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.1f, 0.1f);
        labelRect.anchorMax = new Vector2(0.9f, 0.3f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text txt = label.GetComponent<Text>();
        txt.text = "PharmOS";
        txt.color = textColor;
        txt.fontSize = 96;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
