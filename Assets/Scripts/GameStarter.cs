using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Kicks off NPC spawning at the right moment depending on whether NGO is running.
///
/// Networked (host/client flow):
///   Start() fires before NetworkManager.StartHost() is called by QuickConnect.
///   We subscribe to OnServerStarted so spawning is deferred until the server is live.
///   Only the server actually starts spawning (NPCSpawnManager guards the client path).
///
/// Non-networked (editor solo testing without NGO):
///   NetworkManager.Singleton is null or not listening — spawning starts immediately in Start().
/// </summary>
public class GameStarter : MonoBehaviour
{
    [SerializeField] private NPCSpawnManager spawnManager;
    [SerializeField] private RoundConfig roundConfig;

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            // Networked: wait for the server to start before spawning NPCs.
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        }
        else
        {
            // Non-networked (editor solo play without NGO): start immediately.
            spawnManager.StartNPCSpawning(roundConfig);
        }
    }

    private void OnServerStarted()
    {
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        spawnManager.StartNPCSpawning(roundConfig);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
    }
}
