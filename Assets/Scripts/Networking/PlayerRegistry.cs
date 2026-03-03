using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry mapping network client IDs → PlayerComponents.
/// Populated by PlayerSetup.OnNetworkSpawn / OnNetworkDespawn for every
/// spawned player instance, regardless of ownership.
///
/// World scripts (e.g. NPCDialogueTrigger) should use:
///   PlayerRegistry.GetNearest(transform.position)  — nearest player to a world position
///   PlayerComponents.Local                          — the locally-owned player only
/// </summary>
public static class PlayerRegistry
{
    private static readonly Dictionary<ulong, PlayerComponents> _players = new();

    /// <summary>Read-only view of all registered players keyed by owner client ID.</summary>
    public static IReadOnlyDictionary<ulong, PlayerComponents> All => _players;

    /// <summary>
    /// Registers a spawned player. Called by PlayerSetup.OnNetworkSpawn.
    /// </summary>
    public static void Register(ulong clientId, PlayerComponents pc)
    {
        _players[clientId] = pc;
        Debug.Log($"[PlayerRegistry] Registered clientId={clientId}. Total players: {_players.Count}");
    }

    /// <summary>
    /// Removes a player from the registry. Called by PlayerSetup.OnNetworkDespawn.
    /// </summary>
    public static void Unregister(ulong clientId)
    {
        _players.Remove(clientId);
        Debug.Log($"[PlayerRegistry] Unregistered clientId={clientId}. Total players: {_players.Count}");
    }

    /// <summary>
    /// Returns the PlayerComponents whose movement transform is closest to worldPosition.
    /// Returns null if no players are registered.
    /// </summary>
    public static PlayerComponents GetNearest(Vector3 worldPosition)
    {
        PlayerComponents nearest = null;
        float bestDist = float.MaxValue;

        foreach (PlayerComponents pc in _players.Values)
        {
            if (pc == null || pc.Movement == null) continue;
            float d = Vector3.Distance(worldPosition, pc.Movement.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                nearest = pc;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Tries to get a player by owner client ID.
    /// </summary>
    public static bool TryGet(ulong clientId, out PlayerComponents pc)
        => _players.TryGetValue(clientId, out pc);

    /// <summary>
    /// Clears all registrations (e.g. on scene unload).
    /// </summary>
    public static void Clear() => _players.Clear();
}
