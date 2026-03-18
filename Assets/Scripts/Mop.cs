using UnityEngine;

/// <summary>
/// Attach to the mop GameObject (alongside a Rigidbody so ObjectPickup can pick it up).
/// While held, the player holds Left Click to scrub nearby BloodDecals and clean them.
///
/// Editor setup:
///   - Add Rigidbody + Collider to the mop so ObjectPickup can grab it
///   - Optionally assign _mopHead to a child Transform at the bristle end for accurate
///     cleaning position; falls back to this transform if unassigned
/// </summary>
public class Mop : MonoBehaviour
{
    [Tooltip("Child transform at the bristle/cleaning end. Falls back to this transform if unassigned.")]
    [SerializeField] private Transform _mopHead;

    [Tooltip("Radius around the mop head that cleans blood decals.")]
    [SerializeField] private float _cleanRadius = 0.8f;

    [Tooltip("Seconds between each clean sweep while holding left click.")]
    [SerializeField] private float _cleanInterval = 0.15f;

    private float _cleanTimer;

    void Update()
    {
        // Only run cleaning logic when this mop is held by the local player
        if (PlayerComponents.Local?.Pickup?.GetHeldObject() != gameObject) return;

        if (!Input.GetMouseButton(0)) return;

        _cleanTimer -= Time.deltaTime;
        if (_cleanTimer > 0f) return;
        _cleanTimer = _cleanInterval;

        CleanNearby();
    }

    private void CleanNearby()
    {
        Vector3 cleanPoint = _mopHead != null ? _mopHead.position : transform.position;

        // Iterate backwards so Clean() → Destroy() doesn't invalidate forward indices
        for (int i = BloodDecal.Active.Count - 1; i >= 0; i--)
        {
            BloodDecal decal = BloodDecal.Active[i];
            if (decal == null) continue;

            if (Vector3.Distance(cleanPoint, decal.transform.position) <= _cleanRadius)
                decal.Clean();
        }
    }
}
