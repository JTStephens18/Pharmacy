using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Interactable delivery station that spawns inventory boxes.
/// The spawned box inherits its inventory from the prefab configuration.
/// Place this on a GameObject in the scene where boxes should be delivered.
///
/// Networking: SpawnBox() sends a ServerRpc so the host instantiates and
/// network-spawns the box, making it visible to all clients.
/// Requires: InventoryBox prefab must have a NetworkObject component and be
/// registered in NetworkManager → NetworkPrefabsList.
/// </summary>
public class DeliveryStation : NetworkBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("The InventoryBox prefab to spawn. Inventory is inherited from prefab.")]
    [SerializeField] private GameObject inventoryBoxPrefab;

    [Tooltip("Where the box will spawn. If not set, uses this transform's position.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Visual Feedback")]
    [Tooltip("Optional highlight object to show when player is looking at station.")]
    [SerializeField] private GameObject highlightObject;

    [Header("Debug")]
    [SerializeField] private bool logOperations = false;

    /// <summary>
    /// Called by ObjectPickup when the player presses E on this station.
    /// Routes to the server so the spawned box appears for all clients.
    /// </summary>
    public void SpawnBox()
    {
        SpawnBoxServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnBoxServerRpc()
    {
        if (inventoryBoxPrefab == null)
        {
            Debug.LogWarning("[DeliveryStation] No inventory box prefab assigned!");
            return;
        }

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject boxObj = Instantiate(inventoryBoxPrefab, spawnPos, spawnRot);

        NetworkObject netObj = boxObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }
        else
        {
            Debug.LogWarning("[DeliveryStation] InventoryBox prefab has no NetworkObject — " +
                             "box will only appear on the host. Add NetworkObject to the prefab " +
                             "and register it in NetworkManager → NetworkPrefabsList.");
        }

        if (logOperations)
            Debug.Log($"[DeliveryStation] Spawned new inventory box at {spawnPos}");
    }

    /// <summary>
    /// Shows the highlight indicator when player is looking at the station.
    /// </summary>
    public void ShowHighlight()
    {
        if (highlightObject != null)
            highlightObject.SetActive(true);
    }

    /// <summary>
    /// Hides the highlight indicator.
    /// </summary>
    public void HideHighlight()
    {
        if (highlightObject != null)
            highlightObject.SetActive(false);
    }

    private void Start()
    {
        // Ensure highlight is hidden initially
        HideHighlight();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw spawn point in editor
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(spawnPos, new Vector3(0.5f, 0.5f, 0.5f));
        Gizmos.DrawLine(transform.position, spawnPos);
    }
#endif
}
