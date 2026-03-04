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
    }

    [ClientRpc]
    private void NotifyItemRemovedClientRpc(ulong itemNetworkObjectId)
    {
        CounterSlot.UnregisterNetworkedCounterItem(itemNetworkObjectId);
    }
}
