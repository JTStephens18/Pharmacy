using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCSpawnManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Where NPCs appear in the scene.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Seconds to wait before spawning the first NPC.")]
    [SerializeField] private float initialDelay = 2f;

    [Tooltip("Seconds to wait after the previous NPC despawns before spawning the next.")]
    [SerializeField] private float delayAfterExit = 3f;

    [Header("Shared Scene References")]
    [Tooltip("Counter slots assigned to every spawned NPC.")]
    [SerializeField] private List<CounterSlot> counterSlots = new List<CounterSlot>();

    [Tooltip("Exit point assigned to every spawned NPC.")]
    [SerializeField] private Transform exitPoint;

    [Tooltip("ID card slot assigned to every spawned NPC.")]
    [SerializeField] private IDCardSlot idCardSlot;

    [Tooltip("Shelf slots assigned to every spawned NPC.")]
    [SerializeField] private List<ShelfSlot> allowedShelfSlots = new List<ShelfSlot>();

    public event Action<NPCInteractionController> OnNPCSpawned;
    public event Action OnAllNPCsFinished;

    private Queue<GameObject> _spawnQueue = new Queue<GameObject>();
    private NPCInteractionController _activeNPC;
    private Coroutine _spawnCoroutine;
    private bool _isSpawning;

    public bool IsSpawning => _isSpawning;

    public void StartNPCSpawning(RoundConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[NPCSpawnManager] RoundConfig is null.");
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogError("[NPCSpawnManager] Spawn point is not assigned.");
            return;
        }

        StopNPCSpawning();

        List<GameObject> resolvedQueue = ResolveQueue(config);
        _spawnQueue = new Queue<GameObject>(resolvedQueue);

        NPCInteractionController.OnNPCExited += HandleNPCExited;
        _isSpawning = true;
        _spawnCoroutine = StartCoroutine(SpawnCoroutine());
    }

    public void StopNPCSpawning()
    {
        NPCInteractionController.OnNPCExited -= HandleNPCExited;

        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }

        _spawnQueue.Clear();
        _activeNPC = null;
        _isSpawning = false;
    }

    private List<GameObject> ResolveQueue(RoundConfig config)
    {
        List<GameObject> availablePool = new List<GameObject>(config.npcPool);
        List<GameObject> resolved = new List<GameObject>();

        foreach (QueueEntry entry in config.queueEntries)
        {
            if (entry.isFixed)
            {
                if (entry.fixedNpcPrefab == null)
                {
                    Debug.LogWarning("[NPCSpawnManager] Fixed queue entry has no prefab assigned. Skipping.");
                    continue;
                }

                resolved.Add(entry.fixedNpcPrefab);
                availablePool.Remove(entry.fixedNpcPrefab);
            }
            else
            {
                if (availablePool.Count == 0)
                {
                    Debug.LogWarning("[NPCSpawnManager] NPC pool exhausted. Skipping remaining random entries.");
                    break;
                }

                int index = UnityEngine.Random.Range(0, availablePool.Count);
                GameObject picked = availablePool[index];
                resolved.Add(picked);
                availablePool.RemoveAt(index);
            }
        }

        return resolved;
    }

    private IEnumerator SpawnCoroutine()
    {
        yield return new WaitForSeconds(initialDelay);

        while (_spawnQueue.Count > 0)
        {
            SpawnNextNPC();
            yield return new WaitUntil(() => _activeNPC == null);

            if (_spawnQueue.Count > 0)
            {
                yield return new WaitForSeconds(delayAfterExit);
            }
        }

        _isSpawning = false;
        _spawnCoroutine = null;
        NPCInteractionController.OnNPCExited -= HandleNPCExited;
        OnAllNPCsFinished?.Invoke();
    }

    private void SpawnNextNPC()
    {
        GameObject prefab = _spawnQueue.Dequeue();
        GameObject npcObject = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        _activeNPC = npcObject.GetComponent<NPCInteractionController>();

        if (_activeNPC == null)
        {
            Debug.LogError($"[NPCSpawnManager] Spawned prefab '{prefab.name}' has no NPCInteractionController.");
            return;
        }

        _activeNPC.AssignSceneReferences(counterSlots, exitPoint, idCardSlot, allowedShelfSlots);
        OnNPCSpawned?.Invoke(_activeNPC);
    }

    private void HandleNPCExited(NPCInteractionController npc)
    {
        if (npc == _activeNPC)
        {
            _activeNPC = null;
        }
    }

    private void OnDestroy()
    {
        NPCInteractionController.OnNPCExited -= HandleNPCExited;
    }
}
