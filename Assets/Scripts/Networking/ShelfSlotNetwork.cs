using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NetworkBehaviour placed on each ShelfSection alongside a NetworkObject.
/// Owns a NetworkList of per-slot item counts so all clients (including
/// late joiners) agree on how many items are stocked in each slot.
///
/// Also provides TryFindSlotContaining() — a server-side scan used by
/// ObjectPickup when a player picks up a shelf item without a parent
/// relationship (NetworkObjects can't be parented to non-NetworkObjects).
///
/// Editor setup: On each ShelfSection GameObject in the scene, add both
/// a NetworkObject and this ShelfSlotNetwork component.
/// </summary>
[RequireComponent(typeof(ShelfSection))]
public class ShelfSlotNetwork : NetworkBehaviour
{
    // All live instances — used for the server-side static scan
    private static readonly HashSet<ShelfSlotNetwork> _all = new();

    private ShelfSection _section;
    private List<ShelfSlot> _slots;

    // One integer per slot: the authoritative CurrentItemCount for that slot
    private NetworkList<int> _slotCounts;

    void Awake()
    {
        _section = GetComponent<ShelfSection>();
        _slotCounts = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        _slots = new List<ShelfSlot>(_section.GetSlots());

        if (IsServer)
        {
            _slotCounts.Clear();
            foreach (ShelfSlot slot in _slots)
                _slotCounts.Add(slot.CurrentItemCount);
        }

        // Subscribe on all clients — OnListChanged also fires for every element
        // received from the server snapshot when a late client joins
        _slotCounts.OnListChanged += OnSlotCountChanged;

        // Push initial state on non-host clients (snapshot already applied but
        // OnListChanged may not fire for Add events — push manually to be safe)
        if (!IsServer)
        {
            for (int i = 0; i < _slotCounts.Count && i < _slots.Count; i++)
                _slots[i].SetNetworkedItemCount(_slotCounts[i]);
        }

        _all.Add(this);
    }

    public override void OnNetworkDespawn()
    {
        _slotCounts.OnListChanged -= OnSlotCountChanged;
        _all.Remove(this);
    }

    private void OnSlotCountChanged(NetworkListEvent<int> changeEvent)
    {
        if (IsServer) return; // server already has ground truth
        int i = changeEvent.Index;
        if (i >= 0 && i < _slots.Count)
            _slots[i].SetNetworkedItemCount(changeEvent.Value);
    }

    // ── Server-side write helpers ──────────────────────────────────

    /// <summary>Called server-side by ItemPlacementManager after placing an item.</summary>
    public void RecordPlacement(int slotIndex, int newCount)
    {
        _slotCounts[slotIndex] = newCount;
    }

    /// <summary>Called server-side by ObjectPickup after a shelf item is picked up.</summary>
    public void RecordPickup(int slotIndex, int newCount)
    {
        _slotCounts[slotIndex] = newCount;
    }

    public ShelfSlot GetSlotAt(int index) => (index >= 0 && index < _slots.Count) ? _slots[index] : null;
    public int SlotCount => _slots?.Count ?? 0;

    // ── Static slot scan ──────────────────────────────────────────

    /// <summary>
    /// Server-side: scans all registered ShelfSlotNetworks to find which slot
    /// contains the given item via its itemPlacements[].placedItem references.
    /// Works correctly because items are placed server-side in PlaceItemOnShelfServerRpc,
    /// so only the server's ShelfSlot instances have valid placedItem references.
    /// </summary>
    public static bool TryFindSlotContaining(
        GameObject item,
        out ShelfSlotNetwork sectionNetwork,
        out int slotIndex)
    {
        foreach (ShelfSlotNetwork net in _all)
        {
            if (net._slots == null) continue;
            for (int i = 0; i < net._slots.Count; i++)
            {
                if (net._slots[i].ContainsItem(item))
                {
                    sectionNetwork = net;
                    slotIndex = i;
                    return true;
                }
            }
        }
        sectionNetwork = null;
        slotIndex = -1;
        return false;
    }
}
