using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NetworkBehaviour placed on each CounterSlot alongside a NetworkObject.
/// Broadcasts to all clients when a NetworkObject item is placed on or removed from
/// this counter slot, so non-host clients can detect counter items without relying
/// on the parent-child hierarchy (items are positioned in world-space, not parented).
///
/// Editor setup: On each CounterSlot GameObject in the scene, add both a
/// NetworkObject and this CounterSlotNetwork component.
/// </summary>
[RequireComponent(typeof(CounterSlot))]
public class CounterSlotNetwork : NetworkBehaviour
{
    /// <summary>
    /// Called server-side (by NPCInteractionController) when a NetworkObject item
    /// is placed on this counter slot. Tells all clients to register the item.
    /// </summary>
    public void RecordPlacement(ulong itemNetworkObjectId)
    {
        NotifyItemPlacedClientRpc(itemNetworkObjectId);
    }

    /// <summary>
    /// Called server-side (by ObjectPickup.DeleteCounterItemServerRpc) before the
    /// item is despawned. Tells all clients to unregister it so stale entries are cleaned up.
    /// </summary>
    public void RecordRemoval(ulong itemNetworkObjectId)
    {
        NotifyItemRemovedClientRpc(itemNetworkObjectId);
    }

    [ClientRpc]
    private void NotifyItemPlacedClientRpc(ulong itemNetworkObjectId)
    {
        CounterSlot.RegisterNetworkedCounterItem(itemNetworkObjectId);
        Debug.Log($"[CounterSlotNetwork] ClientRpc: Registered counter item {itemNetworkObjectId} (IsServer={IsServer})");

        // Sync physics state to all clients so the item stays at the counter position.
        // The server sets Rigidbody to kinematic in CounterSlot.PlaceItem(), but that
        // doesn't replicate — clients still have gravity-enabled non-kinematic bodies
        // which fight NetworkTransform and cause the item to jitter/fall.
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkObjectId, out NetworkObject netObj))
        {
            // Ensure the item is active and visible on this client
            if (!netObj.gameObject.activeSelf)
                netObj.gameObject.SetActive(true);

            // Make Rigidbody kinematic so it doesn't fight NetworkTransform
            Rigidbody rb = netObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            // Ensure collider is enabled for raycast detection
            Collider col = netObj.GetComponent<Collider>();
            if (col != null)
                col.enabled = true;

            // Ensure renderers are visible
            Renderer[] renderers = netObj.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
                r.enabled = true;
        }
    }

    [ClientRpc]
    private void NotifyItemRemovedClientRpc(ulong itemNetworkObjectId)
    {
        CounterSlot.UnregisterNetworkedCounterItem(itemNetworkObjectId);
    }
}
