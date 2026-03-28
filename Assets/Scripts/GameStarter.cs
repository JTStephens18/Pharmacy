using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Kicks off the game at the right moment depending on whether NGO is running.
///
/// Routes through <see cref="ShiftManager"/> when assigned (the new default path).
/// Falls back to calling <see cref="NPCSpawnManager"/> directly if no ShiftManager is present,
/// preserving backwards compatibility for scenes that haven't been updated yet.
///
/// Networked (host/client flow):
///   Start() fires before NetworkManager.StartHost() is called by QuickConnect.
///   We subscribe to OnServerStarted so the game is deferred until the server is live.
///   Only the server actually starts the shift (ShiftManager / NPCSpawnManager guard the client path).
///
/// Non-networked (editor solo testing without NGO):
///   NetworkManager.Singleton is null or not listening — starts immediately in Start().
/// </summary>
public class GameStarter : MonoBehaviour
{
    [Header("New Path (preferred)")]
    [Tooltip("Assign the ShiftManager to drive the full day/night cycle. When set, spawnManager and roundConfig below are ignored — ShiftManager owns those references.")]
    [SerializeField] private ShiftManager shiftManager;

    [Header("Legacy Path (fallback)")]
    [Tooltip("Only used when ShiftManager is not assigned. Directly starts NPC spawning.")]
    [SerializeField] private NPCSpawnManager spawnManager;

    [Tooltip("Only used when ShiftManager is not assigned.")]
    [SerializeField] private RoundConfig roundConfig;

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            // Networked: wait for the server to start.
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        }
        else
        {
            // Non-networked (editor solo play without NGO): start immediately.
            BeginGame();
        }
    }

    private void OnServerStarted()
    {
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        BeginGame();
    }

    private void BeginGame()
    {
        if (shiftManager != null)
        {
            // New path: ShiftManager drives everything.
            shiftManager.StartDayShift();
        }
        else if (spawnManager != null)
        {
            // Legacy fallback: direct NPC spawning (no shift cycle).
            spawnManager.StartNPCSpawning(roundConfig);
        }
        else
        {
            Debug.LogWarning("[GameStarter] No ShiftManager or NPCSpawnManager assigned.");
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
    }
}
