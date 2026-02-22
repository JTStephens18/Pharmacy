using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable data classes for the dialogue tree JSON format.
/// JSON is deserialized via JsonUtility, then nodes are indexed into a dictionary at runtime.
/// </summary>

[System.Serializable]
public class DialogueResponse
{
    [Tooltip("Text displayed on the response button.")]
    public string text;

    [Tooltip("ID of the node to navigate to when this response is chosen.")]
    public string nextNodeId;
}

[System.Serializable]
public class DialogueNode
{
    [Tooltip("Unique identifier for this node.")]
    public string id;

    [Tooltip("The dialogue text displayed to the player.")]
    public string text;

    [Tooltip("Optional speaker name override. If empty, uses the root speakerName.")]
    public string speakerName;

    [Tooltip("Available responses. Empty array = end of conversation.")]
    public DialogueResponse[] responses;

    /// <summary>Whether this node ends the conversation (no responses).</summary>
    public bool IsTerminal => responses == null || responses.Length == 0;
}

[System.Serializable]
public class DialogueData
{
    [Tooltip("Unique identifier for this dialogue tree.")]
    public string dialogueId;

    [Tooltip("Default speaker name (used when node doesn't override).")]
    public string speakerName;

    [Tooltip("ID of the first node to display.")]
    public string startNodeId;

    [Tooltip("All nodes in this dialogue tree.")]
    public DialogueNode[] nodes;
}

/// <summary>
/// Static utility for loading and indexing dialogue data from JSON TextAssets.
/// </summary>
public static class DialogueLoader
{
    /// <summary>
    /// Parses a TextAsset containing dialogue JSON and builds a runtime-ready dialogue.
    /// Returns the DialogueData with a companion dictionary for O(1) node lookups.
    /// </summary>
    public static DialogueData Load(TextAsset jsonAsset, out Dictionary<string, DialogueNode> nodeLookup)
    {
        nodeLookup = null;

        if (jsonAsset == null)
        {
            Debug.LogError("[DialogueLoader] Cannot load: TextAsset is null.");
            return null;
        }

        DialogueData data = JsonUtility.FromJson<DialogueData>(jsonAsset.text);

        if (data == null)
        {
            Debug.LogError($"[DialogueLoader] Failed to parse JSON from '{jsonAsset.name}'.");
            return null;
        }

        if (data.nodes == null || data.nodes.Length == 0)
        {
            Debug.LogWarning($"[DialogueLoader] Dialogue '{data.dialogueId}' has no nodes.");
            return data;
        }

        // Build dictionary for O(1) lookups
        nodeLookup = new Dictionary<string, DialogueNode>(data.nodes.Length);
        foreach (DialogueNode node in data.nodes)
        {
            if (string.IsNullOrEmpty(node.id))
            {
                Debug.LogWarning("[DialogueLoader] Skipping node with empty ID.");
                continue;
            }

            if (nodeLookup.ContainsKey(node.id))
            {
                Debug.LogWarning($"[DialogueLoader] Duplicate node ID '{node.id}' — keeping first occurrence.");
                continue;
            }

            nodeLookup[node.id] = node;
        }

        // Validate start node exists
        if (!string.IsNullOrEmpty(data.startNodeId) && !nodeLookup.ContainsKey(data.startNodeId))
        {
            Debug.LogError($"[DialogueLoader] Start node '{data.startNodeId}' not found in dialogue '{data.dialogueId}'.");
        }

        Debug.Log($"[DialogueLoader] Loaded dialogue '{data.dialogueId}' — {nodeLookup.Count} nodes, start: '{data.startNodeId}'");
        return data;
    }
}
