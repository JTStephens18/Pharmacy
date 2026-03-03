using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Temporary on-screen buttons to start host/client without a lobby.
/// Attach to any scene GameObject. Remove once a real lobby is implemented.
/// </summary>
public class QuickConnect : MonoBehaviour
{
    void OnGUI()
    {
        // Hide buttons once session is running
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsListening)
            return;

        GUILayout.BeginArea(new Rect(10, 10, 200, 80));
        if (GUILayout.Button("Start Host", GUILayout.Height(35)))
            NetworkManager.Singleton.StartHost();
        if (GUILayout.Button("Start Client", GUILayout.Height(35)))
            NetworkManager.Singleton.StartClient();
        GUILayout.EndArea();
    }
}
