using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach to the mop GameObject (alongside a Rigidbody so ObjectPickup can pick it up).
/// While held, the player holds Left Click to scrub nearby BloodDecals and clean them.
///
/// Multiplayer: Add NetworkObject + ClientNetworkTransform to the mop prefab so
/// ObjectPickup's existing networked pickup path handles position sync automatically.
/// Cleaning is broadcast via ServerRpc → ClientRpc so all clients remove the same decals.
///
/// Editor setup:
///   - Add Rigidbody + Collider to the mop so ObjectPickup can grab it
///   - Add NetworkObject + ClientNetworkTransform for multiplayer position sync
///   - Register the mop prefab in the NetworkManager's NetworkPrefabsList
///   - Optionally assign _mopHead to a child Transform at the bristle end for accurate
///     cleaning position; falls back to this transform if unassigned
/// </summary>
public class Mop : NetworkBehaviour
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

        if (IsSpawned)
        {
            // Send world-space clean point to the server, which broadcasts it to all clients
            // so every client removes the same decals from their local BloodDecal.Active list.
            CleanDecalsServerRpc(cleanPoint, _cleanRadius);
        }
        else
        {
            // Single-player / editor fallback
            DoCleanNearby(cleanPoint, _cleanRadius);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CleanDecalsServerRpc(Vector3 center, float radius)
    {
        CleanDecalsClientRpc(center, radius);
    }

    [ClientRpc]
    private void CleanDecalsClientRpc(Vector3 center, float radius)
    {
        DoCleanNearby(center, radius);
    }

    private void DoCleanNearby(Vector3 center, float radius)
    {
        // Iterate backwards so Clean() → Destroy() doesn't invalidate forward indices
        for (int i = BloodDecal.Active.Count - 1; i >= 0; i--)
        {
            BloodDecal decal = BloodDecal.Active[i];
            if (decal == null) continue;

            if (Vector3.Distance(center, decal.transform.position) <= radius)
                decal.Clean();
        }
    }
}
