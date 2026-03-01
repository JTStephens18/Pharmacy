# Multiplayer Conversion Plan — Pharmacy Simulator

> **Goal**: 1–4 players in lobbies, one player as host (host-authoritative).
> **Recommended Framework**: Unity Netcode for GameObjects (NGO) — first-party, well-documented, supports host/client topology out of the box.

---

## Table of Contents

1. [Networking Foundation](#1-networking-foundation)
2. [Lobby System](#2-lobby-system)
3. [Player Prefab & Spawning](#3-player-prefab--spawning)
4. [Singleton Overhaul](#4-singleton-overhaul)
5. [Input & Camera Isolation](#5-input--camera-isolation)
6. [Object Pickup & Interaction](#6-object-pickup--interaction)
7. [Shelf & Inventory System](#7-shelf--inventory-system)
8. [NPC System](#8-npc-system)
9. [Counter & Checkout](#9-counter--checkout)
10. [Delivery Station](#10-delivery-station)
11. [Computer Screen & UI](#11-computer-screen--ui)
12. [ID Card Verification](#12-id-card-verification)
13. [Pill Counting Mini-Game](#13-pill-counting-mini-game)
14. [Dialogue System](#14-dialogue-system)
15. [Physics & Layers](#15-physics--layers)
16. [Cursor Management](#16-cursor-management)
17. [Scene & Lifecycle](#17-scene--lifecycle)
18. [Summary: File-by-File Changes](#18-summary-file-by-file-changes)

---

## 1. Networking Foundation

### Package Installation
- Install **Netcode for GameObjects** (`com.unity.netcode.gameobjects`) via Package Manager.
- Install **Unity Transport** (`com.unity.transport`) as the transport layer.
- Optionally install **Unity Lobby** + **Unity Relay** packages for matchmaking without port forwarding.

### Scene Setup
- Add a **NetworkManager** GameObject to the scene (or a bootstrap scene).
- Configure the `UnityTransport` component on it.
- Register all networked prefabs in `NetworkManager.NetworkConfig.Prefabs`.
- Set the **Player Prefab** field to the new multiplayer player prefab.

### Architecture Decision: Host-Authoritative
- The host runs both server and client logic.
- All world-state mutations (spawning objects, destroying objects, NPC AI, item placement) run on the host.
- Clients send input/requests via **ServerRpc**, host validates and applies changes.
- Host replicates state to clients via **NetworkVariable** or **ClientRpc**.

---

## 2. Lobby System

### New Scripts Needed

#### `LobbyManager.cs`
- Pre-game lobby UI (new scene or overlay).
- Host clicks "Create Lobby" → starts as host via `NetworkManager.Singleton.StartHost()`.
- Other players click "Join" → `NetworkManager.Singleton.StartClient()` with host IP/Relay code.
- Displays connected player list (subscribe to `NetworkManager.OnClientConnectedCallback`).
- Host has a "Start Game" button that triggers a scene load via `NetworkManager.SceneManager.LoadScene()`.

#### `LobbyPlayerSlot.cs`
- UI element per connected player showing name, ready status.
- Synced via `NetworkVariable<FixedString64Bytes>` for player name, `NetworkVariable<bool>` for ready state.

#### `PlayerData.cs` (NetworkBehaviour)
- Attached to the player prefab as a sibling to movement scripts.
- Holds `NetworkVariable`s for player name, color/skin index, score, etc.
- Persists across scene loads if using `DontDestroyOnLoad`.

### Integration with Unity Services (Optional)
- **Unity Relay**: Allows NAT traversal so players don't need to port-forward. Host gets a join code, others enter it.
- **Unity Lobby**: Cloud-based lobby listing. Players can browse or search for open lobbies.
- These are optional — a simpler approach is direct IP connect.

---

## 3. Player Prefab & Spawning

### Current State
- Single "Player" GameObject in the scene hierarchy with `CharacterController`, `PlayerMovement`, `MouseLook` (on camera child), `ObjectPickup` (on camera), `ItemPlacementManager`.
- No network identity. All scripts assume they are *the* player.

### Required Changes

#### Convert to a Network Player Prefab
1. Add `NetworkObject` component to the Player root.
2. Add `NetworkTransform` (or `ClientNetworkTransform` for owner-authoritative movement) to sync position/rotation.
3. Move the Player out of the scene hierarchy into a **Prefab** and assign it as the NetworkManager's Player Prefab.
4. NetworkManager auto-spawns one instance per connected client.

#### Local vs. Remote Player Setup
Add a new script `PlayerSetup.cs` (NetworkBehaviour):

```
OnNetworkSpawn():
    if (IsOwner):
        - Enable: PlayerMovement, MouseLook, ObjectPickup, ItemPlacementManager
        - Enable the Camera component (and AudioListener)
        - Set camera tag to "MainCamera"
        - Initialize per-player singletons (see §4)
    else:
        - Disable: PlayerMovement, MouseLook, ObjectPickup, ItemPlacementManager
        - Disable the Camera component and AudioListener
        - Optionally: enable a visible player model (body mesh) so others see you
```

#### Player Model
- Currently first-person only (no visible body). For multiplayer, add a body mesh visible to other players but hidden for the owner (set layer or disable renderer on owner's instance).

---

## 4. Singleton Overhaul

The codebase has 5 singletons that assume a single player. Each needs a different treatment.

### Singletons That Become Per-Player Components

| Singleton | Current Location | New Approach |
|---|---|---|
| `MouseLook.Instance` | Camera child | Remove singleton pattern. Each player prefab has its own `MouseLook`. Other scripts access it via the local player's reference chain, not `Instance`. |
| `FocusStateManager.Instance` | Player root | Remove singleton. Each player prefab has its own `FocusStateManager`. Only the local player's instance processes input. Remote players don't need focus transitions. |
| `ItemPlacementManager.Instance` | Player/Camera | Remove singleton. Per-player component. Ghost previews are local-only visuals. Actual placement sends a ServerRpc. |

### Singletons That Remain Scene-Global (Host-Only Logic)

| Singleton | Treatment |
|---|---|
| `DialogueManager.Instance` | Keep as singleton but make it **local-only UI**. Each client has its own `DialogueManager` instance controlling their own overlay. It no longer needs to be a scene singleton — it can be part of the player prefab's UI canvas. |
| `NPCInfoDisplay.Instance` | This is tied to a specific computer screen in the world. If there's one computer, only one player uses it at a time (see §11). Keep as world singleton but add an "in-use" lock. |

### All `FindFirstObjectByType<PlayerMovement>()` Calls
These appear in:
- `FocusStateManager.Start()` → Replace with direct reference on the same player prefab.
- `DialogueManager.StartDialogue()` / `EndDialogue()` → Replace with the local player's cached reference.
- `NPCDialogueTrigger.Start()` → Must find the **nearest** player, not *the* player. Change to `FindClosestPlayer()` helper or use a static player registry.
- `InventoryBox.ShrinkAndDestroy()` → Must find the player **currently holding this box** (track owner via `NetworkVariable<ulong>` storing `OwnerClientId`).

### Player Registry Pattern
Create a static `PlayerRegistry`:
```
static Dictionary<ulong, PlayerController> players;  // clientId → player
static PlayerController LocalPlayer;                  // shortcut for local
```
All scripts that need "the player" or "nearest player" use this registry instead of `FindFirstObjectByType`.

---

## 5. Input & Camera Isolation

### Problem
Every player-facing script reads `Input.GetKey*` / `Input.mousePosition` unconditionally in `Update()`. With multiple player instances, remote players' scripts would also read input and act on it.

### Solution
Every `Update()` in a player-owned script needs an early-out:

```csharp
void Update()
{
    if (!IsOwner) return;  // NetworkBehaviour.IsOwner
    // ... existing logic
}
```

#### Scripts Requiring This Guard

| Script | Input Methods Used |
|---|---|
| `PlayerMovement.cs` | `GetAxisRaw`, `GetKey`, `GetButtonDown` |
| `MouseLook.cs` | `GetAxisRaw("Mouse X/Y")` |
| `ObjectPickup.cs` | `GetKeyDown(E/F/G)` |
| `FocusStateManager.cs` | `GetKeyDown(Escape)` |
| `PillScraper.cs` | `mousePosition`, `GetMouseButton`, `GetKey` |
| `IDCardInteraction.cs` | `GetMouseButtonDown(0)`, `mousePosition` |
| `ItemPlacementManager.cs` | `GetKeyDown(Tab)` |
| `DialogueHistory.cs` | `GetKeyDown(Return)` |
| `ComputerScreenController.cs` | `GetMouseButtonDown(0)` (debug code) |

### Camera References
All `Camera.main` lookups must be replaced:
- On owner player: use the player's own camera (stored as a field on the player prefab).
- On remote players: camera is disabled, so no lookups needed.
- World objects that need "the interacting player's camera" (e.g., `ComputerScreen.EnsureEventCamera()`) must receive the camera reference from the player who activated them.

---

## 6. Object Pickup & Interaction

### `ObjectPickup.cs`

#### Current: 7 raycasts per frame from one camera
#### New: Only the local player raycasts. All mutations go through ServerRpc.

### Changes

1. **Make it a `NetworkBehaviour`**, add `if (!IsOwner) return;` at the top of `Update()`.

2. **Pickup flow**:
   - Client raycasts, finds a Rigidbody → sends `PickupServerRpc(networkObjectId)`.
   - Host validates (object exists, not held by another player, in range) → sets a `NetworkVariable<ulong> HeldByClientId` on the item → sends `PickupClientRpc` to the requesting client.
   - Only the owning client parents the object to their camera (visual only). Host tracks logical ownership.

3. **Throw/Drop**:
   - Client sends `ThrowServerRpc(direction, force)` or `DropServerRpc()`.
   - Host unparents, applies force, clears `HeldByClientId`.

4. **Counter item deletion ("bagging")**:
   - Client sends `BagItemServerRpc(networkObjectId)`.
   - Host validates and calls `NetworkObject.Despawn()` (networked destroy).

5. **Delivery station interaction**:
   - Client sends `SpawnBoxServerRpc()`.
   - Host spawns the box with `NetworkObject.Spawn()`.

6. **Computer/Pill station activation**:
   - Client sends `ActivateStationServerRpc(stationNetworkObjectId)`.
   - Host checks if station is free → marks it in-use → sends `ActivateStationClientRpc(clientId)`.
   - Only the requesting client enters focus mode locally.

### `HoldableItem.cs`
- No changes needed — it's data-only. Hold offsets are consumed locally by the owning client's `ObjectPickup`.

---

## 7. Shelf & Inventory System

### `ShelfSlot.cs` / `ShelfSection.cs`

#### State Synchronization
Each `ShelfSlot` needs to track its contents across the network:
- Add `NetworkObject` to shelf slot prefabs.
- Use a `NetworkList<NetworkObjectReference>` or `NetworkVariable<int>` (item count) to sync slot state.
- `PlaceItem()` and `RemoveItem()` become host-only operations.

#### Placement Flow (Networked)
1. Client holding an `InventoryBox` looks at a shelf slot → ghost preview shown locally.
2. Client presses E → `PlaceItemServerRpc(slotNetworkObjectId, itemCategoryIndex)`.
3. Host validates slot has room, box has items → spawns item prefab with `NetworkObject.Spawn()` → calls `ShelfSlot.PlaceItem()` on host → decrements box.
4. `NetworkTransform` on the spawned item syncs its position to all clients.

### `InventoryBox.cs`
- Add `NetworkObject` + `NetworkBehaviour`.
- `_remainingItems` → `NetworkVariable<int>` so all clients see the correct count.
- `Decrement()` is host-only.
- `ShrinkAndDestroy()` → host calls `NetworkObject.Despawn()`. The `FindFirstObjectByType<ObjectPickup>()` call must instead look up the player who owns this box (via a `NetworkVariable<ulong> HeldByClientId`) and send them a `ForceDropClientRpc`.

### `BoxItemPreview.cs`
- Purely visual. Can remain local. Reads from the box's `NetworkVariable<int>` count to update preview items.

### `ItemPlacementManager.cs`
- Remove singleton. Per-player component.
- Ghost previews remain client-local (no sync needed).
- `TryPlaceFromBox()` sends a ServerRpc instead of directly calling `ShelfSlot.PlaceItem()`.
- `MouseLook.Instance.Shake()` → call on the local player's `MouseLook` reference directly.

---

## 8. NPC System

### Host-Authoritative NPC AI
NPCs should only exist on the host, with their state replicated to clients.

#### `NPCInteractionController.cs`
- Add `NetworkObject` + `NetworkBehaviour`.
- All AI logic (`Update()` state machine, `NavMeshAgent` pathing) runs only on the host (`if (!IsServer) return;`).
- Add `NetworkVariable<NPCState>` to sync the current state enum to clients (for animation).
- `NetworkTransform` syncs position/rotation.
- Item pickup/placement calls are host-only (items are network-spawned).
- `TriggerCheckout()` is called via ServerRpc from `CashRegister`.
- `OnNPCExited` static event stays host-only. `NPCSpawnManager` only runs on the host.

#### `NPCSpawnManager.cs`
- Add `NetworkBehaviour`. All spawning logic gated on `if (!IsServer) return;`.
- `Instantiate()` → `Instantiate()` + `NetworkObject.Spawn()`.
- `AssignSceneReferences()` stays the same (host-only initialization).

#### `NPCAnimationController.cs`
- Reads from the NPC's `NetworkVariable<NPCState>` to set animator parameters on all clients.
- Alternatively, add a `NetworkAnimator` component to auto-sync animator state.

#### `InteractableItem.cs`
- Add `NetworkObject` + `NetworkBehaviour`.
- `OnPickedUp()` / `PlaceAt()` / `Release()` become host-only. Visual state (active/inactive, parent) synced via `NetworkVariable<bool> IsPickedUp` + `ClientRpc` for re-parenting visuals.

#### `GameStarter.cs`
- Gate on `if (!IsServer) return;` or check `NetworkManager.Singleton.IsServer` before calling `StartNPCSpawning()`.

---

## 9. Counter & Checkout

### `CounterSlot.cs`
- Add `NetworkObject` + `NetworkBehaviour`.
- Slot occupancy → `NetworkList` or `NetworkVariable<int>` for item count.
- `PlaceItem()` host-only. `RemoveItem()` host-only (triggered by player ServerRpc).
- `GetSlotContaining()` static method uses `FindObjectsByType` — replace with a static registry of all counter slots (populated in `OnNetworkSpawn`).

### `CashRegister.cs`
- Player presses E → `CheckoutServerRpc()`.
- Host finds the closest NPC to the **requesting player's position** (pass position in the RPC or look it up from the player's `NetworkObject`).
- Host calls `npc.TriggerCheckout()`.
- Remove `FindObjectsOfType<NPCInteractionController>()` from client code; host already has references.

### `IDCardSlot.cs`
- Add `NetworkObject`.
- `PlaceIDCard()` / `RemoveIDCard()` host-only (called by NPC controller on host).
- Card spawn: `Instantiate()` + `NetworkObject.Spawn()`.
- Card destroy: `NetworkObject.Despawn()`.

---

## 10. Delivery Station

### `DeliveryStation.cs`
- Player presses E → `SpawnBoxServerRpc()`.
- Host: `Instantiate(inventoryBoxPrefab)` + `GetComponent<NetworkObject>().Spawn()`.
- The spawned box is a networked object visible to all clients.
- Highlight visuals remain local (only the looking player sees them).

---

## 11. Computer Screen & UI

### Problem
The computer screen is a single shared world object. Its `ComputerScreen`, `ComputerScreenController`, and `NPCInfoDisplay` all assume one user.

### Approach: Exclusive Access with Lock

#### `ComputerScreen.cs`
- Add `NetworkVariable<ulong> CurrentUserId` (default = `ulong.MaxValue` meaning "nobody").
- Player presses E → `UseComputerServerRpc()`.
- Host checks if `CurrentUserId == ulong.MaxValue` (free):
  - Sets `CurrentUserId = requestingClientId`.
  - Sends `ActivateComputerClientRpc(clientId)` — only the targeted client runs `Activate()`.
- On Escape: client sends `ReleaseComputerServerRpc()` → host clears `CurrentUserId`.
- Other players see a "Computer in use" prompt instead of activating.

#### `ComputerScreenController.cs`
- View/tab state could remain local (each player enters the computer fresh with `ResetToMain()`).
- Remove the debug `Update()` input polling or guard it behind the current user check.

#### `NPCInfoDisplay.cs`
- Keep as a world singleton, but `ShowNPCInfo` / `ClearNPCInfo` are driven by the host (NPC lifecycle) and displayed to the player currently using the computer.
- When a player scans an ID card, the scan result (which `NPCIdentity` to display) is sent to the host, which updates a `NetworkVariable<int> DisplayedNPCIndex` on the `NPCInfoDisplay`. The currently-focused client reads this to show the panel.

#### Multiple Computers (Future)
If the game eventually has multiple computer stations, each would have its own `ComputerScreen` + `NPCInfoDisplay` instance. The lock pattern scales naturally.

---

## 12. ID Card Verification

### `IDCardInteraction.cs`
- Player presses E on the card → `FocusOnCardServerRpc()`.
- Host checks if the card is available → marks it in-use → `FocusOnCardClientRpc(clientId)`.
- Only the targeted client enters focus mode and performs the barcode scan.
- On barcode click: `ScanBarcodeServerRpc()` → host updates `NPCInfoDisplay` state.

### `IDCardVisuals.cs`
- No changes — visual-only, initialized once at spawn. Synced automatically via `NetworkTransform` on the card's `NetworkObject`.

---

## 13. Pill Counting Mini-Game

### Exclusive Access
Only one player at a time can use the pill station.

#### `PillCountingStation.cs`
- Add `NetworkVariable<ulong> CurrentUserId`.
- Player presses E → `ActivatePillStationServerRpc()`.
- Host checks if free → assigns → `ActivateClientRpc(clientId)`.
- Only the assigned client enters focus and interacts.

#### `PillSpawner.cs`
- Host spawns pills with `NetworkObject.Spawn()`. Use a seeded `Random` (seed sent to all clients) or spawn on host and sync via `NetworkTransform`.
- Pills need `NetworkObject` + `NetworkRigidbody` for physics sync.

#### `PillScraper.cs`
- Local-only on the using player. Input guarded by `if (!IsOwner) return;`.
- The scraper's kinematic movement drives pills via physics on the host.
- With client-authoritative scraper movement: add `ClientNetworkTransform` so the host sees the scraper position and runs physics collisions.

#### `PillCountingChute.cs`
- `OnTriggerEnter` runs on the host (physics authority).
- Count stored as `NetworkVariable<int>`.
- `OnTargetReached` event fires on host → host calls `DeactivateClientRpc(clientId)`.

#### `PillCountUI.cs`
- Reads from `NetworkVariable<int>` count. Updates locally for the focused player.

### `Physics.IgnoreLayerCollision` Issue
- This is a **global** setting. If two pill stations existed (or if the same one could somehow be used by two players), it would conflict.
- Mitigation: Since only one player uses the station at a time (exclusive lock), this is safe. But the toggle should be done on the host only.

---

## 14. Dialogue System

### Per-Player Dialogue
Each player has their own dialogue experience. Dialogue is **local UI only** — no need to sync dialogue state across clients.

#### `DialogueManager.cs`
- Move from a scene singleton to a **per-player component** on the player prefab (with its own Screen Space Overlay Canvas).
- Remove singleton pattern. Store as a field on the player's controller.
- `FindFirstObjectByType<PlayerMovement>()` → use the owning player's `PlayerMovement` directly.
- Camera lerp: use the owning player's camera, not `MouseLook.Instance`.
- Cursor lock changes: local-only (only affects the client running this instance).

#### `DialogueHistory.cs`
- Per-player component alongside `DialogueManager`. Each player has their own history.
- Input guard: `if (!IsOwner) return;` in `Update()`.

#### `NPCDialogueTrigger.cs`
- `FindFirstObjectByType<PlayerMovement>()` → Replace with a nearest-player lookup from the `PlayerRegistry`.
- Auto-trigger: should trigger for the **nearest player within range**, not a hardcoded single player.
- `StartDialogue()` must target a specific player's `DialogueManager`.
- For multiplayer: only one player converses with an NPC at a time. Add a `NetworkVariable<ulong> TalkingToClientId` lock.

#### `NPCInfoTalkButton.cs`
- Runs on the client using the computer. Finds the NPC matching the displayed identity.
- `ComputerScreen.TemporaryExitForDialogue()` only affects the local client.
- After dialogue ends, `ReactivateAfterDialogue()` only affects the local client.
- The `FindObjectsByType<NPCDialogueTrigger>()` scan stays local (NPC triggers exist on all clients as spawned network objects).

---

## 15. Physics & Layers

### `Physics.IgnoreLayerCollision`
- Called in `PillScraper.cs` for tool ↔ debris layer interaction.
- This is a global physics matrix setting, not per-object.
- Safe as long as the pill station is single-occupancy (which it is via the lock).
- Execute on host only.

### Rigidbody Synchronization
Every object that moves via physics needs one of:
- `NetworkRigidbody` (part of the NGO physics package) — syncs rigidbody state.
- `NetworkTransform` — syncs transform (less accurate for physics objects).
- Owner-authoritative: player-held items use `ClientNetworkTransform`.

### Objects Needing `NetworkRigidbody`
- Pills (during pill counting mini-game)
- Thrown/dropped items
- Inventory boxes after being dropped

### Objects Using `NetworkTransform` (kinematic/placed)
- Items placed on shelves (kinematic, position-synced once)
- Items on counter slots
- NPC-held items

---

## 16. Cursor Management

### Problem
`Cursor.lockState` and `Cursor.visible` are application-global. Multiple players on the same machine (split-screen) would conflict.

### Solution
- For **networked multiplayer** (separate machines): No conflict. Each client manages its own cursor independently. The existing cursor management code works as-is per client — just ensure only the local player's scripts modify it.
- For **split-screen on one machine**: Would require a custom input system (e.g., gamepads with virtual cursors). Out of scope for initial implementation.

### No Code Changes Needed (Networked)
Just ensure cursor lock/unlock calls are only executed on the local player's code path, which is already guaranteed by the `if (!IsOwner) return;` guards.

---

## 17. Scene & Lifecycle

### Scene Loading
- Use `NetworkManager.SceneManager.LoadScene()` for synchronized scene transitions.
- The lobby scene loads first; host triggers game scene load when all players are ready.
- `NetworkManager.SceneManager` handles spawning player prefabs in the new scene.

### `FrameRateManager.cs`
- No changes. Application-level setting, runs on every client independently.

### `GameStarter.cs`
- Gate behind `IsServer`: only the host starts NPC spawning.
- Alternatively, move this logic into `NPCSpawnManager.OnNetworkSpawn()`.

### Disconnect Handling
- Add a `DisconnectHandler.cs` script:
  - If a client disconnects: host cleans up their held items (drop them), releases any station locks they held.
  - If the host disconnects: clients return to lobby or show "Host disconnected" screen.
  - Items held by a disconnected player: host force-drops them at the player's last known position.

---

## 18. Summary: File-by-File Changes

### New Scripts to Create

| Script | Purpose |
|---|---|
| `LobbyManager.cs` | Lobby UI, host/join, player list, start game |
| `LobbyPlayerSlot.cs` | Per-player lobby UI element |
| `PlayerData.cs` | NetworkBehaviour: player name, score, etc. |
| `PlayerSetup.cs` | NetworkBehaviour: enable/disable components based on ownership |
| `PlayerRegistry.cs` | Static registry of all active players |
| `DisconnectHandler.cs` | Cleanup on player/host disconnect |
| `NetworkItemState.cs` | NetworkBehaviour on items: ownership, held-by tracking |

### Existing Scripts: Modification Summary

| Script | Changes |
|---|---|
| `PlayerMovement.cs` | Add `NetworkBehaviour`, `if (!IsOwner) return;` guard, `ClientNetworkTransform` |
| `MouseLook.cs` | Remove singleton. Add `NetworkBehaviour`, `if (!IsOwner) return;` guard |
| `ObjectPickup.cs` | Add `NetworkBehaviour`, `if (!IsOwner) return;`, convert all mutations to ServerRpcs |
| `HoldableItem.cs` | No changes |
| `FocusStateManager.cs` | Remove singleton. Per-player component. `if (!IsOwner) return;` guard |
| `ItemPlacementManager.cs` | Remove singleton. Per-player. ServerRpc for placement. Local ghost previews |
| `ShelfSlot.cs` | Add `NetworkBehaviour`, `NetworkVariable` for item count, host-only mutations |
| `ShelfSection.cs` | Add `NetworkBehaviour`, host-only `PlaceItem`/`RemoveItem` |
| `InventoryBox.cs` | Add `NetworkBehaviour`, `NetworkVariable<int>` for remaining items, host-only decrement |
| `BoxItemPreview.cs` | Read from `NetworkVariable`. Mostly unchanged |
| `DeliveryStation.cs` | ServerRpc for box spawning, host spawns with `NetworkObject.Spawn()` |
| `CounterSlot.cs` | Add `NetworkBehaviour`, `NetworkVariable` state, host-only mutations, replace `FindObjectsByType` with static registry |
| `CashRegister.cs` | ServerRpc for checkout, host finds NPC and triggers checkout |
| `IDCardSlot.cs` | Add `NetworkObject`, host-only spawn/destroy |
| `IDCardInteraction.cs` | ServerRpc for focus + scan, exclusive lock, per-player camera ref |
| `IDCardVisuals.cs` | No changes (initialized at spawn) |
| `NPCInfoDisplay.cs` | Add `NetworkVariable` for displayed identity, update for current computer user |
| `NPCIdentityField.cs` | No changes |
| `NPCInteractionController.cs` | Add `NetworkBehaviour`, host-only AI, `NetworkVariable<NPCState>`, networked events |
| `NPCSpawnManager.cs` | Add `NetworkBehaviour`, host-only spawning, `NetworkObject.Spawn()` |
| `NPCAnimationController.cs` | Read from `NetworkVariable<NPCState>` or use `NetworkAnimator` |
| `InteractableItem.cs` | Add `NetworkBehaviour`, host-only state changes, `NetworkVariable<bool>` for pickup state |
| `GameStarter.cs` | Gate on `IsServer` |
| `ComputerScreen.cs` | Add `NetworkVariable<ulong>` user lock, ServerRpc for activate/release |
| `ComputerScreenController.cs` | Guard debug input, tab state is local to the active user |
| `PillCountingStation.cs` | Add `NetworkVariable<ulong>` user lock, ServerRpc for activate |
| `PillSpawner.cs` | Host-only spawning with `NetworkObject.Spawn()` |
| `PillScraper.cs` | Per-player, `ClientNetworkTransform`, local input only |
| `PillCountingChute.cs` | Host-only trigger, `NetworkVariable<int>` count |
| `PillCountUI.cs` | Read from `NetworkVariable<int>` |
| `DialogueManager.cs` | Remove singleton. Per-player component with own canvas |
| `DialogueHistory.cs` | Per-player. `if (!IsOwner) return;` guard |
| `NPCDialogueTrigger.cs` | Replace `FindFirstObjectByType<PlayerMovement>()` with player registry nearest-player lookup |
| `NPCInfoTalkButton.cs` | Works on local client only. No structural changes beyond reference updates |
| `DialogueData.cs` | No changes |
| `FrameRateManager.cs` | No changes |
| `RoundConfig.cs` | No changes |
| `ItemCategory.cs` | No changes |
| `NPCIdentity.cs` | No changes |
| `IInteractable.cs` | No changes |
| `IPlaceable.cs` | No changes |

### Prefabs Needing `NetworkObject`

| Prefab | Additional Components |
|---|---|
| Player | `NetworkObject`, `ClientNetworkTransform`, `PlayerSetup`, `PlayerData` |
| All NPC prefabs | `NetworkObject`, `NetworkTransform`, `NetworkAnimator` |
| All item prefabs (shelf items) | `NetworkObject`, `NetworkTransform` |
| InventoryBox prefab | `NetworkObject`, `NetworkTransform`, `NetworkRigidbody` |
| ID Card prefab | `NetworkObject`, `NetworkTransform` |
| Pill prefab | `NetworkObject`, `NetworkRigidbody` |
| Counter slots (scene) | `NetworkObject` (in-scene placed) |
| Shelf slots (scene) | `NetworkObject` (in-scene placed) |

---

## Implementation Order (Recommended)

1. **Networking foundation** — Install packages, add `NetworkManager`, create bootstrap/lobby scene.
2. **Player prefab** — Convert to networked player with `PlayerSetup.cs`, ownership guards, per-player camera.
3. **Player registry** — Replace all `FindFirstObjectByType<PlayerMovement>()` calls.
4. **Remove singletons** — `MouseLook`, `FocusStateManager`, `ItemPlacementManager`, `DialogueManager` become per-player.
5. **Object pickup** — ServerRpc/ClientRpc flow for pickup, throw, drop.
6. **Shelf & inventory** — Network-synced shelf slots, box decrement, item placement.
7. **NPC system** — Host-only AI, network-spawned NPCs, synced animation.
8. **Counter & checkout** — Networked counter slots, ServerRpc checkout.
9. **Delivery station** — ServerRpc box spawning.
10. **Computer screen** — Exclusive access lock, per-user focus.
11. **ID card** — Networked card spawning, exclusive scan.
12. **Pill counting** — Exclusive access, networked pill physics, host-only chute counting.
13. **Dialogue** — Per-player dialogue UI, nearest-player NPC triggers.
14. **Lobby polish** — Ready states, player names, disconnect handling.
15. **Testing** — 2-player local testing (two Unity editors via ParrelSync or separate builds), then 4-player stress test.
