using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Cash register that triggers NPC checkout when the player interacts with it.
/// Detected by ObjectPickup via raycast, just like other interactables.
///
/// Networking: ProcessCheckout() sends a ServerRpc so NPC state changes
/// happen on the host (server-authoritative NPC logic).
/// </summary>
public class CashRegister : NetworkBehaviour
{
    [Header("NPC Detection")]
    [Tooltip("Radius to check for NPCs at the counter.")]
    [SerializeField] private float npcDetectionRadius = 5f;

    [Header("Doppelganger")]
    [Tooltip("ShiftManager reference for reporting doppelganger escapes. Leave null if shift system is not active.")]
    [SerializeField] private ShiftManager shiftManager;

    /// <summary>
    /// Called by ObjectPickup when the player interacts with this cash register.
    /// Routes to the server so NPC state changes are authoritative.
    /// </summary>
    public void ProcessCheckout()
    {
        ProcessCheckoutServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ProcessCheckoutServerRpc()
    {
        // Find all NPCs in the scene directly
        NPCInteractionController[] allNPCs = FindObjectsOfType<NPCInteractionController>();
        NPCInteractionController bestCandidate = null;
        float closestDist = float.MaxValue;

        Debug.Log($"[CashRegister] ProcessCheckout: Found {allNPCs.Length} NPCs in scene.");

        foreach (var npc in allNPCs)
        {
            if (npc != null)
            {
                if (!npc.HasCheckedOut())
                {
                    float dist = Vector3.Distance(transform.position, npc.transform.position);
                    Debug.Log($"[CashRegister] Candidate: {npc.name} (Distance: {dist:F2}m)");

                    if (dist <= npcDetectionRadius)
                    {
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            bestCandidate = npc;
                        }
                    }
                    else
                    {
                        Debug.Log($"[CashRegister] NPC {npc.name} is too far ({dist:F2}m > {npcDetectionRadius}m).");
                    }
                }
                else
                {
                    Debug.Log($"[CashRegister] Ignoring NPC {npc.name}: Already checked out.");
                }
            }
        }

        if (bestCandidate != null)
        {
            Debug.Log($"[CashRegister] Processing checkout for NPC: {bestCandidate.name}");

            // Check doppelganger status before triggering checkout (server-only ground truth)
            if (bestCandidate.IsDoppelganger)
            {
                Debug.LogWarning($"[CashRegister] Doppelganger '{bestCandidate.name}' escaped! Approved by player.");
                if (shiftManager != null)
                    shiftManager.ReportEscape();
            }
            else
            {
                Debug.Log($"[CashRegister] Real patient '{bestCandidate.name}' correctly approved.");
            }

            bestCandidate.TriggerCheckout();
        }
        else
        {
            Debug.Log("[CashRegister] No eligible NPC found near counter to checkout.");
        }
    }
}
