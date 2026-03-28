using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Gun case interactable. Player presses E to take the gun (dropping whatever
/// they hold). Only one player can hold it at a time. They must return to this
/// case and press E again to put it away — no other way to unequip it.
///
/// Attach to the gun case GameObject. Add a NetworkObject component to the same
/// GameObject and register it in the NetworkManager's NetworkPrefabsList.
/// </summary>
public class GunCase : NetworkBehaviour
{
    [Header("Gun Models")]
    [Tooltip("The gun model sitting inside the case. Hidden while the gun is held.")]
    [SerializeField] private GameObject _gunCaseModel;

    [Tooltip("Prefab instantiated locally on the holding player's camera. Should have no Rigidbody.")]
    [SerializeField] private GameObject _heldGunPrefab;

    [Header("Hold Settings")]
    [SerializeField] private Vector3 _holdOffset   = new Vector3(0.3f, -0.3f, 0.6f);
    [SerializeField] private Vector3 _holdRotation = new Vector3(10f, -15f, 0f);

    [Header("Shooting")]
    [SerializeField] private float _shootRange = 50f;

    [Tooltip("BloodSplatterEffect prefab instantiated at the impact point on all clients.")]
    [SerializeField] private BloodSplatterEffect _bloodSplatterPrefab;

    [Header("Doppelganger")]
    [Tooltip("ShiftManager reference for doppelganger outcome tracking. Leave null if shift system is not active.")]
    [SerializeField] private ShiftManager _shiftManager;

    [Header("Highlight")]
    [Tooltip("Optional child GameObject used as a highlight (e.g. emissive outline mesh). Shown when interaction is available.")]
    [SerializeField] private GameObject _highlightObject;

    // ── Server-authoritative state ───────────────────────────────────────────
    // ulong.MaxValue means nobody holds the gun.
    private NetworkVariable<ulong> _holderClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Single-player (no NGO) fallback
    private bool _isHeldLocal;

    // Only exists on the client who currently holds the gun
    private GameObject _localHeldGun;

    // ── Public accessors ─────────────────────────────────────────────────────

    /// <summary>True if any player is currently holding the gun.</summary>
    public bool IsHeld => IsSpawned ? _holderClientId.Value != ulong.MaxValue : _isHeldLocal;

    /// <summary>True if the local player is the one currently holding the gun.</summary>
    public bool IsHeldByLocalPlayer => _localHeldGun != null;

    // ── Shooting ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (!IsHeldByLocalPlayer) return;

        // Block shooting while focused on a station (computer, pill counter, etc.)
        if (PlayerComponents.Local?.FocusState?.IsFocused == true) return;

        if (Input.GetMouseButtonDown(0))
            Shoot();
    }

    private void Shoot()
    {
        Camera cam = PlayerComponents.Local?.PlayerCamera;
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, _shootRange)) return;

        NPCInteractionController npc = hit.collider.GetComponent<NPCInteractionController>()
                                    ?? hit.collider.GetComponentInParent<NPCInteractionController>();
        if (npc == null) return;

        if (IsSpawned)
        {
            NetworkObject netObj = npc.GetComponent<NetworkObject>();
            if (netObj != null)
                ShootNPCServerRpc(netObj.NetworkObjectId, hit.point, hit.normal);
        }
        else
        {
            // Single-player fallback
            ReportShootOutcome(npc);
            npc.Kill();
            SpawnBloodSplatter(hit.point, hit.normal, Random.Range(int.MinValue, int.MaxValue));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ShootNPCServerRpc(ulong npcNetworkObjectId, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(npcNetworkObjectId, out var netObj)) return;

        NPCInteractionController npc = netObj.GetComponent<NPCInteractionController>();
        if (npc == null) return;

        // Check doppelganger status before killing (server-only ground truth)
        ReportShootOutcome(npc);

        npc.Kill();
        // Generate seed here (server) so all clients use the same value and
        // produce identical decal placement.
        int seed = Random.Range(int.MinValue, int.MaxValue);
        SpawnBloodSplatterClientRpc(hitPoint, hitNormal, seed);
    }

    [ClientRpc]
    private void SpawnBloodSplatterClientRpc(Vector3 hitPoint, Vector3 hitNormal, int seed)
    {
        SpawnBloodSplatter(hitPoint, hitNormal, seed);
    }

    private void SpawnBloodSplatter(Vector3 hitPoint, Vector3 hitNormal, int seed)
    {
        if (_bloodSplatterPrefab == null) return;
        BloodSplatterEffect effect = Instantiate(_bloodSplatterPrefab);
        effect.Initialize(hitPoint, hitNormal, seed);
    }

    private void ReportShootOutcome(NPCInteractionController npc)
    {
        if (npc.IsDoppelganger)
        {
            Debug.Log($"[GunCase] Doppelganger '{npc.name}' correctly eliminated.");
        }
        else
        {
            Debug.LogWarning($"[GunCase] Innocent patient '{npc.name}' was shot! Penalty applied.");
        }
    }

    // ── Highlight helpers ────────────────────────────────────────────────────

    public void ShowHighlight()
    {
        if (_highlightObject != null) _highlightObject.SetActive(true);
    }

    public void HideHighlight()
    {
        if (_highlightObject != null) _highlightObject.SetActive(false);
    }

    // ── Interaction entry point ──────────────────────────────────────────────

    /// <summary>
    /// Called by ObjectPickup when the local player presses E while looking at this case.
    /// </summary>
    public void TryInteract(ObjectPickup pickup)
    {
        if (IsHeldByLocalPlayer)
        {
            // Local player holds the gun → return it to the case
            ReturnGun();
        }
        else if (!IsHeld)
        {
            // Gun is free → drop current held item and pick it up
            pickup.ForceDropObject();
            PickupGun();
        }
        // else: another player holds it — do nothing
    }

    // ── Pickup ───────────────────────────────────────────────────────────────

    private void PickupGun()
    {
        if (IsSpawned)
        {
            RequestPickupServerRpc();
        }
        else
        {
            // Single-player fallback (no NGO)
            _isHeldLocal = true;
            if (_gunCaseModel != null) _gunCaseModel.SetActive(false);
            SpawnHeldGun();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ServerRpcParams rpcParams = default)
    {
        // Guard against race condition (two clients press E simultaneously)
        if (_holderClientId.Value != ulong.MaxValue) return;

        _holderClientId.Value = rpcParams.Receive.SenderClientId;
        ConfirmPickupClientRpc(_holderClientId.Value);
    }

    [ClientRpc]
    private void ConfirmPickupClientRpc(ulong clientId)
    {
        if (_gunCaseModel != null) _gunCaseModel.SetActive(false);

        if (NetworkManager.Singleton.LocalClientId == clientId)
            SpawnHeldGun();
    }

    // ── Return ───────────────────────────────────────────────────────────────

    private void ReturnGun()
    {
        if (IsSpawned)
        {
            RequestReturnServerRpc();
        }
        else
        {
            // Single-player fallback
            _isHeldLocal = false;
            if (_gunCaseModel != null) _gunCaseModel.SetActive(true);
            DestroyHeldGun();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestReturnServerRpc(ServerRpcParams rpcParams = default)
    {
        // Only the current holder can return the gun
        if (_holderClientId.Value != rpcParams.Receive.SenderClientId) return;

        ulong prevHolder = _holderClientId.Value;
        _holderClientId.Value = ulong.MaxValue;
        ConfirmReturnClientRpc(prevHolder);
    }

    [ClientRpc]
    private void ConfirmReturnClientRpc(ulong prevClientId)
    {
        if (_gunCaseModel != null) _gunCaseModel.SetActive(true);

        if (NetworkManager.Singleton.LocalClientId == prevClientId)
            DestroyHeldGun();
    }

    // ── Disconnect cleanup ───────────────────────────────────────────────────

    /// <summary>
    /// Called by DisconnectHandler when a client disconnects.
    /// Returns the gun to the case if the disconnecting client held it.
    /// </summary>
    public void ForceReleaseLock(ulong clientId)
    {
        if (!IsServer) return;
        if (_holderClientId.Value != clientId) return;

        ulong prevHolder = _holderClientId.Value;
        _holderClientId.Value = ulong.MaxValue;
        ConfirmReturnClientRpc(prevHolder);
    }

    // ── Held gun visuals (local client only) ─────────────────────────────────

    private void SpawnHeldGun()
    {
        if (_heldGunPrefab == null) return;

        Camera cam = PlayerComponents.Local?.PlayerCamera;
        if (cam == null) return;

        _localHeldGun = Instantiate(_heldGunPrefab, cam.transform);
        _localHeldGun.transform.localPosition = _holdOffset;
        _localHeldGun.transform.localRotation = Quaternion.Euler(_holdRotation);
    }

    private void DestroyHeldGun()
    {
        if (_localHeldGun != null)
        {
            Destroy(_localHeldGun);
            _localHeldGun = null;
        }
    }

    public override void OnNetworkDespawn()
    {
        // Clean up the local held gun if this case despawns while it's being held
        DestroyHeldGun();
    }
}
