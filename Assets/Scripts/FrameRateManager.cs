using UnityEngine;

public static class FrameRateManager
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void InitializeFrameRate()
    {
        // Disable VSync to allow targetFrameRate to work effectively
        QualitySettings.vSyncCount = 0;

        // Set target frame rate to 60
        Application.targetFrameRate = 60;
    }
}
