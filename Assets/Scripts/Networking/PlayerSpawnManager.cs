using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Assigns each connecting player to a designated spawn point Transform.
/// Players are assigned in array order; wraps around if there are more players than points.
///
/// Uses OnClientConnectedCallback + a targeted ClientRpc so the owning client teleports
/// itself — required because ClientNetworkTransform is owner-authoritative and would
/// override any position the server sets directly.
///
/// Editor setup:
///   1. Add this component to a persistent scene GameObject (e.g. NetworkManager object).
///   2. Fill Spawn Points with empty Transforms placed at each desired spawn location.
///   3. Connection Approval is NOT required — disable it on the NetworkManager.
///   4. Also disable "Enable Scene Management" on the NetworkManager to prevent NGO
///      from reloading the scene on each connection, which disrupts physics.
/// </summary>
public class PlayerSpawnManager : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints;

    private int _nextIndex;

    private void Start()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnServerStarted()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
        if (client.PlayerObject == null) return;

        PlayerSetup setup = client.PlayerObject.GetComponent<PlayerSetup>();
        if (setup == null) return;

        Transform point = spawnPoints[_nextIndex % spawnPoints.Length];
        _nextIndex++;

        setup.TeleportToSpawnClientRpc(
            point.position,
            point.rotation,
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } }
        );
    }
}
