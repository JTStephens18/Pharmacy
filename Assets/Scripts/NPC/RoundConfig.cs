using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class QueueEntry
{
    [Tooltip("If true, spawn the specific NPC prefab below. If false, pick randomly from the round's NPC pool.")]
    public bool isFixed;

    [Tooltip("The specific NPC prefab to spawn at this queue position (only used when Is Fixed is true).")]
    public GameObject fixedNpcPrefab;

    [Header("Doppelganger")]
    [Tooltip("If true, this queue position is always a doppelganger (authored set piece).")]
    public bool forceDoppelganger;

    [Tooltip("Specific doppelganger profile for authored doppelgangers. Only used when Force Doppelganger is true.")]
    public DoppelgangerProfile fixedProfile;
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

    [Header("Doppelganger Settings")]
    [Tooltip("Pool of doppelganger profiles available for random assignment to non-forced queue entries.")]
    public List<DoppelgangerProfile> doppelgangerPool = new List<DoppelgangerProfile>();

    [Tooltip("Number of random doppelgangers to assign (in addition to any forced ones). " +
             "Set to 0 to only use forced doppelgangers.")]
    public int randomDoppelgangerCount = 1;
}
