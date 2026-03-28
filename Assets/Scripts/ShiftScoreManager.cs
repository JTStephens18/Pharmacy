using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative score tracker for each shift.
/// All values are NetworkVariables — readable by all clients for HUD display.
/// Only the server modifies values (via Record* methods called from CashRegister / GunCase).
///
/// Attach to the same persistent scene GameObject as ShiftManager.
/// ShiftManager calls ResetForNewShift() at the start of each day shift.
/// </summary>
public class ShiftScoreManager : NetworkBehaviour
{
    // ── Networked scores ──────────────────────────────────────────────

    public NetworkVariable<int> Money { get; } =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CustomersServed { get; } =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> DoppelgangersCaught { get; } =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> DoppelgangersEscaped { get; } =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> InnocentsKilled { get; } =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Tuning ────────────────────────────────────────────────────────

    [Header("Rewards & Penalties")]
    [Tooltip("Money earned for correctly approving a real patient.")]
    [SerializeField] private int correctApprovalReward = 50;

    [Tooltip("Money earned for correctly killing a doppelganger.")]
    [SerializeField] private int correctKillReward = 25;

    [Tooltip("Money lost for shooting an innocent patient.")]
    [SerializeField] private int wrongKillPenalty = 100;

    // ── Events (server-side, for UI or other systems) ─────────────────

    /// <summary>Fired after any score change. Arg = this manager (for reading updated values).</summary>
    public event Action<ShiftScoreManager> OnScoreChanged;

    // ── Public API (server-only) ──────────────────────────────────────

    /// <summary>Real patient correctly approved at the cash register.</summary>
    public void RecordCorrectApproval()
    {
        if (!IsServerOrLocal()) return;

        Money.Value += correctApprovalReward;
        CustomersServed.Value++;
        OnScoreChanged?.Invoke(this);

        Debug.Log($"[ShiftScoreManager] Correct approval. Money: {Money.Value}, Served: {CustomersServed.Value}");
    }

    /// <summary>Doppelganger approved at the cash register — it escapes.</summary>
    public void RecordWrongApproval()
    {
        if (!IsServerOrLocal()) return;

        DoppelgangersEscaped.Value++;
        OnScoreChanged?.Invoke(this);

        Debug.Log($"[ShiftScoreManager] Doppelganger escaped! Escaped: {DoppelgangersEscaped.Value}");
    }

    /// <summary>Doppelganger correctly shot with the gun.</summary>
    public void RecordCorrectKill()
    {
        if (!IsServerOrLocal()) return;

        Money.Value += correctKillReward;
        DoppelgangersCaught.Value++;
        OnScoreChanged?.Invoke(this);

        Debug.Log($"[ShiftScoreManager] Doppelganger caught! Money: {Money.Value}, Caught: {DoppelgangersCaught.Value}");
    }

    /// <summary>Innocent patient shot with the gun — penalty.</summary>
    public void RecordWrongKill()
    {
        if (!IsServerOrLocal()) return;

        Money.Value -= wrongKillPenalty;
        InnocentsKilled.Value++;
        OnScoreChanged?.Invoke(this);

        Debug.LogWarning($"[ShiftScoreManager] Innocent killed! Money: {Money.Value}, Innocents: {InnocentsKilled.Value}");
    }

    /// <summary>
    /// Resets per-shift counters (not money). Called by ShiftManager at the start of each day shift.
    /// Money persists across shifts.
    /// </summary>
    public void ResetForNewShift()
    {
        if (!IsServerOrLocal()) return;

        CustomersServed.Value = 0;
        DoppelgangersCaught.Value = 0;
        DoppelgangersEscaped.Value = 0;
        InnocentsKilled.Value = 0;
        OnScoreChanged?.Invoke(this);

        Debug.Log("[ShiftScoreManager] Counters reset for new shift. Money carried over.");
    }

    private bool IsServerOrLocal()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return true;
        return IsServer;
    }
}
