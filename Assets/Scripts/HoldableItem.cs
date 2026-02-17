using UnityEngine;

/// <summary>
/// Optional component to override the default hold offset/rotation
/// when an object is picked up by the player. Attach to any pickupable
/// object (one that already has a Rigidbody).
/// If absent, ObjectPickup uses its own default values.
/// </summary>
public class HoldableItem : MonoBehaviour
{
    [Tooltip("If true, uses the custom values below instead of ObjectPickup defaults.")]
    public bool useCustomHoldSettings = true;

    [Tooltip("Local position offset when held (relative to player camera).")]
    public Vector3 holdOffset = new Vector3(0.3f, -0.3f, 0.6f);

    [Tooltip("Local rotation (euler angles) when held.")]
    public Vector3 holdRotation = new Vector3(10f, -15f, 0f);
}
