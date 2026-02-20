using UnityEngine;

/// <summary>
/// Spawns pill instances in a randomized cluster on the tray surface.
/// Pills are simple capsule-shaped rigidbodies on the Physics_Debris layer.
/// </summary>
public class PillSpawner : MonoBehaviour
{
    [Header("Pill Prefab")]
    [Tooltip("Assign a pill prefab (capsule/sphere with Rigidbody + Collider). If null, a primitive capsule is created.")]
    [SerializeField] private GameObject pillPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private int defaultPillCount = 30;
    [SerializeField] private float spawnAreaWidth = 0.8f;
    [SerializeField] private float spawnAreaDepth = 0.8f;
    [SerializeField] private float spawnHeight = 0.3f;
    [SerializeField] private float pillScale = 0.08f;

    [Header("Pill Physics")]
    [SerializeField] private float pillMass = 0.1f;
    [SerializeField] private float pillDrag = 2f;
    [SerializeField] private float pillAngularDrag = 1f;

    [Header("Layer")]
    [Tooltip("Layer index for Physics_Debris. Must match Project Settings.")]
    [SerializeField] private int debrisLayerIndex = 9;

    [Header("Physic Material")]
    [Tooltip("Optional: assign a PhysicMaterial with high friction / 0 bounce. One is created at runtime if left empty.")]
    [SerializeField] private PhysicsMaterial pillPhysicMaterial;

    private GameObject _pillContainer;

    /// <summary>
    /// Spawns the given number of pills (or the default count) on the tray.
    /// </summary>
    public void SpawnPills(int count = -1)
    {
        if (count <= 0) count = defaultPillCount;

        // Create a container to keep the hierarchy tidy
        if (_pillContainer != null) ClearPills();
        _pillContainer = new GameObject("SpawnedPills");
        _pillContainer.transform.SetParent(transform);
        _pillContainer.transform.localPosition = Vector3.zero;

        // Auto-create physic material if not assigned
        if (pillPhysicMaterial == null)
        {
            pillPhysicMaterial = new PhysicsMaterial("PillPhysicMat")
            {
                dynamicFriction = 0.8f,
                staticFriction = 0.9f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 localPos = new Vector3(
                Random.Range(-spawnAreaWidth * 0.5f, spawnAreaWidth * 0.5f),
                spawnHeight + Random.Range(0f, 0.15f),
                Random.Range(-spawnAreaDepth * 0.5f, spawnAreaDepth * 0.5f)
            );

            Vector3 worldPos = transform.TransformPoint(localPos);
            Quaternion randomRot = Random.rotation;

            GameObject pill;
            if (pillPrefab != null)
            {
                pill = Instantiate(pillPrefab, worldPos, randomRot, _pillContainer.transform);
            }
            else
            {
                // Fallback: create a primitive capsule
                pill = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                pill.transform.SetParent(_pillContainer.transform);
                pill.transform.position = worldPos;
                pill.transform.rotation = randomRot;
                pill.transform.localScale = Vector3.one * pillScale;
            }

            pill.name = $"Pill_{i}";
            pill.layer = debrisLayerIndex;

            // Ensure Rigidbody
            Rigidbody rb = pill.GetComponent<Rigidbody>();
            if (rb == null) rb = pill.AddComponent<Rigidbody>();

            rb.mass = pillMass;
            rb.linearDamping = pillDrag;
            rb.angularDamping = pillAngularDrag;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Assign physic material to all colliders
            Collider[] cols = pill.GetComponents<Collider>();
            foreach (var col in cols)
            {
                col.material = pillPhysicMaterial;
            }
        }

        Debug.Log($"[PillSpawner] Spawned {count} pills.");
    }

    /// <summary>
    /// Destroys all spawned pills.
    /// </summary>
    public void ClearPills()
    {
        if (_pillContainer != null)
        {
            Destroy(_pillContainer);
            _pillContainer = null;
        }
    }

    /// <summary>
    /// Returns the number of currently spawned pills.
    /// </summary>
    public int GetActivePillCount()
    {
        if (_pillContainer == null) return 0;
        return _pillContainer.transform.childCount;
    }
}
