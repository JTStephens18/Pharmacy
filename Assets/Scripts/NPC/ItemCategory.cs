using UnityEngine;

/// <summary>
/// ScriptableObject representing an item category (e.g., Food, Drink, Cleaning).
/// Used by NPCs to filter which items they're interested in picking up.
/// To create a new category: Right-click in Project window → Create → NPC → Item Category
/// </summary>
[CreateAssetMenu(fileName = "NewItemCategory", menuName = "NPC/Item Category")]
public class ItemCategory : ScriptableObject
{
    [Tooltip("Optional description for editor reference.")]
    public string description;

    [Header("Prefab")]
    [Tooltip("The prefab to spawn when placing items of this category.")]
    public GameObject prefab;

    [Header("Placement Settings")]
    [Tooltip("Additional rotation (euler angles) applied when placing items of this category on a shelf. Use this to fix orientation issues (e.g. if items lie flat).")]
    public Vector3 shelfRotationOffset;
}
