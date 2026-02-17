using UnityEngine;

/// <summary>
/// Interactable delivery station that spawns inventory boxes.
/// The spawned box inherits its inventory from the prefab configuration.
/// Place this on a GameObject in the scene where boxes should be delivered.
/// </summary>
public class DeliveryStation : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("The InventoryBox prefab to spawn. Inventory is inherited from prefab.")]
    [SerializeField] private GameObject inventoryBoxPrefab;

    [Tooltip("Where the box will spawn. If not set, uses this transform's position.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Visual Feedback")]
    [Tooltip("Optional highlight object to show when player is looking at station.")]
    [SerializeField] private GameObject highlightObject;

    [Header("Debug")]
    [SerializeField] private bool logOperations = false;

    /// <summary>
    /// Spawns a new inventory box at the spawn point.
    /// The box inherits its inventory from the prefab configuration.
    /// </summary>
    /// <returns>The spawned InventoryBox, or null if spawning failed.</returns>
    public InventoryBox SpawnBox()
    {
        if (inventoryBoxPrefab == null)
        {
            Debug.LogWarning("[DeliveryStation] No inventory box prefab assigned!");
            return null;
        }

        // Determine spawn position and rotation
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        // Instantiate the box - inventory is inherited from prefab
        GameObject boxObj = Instantiate(inventoryBoxPrefab, spawnPos, spawnRot);
        InventoryBox box = boxObj.GetComponent<InventoryBox>();

        if (box == null)
        {
            Debug.LogError("[DeliveryStation] Spawned prefab does not have an InventoryBox component!");
            Destroy(boxObj);
            return null;
        }

        if (logOperations)
            Debug.Log($"[DeliveryStation] Spawned new inventory box at {spawnPos}");

        return box;
    }


    /// <summary>
    /// Shows the highlight indicator when player is looking at the station.
    /// </summary>
    public void ShowHighlight()
    {
        if (highlightObject != null)
            highlightObject.SetActive(true);
    }

    /// <summary>
    /// Hides the highlight indicator.
    /// </summary>
    public void HideHighlight()
    {
        if (highlightObject != null)
            highlightObject.SetActive(false);
    }

    private void Start()
    {
        // Ensure highlight is hidden initially
        HideHighlight();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw spawn point in editor
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(spawnPos, new Vector3(0.5f, 0.5f, 0.5f));
        Gizmos.DrawLine(transform.position, spawnPos);
    }
#endif
}
