using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class QueueEntry
{
    [Tooltip("If true, spawn the specific NPC prefab below. If false, pick randomly from the round's NPC pool.")]
    public bool isFixed;

    [Tooltip("The specific NPC prefab to spawn at this queue position (only used when Is Fixed is true).")]
    public GameObject fixedNpcPrefab;
}

[CreateAssetMenu(fileName = "NewRoundConfig", menuName = "NPC/Round Config")]
public class RoundConfig : ScriptableObject
{
    [Header("NPC Pool")]
    [Tooltip("Pool of NPC prefabs available for random selection. Fixed NPCs are automatically excluded from random picks.")]
    public List<GameObject> npcPool = new List<GameObject>();

    [Header("Queue")]
    [Tooltip("Ordered list of queue positions. Each entry is either a fixed NPC or a random pick from the pool.")]
    public List<QueueEntry> queueEntries = new List<QueueEntry>();
}
