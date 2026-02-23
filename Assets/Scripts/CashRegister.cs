using UnityEngine;

/// <summary>
/// Cash register that triggers NPC checkout when the player interacts with it.
/// Detected by ObjectPickup via raycast, just like other interactables.
/// </summary>
public class CashRegister : MonoBehaviour
{
    [Header("NPC Detection")]
    [Tooltip("Radius to check for NPCs at the counter.")]
    [SerializeField] private float npcDetectionRadius = 5f;

    /// <summary>
    /// Called by ObjectPickup when the player interacts with this cash register.
    /// Finds the nearest eligible NPC and triggers their checkout.
    /// </summary>
    public void ProcessCheckout()
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
            bestCandidate.TriggerCheckout();
        }
        else
        {
            Debug.Log("[CashRegister] No eligible NPC found near counter to checkout.");
        }
    }
}
