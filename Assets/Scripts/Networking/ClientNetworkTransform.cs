using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Owner-authoritative NetworkTransform.
/// The client who owns this object sends position/rotation to the host,
/// which replicates it to all other clients. Use this on the Player prefab
/// so each player controls their own movement without server round-trips.
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
