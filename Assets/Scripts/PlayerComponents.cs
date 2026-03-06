using UnityEngine;

/// <summary>
/// Central hub that holds references to all player-owned components.
/// Attach to the Player root GameObject. Auto-finds sibling and child components.
///
/// Replaces scattered singletons (MouseLook.Instance, FocusStateManager.Instance,
/// ItemPlacementManager.Instance) and FindFirstObjectByType calls with a single
/// well-defined access point.
///
/// Usage from player-owned scripts:
///   GetComponentInParent&lt;PlayerComponents&gt;()   — for sibling components on the same prefab
///
/// Usage from world objects that need "the player":
///   PlayerComponents.Local.Movement   — in single-player, always the one player
///   PlayerComponents.Local.Look       — etc.
///
/// In multiplayer, Local will point to the local (owned) player instance.
/// </summary>
public class PlayerComponents : MonoBehaviour
{
    /// <summary>
    /// The local player's components. In single-player this is the only player.
    /// In multiplayer this will be set by the owning client only.
    /// </summary>
    public static PlayerComponents Local { get; set; }

    // ── Cached References ────────────────────────────────────────────
    public PlayerMovement Movement { get; private set; }
    public MouseLook Look { get; private set; }
    public ObjectPickup Pickup { get; private set; }
    public ItemPlacementManager PlacementManager { get; private set; }
    public FocusStateManager FocusState { get; private set; }
    public Camera PlayerCamera { get; private set; }

    /// <summary>Per-player dialogue overlay manager. Lives on the Player prefab canvas.</summary>
    public DialogueManager Dialogue { get; private set; }

    /// <summary>Per-player dialogue history log. Lives alongside DialogueManager.</summary>
    public DialogueHistory DialogueHistory { get; private set; }

    void Awake()
    {
        // Auto-find components in the player hierarchy
        Movement = GetComponent<PlayerMovement>();
        Look = GetComponentInChildren<MouseLook>();
        Pickup = GetComponentInChildren<ObjectPickup>();
        PlacementManager = GetComponentInChildren<ItemPlacementManager>();
        FocusState = GetComponent<FocusStateManager>();
        PlayerCamera = GetComponentInChildren<Camera>();
        Dialogue = GetComponentInChildren<DialogueManager>();
        DialogueHistory = GetComponentInChildren<DialogueHistory>();

        if (Movement == null) Debug.LogWarning("[PlayerComponents] PlayerMovement not found on player hierarchy.");
        if (Look == null) Debug.LogWarning("[PlayerComponents] MouseLook not found on player hierarchy.");
        if (Pickup == null) Debug.LogWarning("[PlayerComponents] ObjectPickup not found on player hierarchy.");
        if (PlayerCamera == null) Debug.LogWarning("[PlayerComponents] Camera not found on player hierarchy.");
    }
}
