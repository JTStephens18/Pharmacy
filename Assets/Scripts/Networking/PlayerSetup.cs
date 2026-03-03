using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Enables or disables player-owned components based on network ownership.
/// Attach to the Player prefab root alongside PlayerComponents.
///
/// Runs once on spawn for every instance of the player prefab:
///   - Owner  : enables all input/camera components, sets PlayerComponents.Local
///   - Non-owner: disables input/camera so remote players don't consume local input
/// </summary>
[RequireComponent(typeof(PlayerComponents))]
public class PlayerSetup : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        PlayerComponents pc = GetComponent<PlayerComponents>();
        Debug.Log($"[PlayerSetup] OnNetworkSpawn — IsOwner={IsOwner}, ClientId={OwnerClientId}, Camera={pc?.PlayerCamera}");

        // Register every spawned player so world scripts can find the nearest player
        PlayerRegistry.Register(OwnerClientId, pc);

        if (IsOwner)
        {
            // This is our local player — enable everything and register as Local
            PlayerComponents.Local = pc;

            pc.Movement.enabled = true;
            pc.Look.enabled = true;
            pc.Pickup.enabled = true;
            pc.PlacementManager.enabled = true;
            pc.FocusState.enabled = true;
            pc.PlayerCamera.enabled = true;

            AudioListener listener = pc.PlayerCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = true;
        }
        else
        {
            // This is someone else's player — disable all local-only components
            pc.Movement.enabled = false;
            pc.Look.enabled = false;
            pc.Pickup.enabled = false;
            pc.PlacementManager.enabled = false;
            pc.FocusState.enabled = false;
            pc.PlayerCamera.enabled = false;

            AudioListener listener = pc.PlayerCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        PlayerRegistry.Unregister(OwnerClientId);
    }
}
