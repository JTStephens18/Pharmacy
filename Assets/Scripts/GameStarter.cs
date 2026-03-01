using UnityEngine;

public class GameStarter : MonoBehaviour
{
    [SerializeField] private NPCSpawnManager spawnManager;
    [SerializeField] private RoundConfig roundConfig;

    void Start()
    {
        spawnManager.StartNPCSpawning(roundConfig);
    }
}
