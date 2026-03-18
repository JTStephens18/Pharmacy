using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-only: listens for client disconnects and force-releases any locks
/// (station access, dialogue) held by the departing client, and restores
/// physics on any objects they were holding.
///
/// Attach to a persistent scene GameObject (e.g. alongside NetworkManager).
/// </summary>
public class DisconnectHandler : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"[DisconnectHandler] Client {clientId} disconnected — releasing locks and restoring held objects.");

        ReleaseStationLocks(clientId);
        ReleaseDialogueLocks(clientId);
        RestoreHeldObjects(clientId);
    }

    /// <summary>
    /// Releases exclusive-access locks on ComputerScreen, PillCountingStation, and IDCardInteraction.
    /// </summary>
    private void ReleaseStationLocks(ulong clientId)
    {
        foreach (var screen in FindObjectsByType<ComputerScreen>(FindObjectsSortMode.None))
            screen.ForceReleaseLock(clientId);

        foreach (var station in FindObjectsByType<PillCountingStation>(FindObjectsSortMode.None))
            station.ForceReleaseLock(clientId);

        foreach (var card in FindObjectsByType<IDCardInteraction>(FindObjectsSortMode.None))
            card.ForceReleaseLock(clientId);

        foreach (var gunCase in FindObjectsByType<GunCase>(FindObjectsSortMode.None))
            gunCase.ForceReleaseLock(clientId);
    }

    /// <summary>
    /// Releases dialogue locks on all NPCDialogueTriggers.
    /// </summary>
    private void ReleaseDialogueLocks(ulong clientId)
    {
        foreach (var trigger in FindObjectsByType<NPCDialogueTrigger>(FindObjectsSortMode.None))
            trigger.ForceReleaseLock(clientId);
    }

    /// <summary>
    /// Finds any spawned NetworkObjects still owned by the disconnecting client
    /// and restores their physics (un-kinematic, gravity on, collider on).
    /// This handles items the player was holding when they disconnected.
    /// </summary>
    private void RestoreHeldObjects(ulong clientId)
    {
        // Collect matching objects first to avoid modifying the collection while iterating
        var spawnedObjects = NetworkManager.Singleton.SpawnManager.SpawnedObjectsList;
        foreach (var netObj in spawnedObjects)
        {
            if (netObj == null) continue;
            if (netObj.OwnerClientId != clientId) continue;

            // Skip player objects — they're handled by NGO's despawn
            if (netObj.IsPlayerObject) continue;

            // Transfer ownership back to server and restore physics
            netObj.ChangeOwnership(NetworkManager.ServerClientId);

            if (netObj.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                Debug.Log($"[DisconnectHandler] Restored physics on '{netObj.gameObject.name}' (was held by client {clientId}).");
            }

            // Re-enable collider if it was disabled during pickup
            if (netObj.TryGetComponent<Collider>(out var col))
            {
                col.enabled = true;
            }
        }
    }
}
