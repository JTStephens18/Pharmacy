using System.Collections;
using UnityEngine;

/// <summary>
/// Spawned at a bullet impact point. Plays a particle burst and fires a cone
/// of short raycasts to place BloodDecal prefabs on nearby surfaces.
///
/// Prefab setup:
///   - Attach this component to a GameObject
///   - Add a child ParticleSystem configured as a one-shot burst (Stop Action = Destroy)
///   - Assign _decalPrefabs (one or more BloodDecal quad prefabs for variation)
///   - Call Initialize(hitPoint, hitNormal) immediately after Instantiate
/// </summary>
public class BloodSplatterEffect : MonoBehaviour
{
    [Header("Decals")]
    [Tooltip("One or more BloodDecal prefabs. A random one is picked per decal spawn.")]
    [SerializeField] private GameObject[] _decalPrefabs;

    [Tooltip("How many decals to attempt to place via cone raycasts.")]
    [SerializeField] private int _decalCount = 7;

    [Tooltip("Max distance a scatter ray travels to find a surface.")]
    [SerializeField] private float _rayLength = 4f;

    [Tooltip("How far off the surface to offset the decal to avoid z-fighting.")]
    [SerializeField] private float _decalSurfaceOffset = 0.005f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem _bloodParticles;

    [Header("Scatter Cone")]
    [Tooltip("Half-angle of the scatter cone in degrees. Smaller = tighter grouping around the bullet direction.")]
    [SerializeField] private float _coneHalfAngle = 55f;

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <summary>
    /// Positions the effect, plays particles, and scatters decals on nearby surfaces.
    /// </summary>
    public void Initialize(Vector3 hitPoint, Vector3 hitNormal)
    {
        transform.position = hitPoint;

        if (_bloodParticles != null)
            _bloodParticles.Play();

        // Delay scatter by one frame so the NPC's collider has been destroyed
        // before raycasts fire. Without this, all rays hit the NPC and get filtered out.
        StartCoroutine(ScatterNextFrame(hitPoint, hitNormal));

        // Self-destruct after particles finish (or after scatter + buffer if no particles)
        float lifetime = _bloodParticles != null
            ? _bloodParticles.main.duration + _bloodParticles.main.startLifetime.constantMax + 0.5f
            : 0.5f;
        Destroy(gameObject, lifetime);
    }

    private IEnumerator ScatterNextFrame(Vector3 hitPoint, Vector3 hitNormal)
    {
        yield return null; // wait one frame for NPC Destroy() to process
        ScatterDecals(hitPoint, hitNormal);
    }

    // ── Decal placement ───────────────────────────────────────────────────────

    private void ScatterDecals(Vector3 origin, Vector3 surfaceNormal)
    {
        if (_decalPrefabs == null || _decalPrefabs.Length == 0)
        {
            Debug.LogWarning("[BloodSplatter] No decal prefabs assigned!");
            return;
        }

        // Cone center: blend bullet travel direction (-surfaceNormal) with downward
        // bias so decals favour floors even for wall shots.
        Vector3 coneCenter = Vector3.Lerp(-surfaceNormal, Vector3.down, 0.3f).normalized;

        for (int i = 0; i < _decalCount; i++)
        {
            Vector3 dir = RandomConeDirection(coneCenter, _coneHalfAngle);

            Vector3 rayStart = origin + dir * 0.15f;

            if (!Physics.Raycast(rayStart, dir, out RaycastHit hit, _rayLength, ~0, QueryTriggerInteraction.Ignore))
                continue;

            if (hit.collider.GetComponentInParent<NPCInteractionController>() != null)
                continue;

            SpawnDecal(hit.point, hit.normal);
        }
    }

    /// <summary>
    /// Returns a random unit vector within <paramref name="halfAngleDeg"/> degrees of
    /// <paramref name="center"/> (uniform distribution over the cone cap).
    /// </summary>
    private static Vector3 RandomConeDirection(Vector3 center, float halfAngleDeg)
    {
        float halfAngleRad = halfAngleDeg * Mathf.Deg2Rad;
        // Random height in [cos(halfAngle), 1] gives uniform area distribution on the cap
        float cosAngle = Mathf.Cos(halfAngleRad);
        float z = Random.Range(cosAngle, 1f);
        float r = Mathf.Sqrt(1f - z * z);
        float phi = Random.Range(0f, 2f * Mathf.PI);
        Vector3 local = new Vector3(r * Mathf.Cos(phi), r * Mathf.Sin(phi), z);

        // Rotate local cone (along +Z) to point along 'center'
        return Quaternion.FromToRotation(Vector3.forward, center) * local;
    }

    private void SpawnDecal(Vector3 point, Vector3 surfaceNormal)
    {
        GameObject prefab = _decalPrefabs[Random.Range(0, _decalPrefabs.Length)];
        if (prefab == null) return;

        // Align the quad so its face (+Z) points along the surface normal,
        // then spin it randomly around that axis for visual variety.
        Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, surfaceNormal);
        rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), surfaceNormal) * rotation;

        Instantiate(prefab, point + surfaceNormal * _decalSurfaceOffset, rotation);
    }
}
