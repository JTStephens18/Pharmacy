# Pharmacy Simulator — Codebase Reference

> **Purpose**: Source of truth for LLMs and developers. Describes every game system, how they connect, and how to set them up in the Unity Editor.

---

## Project Overview

A first-person pharmacy simulator where the player manages a shop: restocking shelves from inventory boxes, processing NPC customers who browse and bring items to the counter, running a cash register checkout, counting pills in a mini-game, and managing orders via a computer screen UI.

**Engine**: Unity (C#)  
**Render Pipeline**: Standard  
**Target FPS**: 60 (set by `FrameRateManager`, VSync disabled)  
**Input**: Legacy `Input` system (keyboard + mouse)

---

## Directory Structure

```
Assets/Scripts/
├── PlayerComponents.cs        # Central hub for all player component references
├── PlayerMovement.cs          # FPS movement + jumping
├── MouseLook.cs               # FPS camera look + screen shake
├── ObjectPickup.cs            # Central interaction hub (pickup, throw, place, interact)
├── HoldableItem.cs            # Per-item hold offset overrides
├── FrameRateManager.cs        # Static 60 FPS initializer
├── ShiftManager.cs            # Server-authoritative day/night cycle state machine
├── GameStarter.cs             # Routes to ShiftManager.StartDayShift() (or legacy NPCSpawnManager fallback)
├── CashRegister.cs            # Checkout trigger
├── ComputerScreen.cs          # Computer focus + UI activation
├── ComputerScreenController.cs# View/tab manager for computer UI
├── NPCInfoDisplay.cs          # Singleton: shows/hides NPC info panel on scan + bridges to PrescriptionDisplay
├── NPCIdentityField.cs        # Component: binds a TMP text to one NPCIdentity field (supports doppelganger overrides)
├── PrescriptionDisplay.cs     # Singleton: shows/hides prescription panel on scan (doppelganger-aware)
├── PrescriptionField.cs       # Component: binds a TMP text to one PrescriptionData field
├── NPISearchPanel.cs          # NPI lookup panel for computer screen prescriber database view
│
├── Counter/
│   ├── CounterSlot.cs         # NPC item placement + player "bagging"
│   ├── IDCardSlot.cs          # Counter spot for NPC ID card
│   ├── IDCardInteraction.cs   # Focus mode + barcode scanning on ID card
│   └── IDCardVisuals.cs       # Physical card visuals: photo (SpriteRenderer) + name (TMP 3D)
│
├── Delivery/
│   └── DeliveryStation.cs     # Spawns InventoryBox prefabs
│
├── NPC/
│   ├── IInteractable.cs       # Interface: NPC-pickable objects
│   ├── IPlaceable.cs          # Interface: player-placeable targets
│   ├── InteractableItem.cs    # Shelf item that NPCs pick up
│   ├── ItemCategory.cs        # ScriptableObject: item type + prefab
│   ├── NPCIdentity.cs         # ScriptableObject: NPC personal data for ID card
│   ├── PrescriptionData.cs    # ScriptableObject: medication, dosage, prescriber info
│   ├── DoppelgangerProfile.cs # ScriptableObject: fake data overrides + discrepancy types
│   ├── PrescriberDatabase.cs  # ScriptableObject: valid prescriber NPI lookup table
│   ├── RoundConfig.cs         # ScriptableObject: NPC queue config per round + doppelganger assignment
│   ├── NPCSpawnManager.cs     # Sequential NPC spawner from RoundConfig queue
│   ├── NPCInteractionController.cs  # NPC state machine (13 states)
│   └── NPCAnimationController.cs    # Drives Animator from NPC state
│
├── PillCounting/
│   ├── FocusStateManager.cs   # Singleton: FPS ↔ focus camera transitions
│   ├── PillCountingStation.cs # Mini-game lifecycle manager
│   ├── PillSpawner.cs         # Spawns pill rigidbodies on tray
│   ├── PillScraper.cs         # Mouse-driven kinematic scraper tool
│   ├── PillCountingChute.cs   # Trigger zone that counts pills
│   └── PillCountUI.cs         # World-space count display
│
├── Shelf/
│   ├── ShelfSection.cs        # IPlaceable shelf with multiple slots
│   ├── ShelfSlot.cs           # Individual slot with multi-item support
│   ├── InventoryBox.cs        # Portable item container (decrements + shrinks)
│   ├── BoxItemPreview.cs      # Visual preview of next items inside box
│   └── ItemPlacementManager.cs# Box-to-shelf placement workflow + ghost previews
│
└── Dialogue/
    ├── DialogueData.cs        # Data classes + JSON loader
    ├── DialogueManager.cs     # Singleton: dialogue UI overlay controller
    ├── DialogueHistory.cs     # Conversation history log (ENTER to toggle)
    ├── NPCDialogueTrigger.cs  # Per-NPC: auto-triggers dialogue at counter
    └── NPCInfoTalkButton.cs   # NPCInfoPanel button: exits PC focus → dialogue → re-enters PC focus

Assets/Data/Dialogues/
├── npc_customer_01.json           # Sample branching dialogue
├── npc_customer_01_info.json      # Info/verification dialogue (key: "default")
├── npc_customer_01_dob.json       # DOB verification dialogue (key: "dob")
└── npc_customer_01_address.json   # Address verification dialogue (key: "address")
```

---

## System 1: Player Controls

### PlayerMovement.cs
Attached to the **Player** GameObject (requires `CharacterController`).

| Feature | Details |
|---|---|
| Walk/Sprint | WASD, hold Shift to sprint (forward only) |
| Jump | Space, with coyote time (0.15s) + jump buffering (0.1s) |
| Ground Check | Uses `CharacterController.isGrounded` |

**Inspector fields**: `walkSpeed`, `sprintSpeed`, `jumpHeight`, `gravity`, `groundCheck` (Transform), `groundMask` (LayerMask).

### PlayerComponents.cs
Attached to the **Player** root GameObject. Central hub that holds references to all player-owned components, replacing scattered singletons and `FindFirstObjectByType` calls.

| Property | Type | Auto-found From |
|---|---|---|
| `Local` (static) | `PlayerComponents` | Set in `Awake()` — the local player instance |
| `Movement` | `PlayerMovement` | `GetComponent` on Player root |
| `Look` | `MouseLook` | `GetComponentInChildren` (Camera child) |
| `Pickup` | `ObjectPickup` | `GetComponentInChildren` (Camera child) |
| `PlacementManager` | `ItemPlacementManager` | `GetComponentInChildren` |
| `FocusState` | `FocusStateManager` | `GetComponent` on Player root |
| `PlayerCamera` | `Camera` | `GetComponentInChildren` (Camera child) |

**Usage from player-owned scripts**: `GetComponentInParent<PlayerComponents>()`
**Usage from world objects**: `PlayerComponents.Local.FocusState`, `PlayerComponents.Local.PlayerCamera`, etc.

**Editor setup**: Add `PlayerComponents` component to the Player root GameObject. No inspector fields needed — all references are auto-found from the player hierarchy.

### MouseLook.cs
Attached to the **Camera** (child of Player). Accessed via `PlayerComponents.Look`.

| Feature | Details |
|---|---|
| Look | Mouse X/Y with configurable sensitivity |
| Smoothing | SmoothDamp (default), Lerp, or raw input |
| Acceleration | Optional mouse acceleration above threshold |
| Screen Shake | `Shake(intensity, duration)` — callable from any script |

**Editor setup**: Assign `playerBody` to the Player root Transform.

---

## System 2: Focus State Manager

**Script**: `FocusStateManager.cs` — Attach to the **Player** root GameObject.
**Access**: `PlayerComponents.Local.FocusState` (or `GetComponentInParent<PlayerComponents>().FocusState` from player scripts)

Manages transitions between **FPS mode** and **focused mode** (computer screen, pill counting). When entering focus:

1. Saves camera parent, local position, and rotation
2. Disables `PlayerMovement`, `MouseLook`, `ObjectPickup`
3. Unlocks + shows cursor (`CursorLockMode.Confined`)
4. Lerps camera to target Transform over `transitionDuration` (0.6s)
5. On exit (Escape key): reverse lerps camera back, re-parents, re-enables FPS scripts

**API** (via PlayerComponents):
```csharp
PlayerComponents.Local.FocusState.EnterFocus(Transform target, Action onExit);
PlayerComponents.Local.FocusState.ExitFocus();
```

**Events**: `OnFocusChanged(bool entering)` — subscribe for custom focus reactions.

**Auto-finds** at Start: `PlayerMovement`, `MouseLook`, `ObjectPickup`, `Camera` from the `PlayerComponents` on the same hierarchy.

---

## System 3: Object Pickup & Interaction

**Script**: `ObjectPickup.cs` — Attach to the **Camera**.

This is the **central interaction hub**. Every frame it raycasts from the camera and handles:

### Holding Objects
- **E** to pick up non-kinematic Rigidbody objects (one at a time)
- Held object follows camera at a fixed offset in the lower-right corner
- `HoldableItem.cs` (optional) overrides hold offset/rotation per object
- **Left Click** = throw, **G** = gentle drop

### Interaction Targets (detected via raycast each frame)

| Target | Detection | Action on E |
|---|---|---|
| `ShelfSection` / `ShelfSlot` | `DetectPlaceable()` — checks `IPlaceable` | Places held item via `ItemPlacementManager` or `IPlaceable.TryPlaceItem` |
| `DeliveryStation` | `DetectDeliveryStation()` | Calls `SpawnBox()` to create an InventoryBox |
| `CounterSlot` item | `DetectCounterItem()` | Deletes (bags) the looked-at counter item |
| `PillCountingStation` | `DetectSortingStation()` | Calls `station.Activate()` → enters focus |
| `ComputerScreen` | `DetectComputerScreen()` | Calls `screen.Activate()` → enters focus |

### Shelf Placement Workflow (when holding InventoryBox near shelves)
1. `ItemPlacementManager` activates and builds an item queue from nearby `ShelfSection.GetMissingItems()`
2. Ghost previews appear on valid slots showing what will be placed
3. Player looks at a slot and presses E → spawns item prefab from `ItemCategory.prefab`
4. `InventoryBox.Decrement()` reduces box count; at 0 the box shrinks + destroys

**Editor setup**: Assign `playerCamera`, `holdPoint` (Transform), `interactKey` (default E), `throwForce`, `pickupRange`, `placementManager` reference.

---

## System 4: Shelf & Inventory

### ItemCategory.cs (ScriptableObject)
Create via **Right-click → Create → NPC → Item Category**.

| Field | Purpose |
|---|---|
| `description` | Editor reference |
| `prefab` | The item prefab to spawn on shelves |
| `shelfRotationOffset` | Euler offset to fix item orientation |

### ShelfSlot.cs
Each slot holds multiple instances of one `ItemCategory`. Positioned in the editor as child GameObjects of a `ShelfSection`.

| Field | Purpose |
|---|---|
| `acceptedCategory` | Only this ItemCategory can go here |
| `maxItems` | Max items per slot (default via `itemPlacements` array size) |
| `itemPlacements[]` | Array defining local offset positions for each item |

**Key methods**: `PlaceItem(GameObject)`, `RemoveItem()`, `ShowHighlight()`, `HideHighlight()`.

### ShelfSection.cs (implements `IPlaceable`)
Groups multiple `ShelfSlot` children. Auto-finds child slots on Awake if `autoFindSlots` is true.

**Key methods**: `CanPlaceItem()`, `TryPlaceItem()`, `GetMissingItems()` (returns list of needed `ItemCategory`), `RemoveFirstItem()`.

### InventoryBox.cs
Attach to a pickupable box prefab (needs `Rigidbody`).

| Feature | Details |
|---|---|
| `totalItems` | How many items before box is depleted |
| Open/Close visuals | Swaps between `closedModel` and `openModel` with scale animation |
| Depletion | `Decrement()` reduces count; at 0, shrinks to zero and self-destructs |

### BoxItemPreview.cs
Attach alongside `InventoryBox`. Shows visual previews of the current and next items inside the box.

**Editor setup**: Assign `itemSlot1` and `itemSlot2` Transforms as anchor points inside the box mesh.

### ItemPlacementManager.cs
Attach to the Player alongside `ObjectPickup`.

Manages the full box-to-shelf workflow:
1. Detects nearby `ShelfSection`s via `OverlapSphere`
2. Builds a **locked item queue** from missing items (`GetMissingItems()`)
3. Shows **ghost previews** (translucent prefab instances) on valid slots
4. On E press: spawns real item, decrements box, advances queue
5. Updates `BoxItemPreview` with upcoming items

**Key fields**: `shelfDetectionRadius`, `maxShelfSections`, `ghostMaterial`, `queueLockCount`.

---

## System 5: NPC Behavior

### NPCInteractionController.cs
Attach to NPC GameObject (requires `NavMeshAgent`).

**State machine** with these states:

```
Idle → MovingToItem → WaitingAtItem → Pickup → 
  ├── MovingToItem (collect more)
  └── MovingToCounter → Placing → WaitingForCheckout → MovingToExit → (Destroy)
```

| State | What happens |
|---|---|
| `Idle` | Periodically scans for items if `autoScan` enabled |
| `MovingToItem` | NavMesh navigates to target shelf item |
| `WaitingAtItem` | Brief pause before pickup |
| `Pickup` | Calls `InteractableItem.OnPickedUp()`, hides item, parents to NPC hand |
| `MovingToCounter` | Navigates to `counterTarget` Transform |
| `Placing` | Places each held item into `CounterSlot.PlaceItem()` one by one |
| `WaitingForCheckout` | Idle until `TriggerCheckout()` called by CashRegister |
| `MovingToExit` | Navigates to `exitTarget` Transform, then self-destructs |

**Item source**: Can scan scene for `InteractableItem` or take items directly from assigned `ShelfSlot[]` references.

**Events**: `OnPickupStart`, `OnPlaceStart` — used by `NPCAnimationController`.
**Static event**: `OnNPCExited(NPCInteractionController)` — fired just before destroy at all exit paths. Used by `NPCSpawnManager` to trigger next spawn.

**Scene ref injection**: `AssignSceneReferences(counterSlots, exitPoint, idCardSlot, shelfSlots)` — called by `NPCSpawnManager` after instantiation. These fields are left empty on prefabs since they reference scene objects.

**Editor setup**: On prefabs, assign `handTransform` (child bone), `npcIdentity`, `idCardPrefab`, `wantedCategories`. Leave `counterSlots`, `exitPoint`, `idCardSlot`, `allowedShelfSlots` empty — injected at runtime by `NPCSpawnManager`.

### NPCAnimationController.cs
Attach alongside `NPCInteractionController`. Auto-finds `Animator` in children.

| Animator Parameter | Type | Purpose |
|---|---|---|
| `IsWalking` | bool | Walk/idle blend |
| `Speed` | float | Animation speed scaling |
| `PickUp` | trigger | Pickup animation |
| `Place` | trigger | Place-on-counter animation |

### InteractableItem.cs (implements `IInteractable`)
Attach to shelf item prefabs. Requires `Collider`.

| Method | Purpose |
|---|---|
| `OnPickedUp(handTransform)` | Disables physics, parents to NPC hand, deactivates GameObject |
| `PlaceAt(position)` | Re-activates, unparents, marks as `IsDelivered` |
| `Release()` | Re-enables physics (for dropping) |

**Editor setup**: Add a child empty named `GrabTarget` to define the NPC's grab point. Assign an `ItemCategory`.

### IInteractable Interface
```csharp
Vector3 GetInteractionPoint();     // Where NPC hand reaches
void OnPickedUp(Transform hand);   // NPC picks this up
```

### IPlaceable Interface
```csharp
bool CanPlaceItem(GameObject item);
bool TryPlaceItem(GameObject item);
string GetPlacementPrompt();
```

---

## System 6: Counter & Checkout

### CounterSlot.cs
Attach to counter surface GameObjects.

| Feature | Details |
|---|---|
| Placement positions | `itemPlacements[]` array defines local offsets for each item spot |
| NPC placing | `PlaceItem(prefab, category)` — instantiates item at next available position |
| Player bagging | `RemoveItem(item)` — deletes item from slot (triggered by ObjectPickup) |
| Highlight | Shows/hides a highlight mesh when player looks at a counter item |

**Static helper**: `CounterSlot.GetSlotContaining(item)` — finds which slot holds a specific item.

### CashRegister.cs
Attach to the register model.

On **E** press while looking at register:
1. `FindObjectsOfType<NPCInteractionController>()` finds all NPCs
2. Selects closest NPC within `npcDetectionRadius` that hasn't checked out
3. Calls `npc.TriggerCheckout()` → NPC navigates to exit and despawns

**Editor setup**: Set `npcDetectionRadius`, `npcLayerMask`, `interactionRange`.

---

## System 7: Delivery Station

### DeliveryStation.cs
Attach to a delivery area GameObject.

`SpawnBox()` instantiates the assigned `inventoryBoxPrefab` at `spawnPoint`. Called by `ObjectPickup` when the player presses E while looking at the station.

Shows/hides a `highlightObject` when the player looks at it.

---

## System 8: Computer Screen UI

### ComputerScreen.cs
Attach to the monitor GameObject.

**Activation flow** (E press → `Activate()`):
1. Validates `FocusStateManager` and `focusCameraTarget` exist
2. Assigns Event Camera to World Space Canvas (required for click detection!)
3. Checks for `EventSystem` in scene
4. Calls `FocusStateManager.Instance.EnterFocus()` → transitions camera
5. Enables `GraphicRaycaster` + shows `InteractiveUI` / hides `IdleScreen`
6. Calls `ComputerScreenController.ResetToMain()`

**Deactivation** (Escape → focus exit callback → `Deactivate()`):
1. Disables raycaster, hides interactive UI, shows idle screen
2. Calls `ComputerScreenController.HideAll()`

**Temporary exit for dialogue** (`TemporaryExitForDialogue(Action onComplete)`):
1. Suppresses `Deactivate()` — UI stays logically active
2. Disables raycaster, calls `FocusStateManager.ExitFocus()`
3. Fires `onComplete` callback after the exit transition finishes

**Reactivation after dialogue** (`ReactivateAfterDialogue()`):
1. Re-enters focus via `FocusStateManager.EnterFocus()` without resetting views
2. Re-enables raycaster when the enter transition completes (via `OnFocusChanged` event)

**Editor setup**:
1. Create a **World Space Canvas** (`ScreenCanvas`) positioned on the monitor
2. Create child GameObjects: `IdleScreen` (always-on display) and `InteractiveUI` (shown during focus)
3. Assign `screenCanvas`, `idleScreen`, `interactiveUI`, `focusCameraTarget` in Inspector
4. Ensure the scene has an **EventSystem** (GameObject → UI → Event System)

> **Critical**: World Space Canvas needs `worldCamera` set to detect clicks. `ComputerScreen` auto-assigns this at activation time because `Camera.main` may be null during `Awake()`.

### ComputerScreenController.cs
Attach to the `InteractiveUI` GameObject.

Manages **tab-based view switching**:

```csharp
[System.Serializable]
public class ScreenView
{
    public string viewName;       // Human-readable name
    public Button tabButton;      // Tab button for this view
    public GameObject viewRoot;   // Root GameObject containing view content
    public Color activeTabColor;  // Tab highlight when active
    public Color inactiveTabColor;// Tab color when inactive
}
```

**Inspector setup**:
1. Create child GameObjects under `InteractiveUI` for each view (e.g., `MainView`, `OrdersView`)
2. Create tab `Button` objects
3. Fill the `views[]` array: assign each view's tab button and root GameObject
4. Tab wiring is **automatic** — `WireTabButtons()` in Awake adds onClick listeners

**API**:
- `ResetToMain()` — shows view index 0
- `ShowView(int index)` / `ShowView(string name)` — switches to specific view
- `HideAll()` — deactivates all views

**Button actions**: Any button inside a view can call **any public method** via the Inspector's `onClick` UnityEvent. No custom action classes needed.

**Events**: `OnViewChanged(int index)` — fires when view switches.

---

## System 9: Pill Counting Mini-Game

### PillCountingStation.cs
Attach to the pill counting tray.

**Activation flow** (`Activate()`):
1. Calls `FocusStateManager.Instance.EnterFocus(focusCameraTarget)`
2. Enables child components: `PillScraper`, `PillCountingChute`, `PillCountUI`
3. Initializes chute with `targetPillCount`
4. Spawns pills via `PillSpawner.SpawnPills()`

**Completion**: When chute fires `OnTargetReached`, auto-exits after 1.5s delay.

**Deactivation**: Cleans up pills, resets chute/UI, disables mini-game components.

### PillSpawner.cs
Spawns pill Rigidbodies in a randomized cluster above the tray. Uses `pillPrefab` or generates primitive capsules as fallback. All pills go on `debrisLayerIndex` (default 9 = Physics_Debris).

### PillScraper.cs
Mouse-controlled kinematic Rigidbody that pushes pills across the tray.

**How it works**:
1. Raycasts screen corners onto the tray plane to establish world-space mapping
2. Converts mouse screen position → tray world position
3. Moves kinematic Rigidbody to push pills via physics collision
4. Supports hover/contact vertical states and procedural tilt

### PillCountingChute.cs
`BoxCollider` trigger zone. When a pill (Physics_Debris layer) enters:
1. Increments count
2. Disables/destroys the pill
3. Fires `OnPillCounted(current, target)`
4. At target count: fires `OnTargetReached`

### PillCountUI.cs
World-space UI showing `"Count: X / Y"`. Subscribes to chute events. Shows completion state with color change and optional confirm button.

---

## System 11: ID Card Verification

### NPCIdentity.cs (ScriptableObject)
Create via **Right-click → Create → NPC → NPC Identity**.

| Field | Purpose |
|---|---|
| `fullName` | Dialogue speaker name + computer screen display |
| `dateOfBirth` | Date of birth shown on computer |
| `address` | Address shown on computer |
| `idNumber` | Unique ID / barcode number |
| `photoSprite` | Headshot for computer screen UI |
| `idCardName` | *(Override)* Name printed on physical card — falls back to `fullName` if empty |
| `idCardPhotoSprite` | *(Override)* Photo on physical card — falls back to `photoSprite` if null |

**Convenience properties** (read-only, computed):
- `IdCardDisplayName` — returns `idCardName` or `fullName`
- `IdCardPhoto` — returns `idCardPhotoSprite` or `photoSprite`

### IDCardSlot.cs
Attach to a counter surface. Holds a single ID card.

**Key methods**: `PlaceIDCard(prefab, identity)` — spawns the card and initializes it. `RemoveIDCard()` — destroys the card.

**Editor setup**: Assign `focusCameraTarget` (empty Transform positioned where the camera looks at the card).

### IDCardInteraction.cs
Attach to the ID card prefab. Handles focus mode + barcode scanning.

**Activation flow** (E press → `Activate()`):
1. `FocusStateManager.Instance.EnterFocus(focusCameraTarget)` — zooms camera
2. Player clicks on barcode area → raycast detects `barcodeZone` collider
3. `NPCInfoDisplay.Instance.ShowNPCInfo(identity)` — shows NPC panel on computer main view
4. Auto-exits focus after `autoExitDelay` (default 1s), or player presses Escape

`Initialize(identity, focusCameraTarget)` also auto-finds and calls `IDCardVisuals.Initialize(identity)` to populate the physical card's photo and name.

**Editor setup**: Assign `barcodeZone` (child collider on barcode area). Optional: `audioSource` + `scanSound`, `scanEffectPrefab`.

### IDCardVisuals.cs
Attach to the ID card prefab alongside `IDCardInteraction`. Drives the physical card's appearance.

| Field | Purpose |
|---|---|
| `photoRenderer` | `SpriteRenderer` on a child object over the photo area |
| `nameText` | `TextMeshPro` (3D) on a child object over the name area |

Reads from `NPCIdentity.IdCardPhoto` and `NPCIdentity.IdCardDisplayName` (both fall back to the main identity fields if overrides are not set). Initialized automatically by `IDCardInteraction.Initialize()` — no manual call needed.

**Editor setup**: In the ID card prefab, create two child GameObjects — one with a `SpriteRenderer` for the photo, one with a `TextMeshPro` (3D, not UGUI) for the name. Assign both to `IDCardVisuals`. For the TMP: rotate `(90, 0, 0)` to lay flat, set font size in world units (e.g. `0.05`–`0.2`), never scale the Transform.

### NPCInfoDisplay.cs (Singleton)
Attach to the computer screen's `InteractiveUI`. `NPCInfoDisplay.Instance`

| Method | Purpose |
|---|---|
| `ShowNPCInfo(NPCIdentity)` | Shows `npcInfoPanel` + auto-populates all `NPCIdentityField` children |
| `ClearNPCInfo()` | Hides `npcInfoPanel` + clears all `NPCIdentityField` children |

**No individual TMP wiring needed.** Text fields are discovered automatically via `NPCIdentityField` components. Only assign `npcInfoPanel` and optionally `photoImage`.

**Editor setup**: Assign `npcInfoPanel` (a panel inside the main view, disabled by default). For each text element inside the panel, add an `NPCIdentityField` component and set its `FieldType`. Optionally assign `photoImage` (Image component) directly on `NPCInfoDisplay`.

### NPCIdentityField.cs
Add to any `TextMeshProUGUI` or `Button` (with a TMP child) inside an `NPCInfoPanel`.

| Field | Purpose |
|---|---|
| `fieldType` | Enum: `FullName`, `DateOfBirth`, `Address`, `IDNumber` |

`NPCInfoDisplay` calls `GetComponentsInChildren<NPCIdentityField>()` to find and populate all instances automatically. Works on direct TMP objects or Button GameObjects (searches children for TMP).

### NPC Integration
`NPCInteractionController` has serialized fields: `npcIdentity`, `idCardPrefab`, `idCardSlot`. The ID card is placed simultaneously with items during `HandlePlacingState`. On NPC destroy (`OnDestroy`), `CleanupIDCard()` removes the card and clears the computer display.

---

## System 12: NPC Spawn Queue

### RoundConfig.cs (ScriptableObject)
Create via **Right-click → Create → NPC → Round Config**.

Defines the NPC queue for a single round/session.

| Field | Purpose |
|---|---|
| `npcPool` | `List<GameObject>` — Pool of NPC prefabs available for random selection |
| `queueEntries` | `List<QueueEntry>` — Ordered list of queue positions |

**`QueueEntry` (Serializable class)**:

| Field | Purpose |
|---|---|
| `isFixed` | If true, always spawn `fixedNpcPrefab` at this queue position |
| `fixedNpcPrefab` | The specific NPC prefab (only used when `isFixed` is true) |

When `isFixed` is false, the spawner picks a random NPC from `npcPool`. Fixed NPCs are automatically removed from the pool so they can't be duplicated by random picks. Once any NPC is used (fixed or random), it is removed from the available pool for the rest of the round.

### NPCSpawnManager.cs
Attach to a persistent scene GameObject.

Manages sequential NPC spawning from a `RoundConfig`. Spawns one NPC at a time, waits for it to exit and despawn, then spawns the next after a configurable delay.

| Field | Purpose |
|---|---|
| `spawnPoint` | Transform where NPCs appear |
| `initialDelay` | Seconds before first NPC spawns (default 2) |
| `delayAfterExit` | Seconds between NPC despawn and next spawn (default 3) |
| `counterSlots` | Shared `List<CounterSlot>` assigned to every NPC |
| `exitPoint` | Shared exit Transform assigned to every NPC |
| `idCardSlot` | Shared `IDCardSlot` assigned to every NPC |
| `allowedShelfSlots` | Shared `List<ShelfSlot>` assigned to every NPC |

**API**:
- `StartNPCSpawning(RoundConfig config)` — Resolves the queue (fixed + random picks), starts spawning coroutine
- `StopNPCSpawning()` — Stops spawning, clears remaining queue

**Events**:
- `OnNPCSpawned(NPCInteractionController)` — Fired after each NPC is instantiated
- `OnAllNPCsFinished` — Fired when all NPCs have exited

**Internal flow**:
1. Copies `config.npcPool` into a working list, resolves all `QueueEntry` items into concrete prefabs (removing each from the pool as used)
2. Coroutine: waits `initialDelay` → spawns NPC → calls `AssignSceneReferences()` to inject shared scene refs → waits for `OnNPCExited` event → waits `delayAfterExit` → repeats until queue empty

**Scene reference injection**: NPC prefabs cannot hold references to scene objects. After instantiation, `NPCSpawnManager` calls `NPCInteractionController.AssignSceneReferences()` to inject `counterSlots`, `exitPoint`, `idCardSlot`, and `allowedShelfSlots`. Per-NPC data (identity, wanted categories, dialogue files, id card prefab, hand bone) stays on the prefab.

### NPCInteractionController.cs — Spawn-Related Additions

**Static event** (used by `NPCSpawnManager`):
```csharp
public static event Action<NPCInteractionController> OnNPCExited;
```
Fired just before `Destroy(gameObject)` at all exit paths (normal exit, immediate despawn).

**Scene reference setter**:
```csharp
public void AssignSceneReferences(List<CounterSlot> counters, Transform exit, IDCardSlot cardSlot, List<ShelfSlot> shelfSlots)
```
Called by `NPCSpawnManager` after instantiation. Also auto-enables `useShelfSlots` if shelf slots are provided.

### GameStarter.cs
Attach to any persistent GameObject. Routes to `ShiftManager.StartDayShift()` when a ShiftManager is assigned. Falls back to calling `NPCSpawnManager.StartNPCSpawning(roundConfig)` directly if no ShiftManager is present.

| Field | Purpose |
|---|---|
| `shiftManager` | **(Preferred)** Reference to the `ShiftManager` — drives the full day/night cycle |
| `spawnManager` | **(Legacy fallback)** Direct reference to `NPCSpawnManager`, only used when `shiftManager` is null |
| `roundConfig` | **(Legacy fallback)** The `RoundConfig` asset, only used when `shiftManager` is null |

---

## System 13: Shift Manager

**Script**: `ShiftManager.cs` — `NetworkBehaviour` on a persistent scene GameObject.

Server-authoritative state machine that drives the day/night cycle. All state is in `NetworkVariable`s — clients read them for UI, lighting, and audio.

### Phase Flow

```
Dawn → DayShift → (all NPCs finished)
  ├── 0 escaped doppelgangers → Dawn (clean end, skip night)
  └── 1+ escaped doppelgangers → Transition → NightShift → Dawn → DayShift → ...
```

### Phases

| Phase | What Happens |
|---|---|
| `Dawn` | Cleanup, score screen, prep. After `dawnDuration` seconds → `DayShift` |
| `DayShift` | NPCs arrive via `NPCSpawnManager`. Player verifies prescriptions. Ends when `OnAllNPCsFinished` fires |
| `Transition` | Atmospheric beat (lights flicker, audio sting). Lasts `transitionDuration` seconds → `NightShift` |
| `NightShift` | Monster(s) spawn based on `EscapedDoppelgangers`. Ends when all monsters killed or `nightDuration` expires → `Dawn` |

### NetworkVariables

| Variable | Type | Purpose |
|---|---|---|
| `CurrentPhase` | `NetworkVariable<int>` | Current `ShiftPhase` enum value — clients read for UI/lighting/audio |
| `EscapedDoppelgangers` | `NetworkVariable<int>` | Doppelgangers that escaped this day shift. Reset each day |
| `CurrentNight` | `NetworkVariable<int>` | Night number (1-based). Increments each dawn |

### Events (server-side)

| Event | When |
|---|---|
| `OnPhaseChanged(ShiftPhase)` | Every phase transition |
| `OnDayShiftStarted` | Day shift begins |
| `OnNightShiftStarted` | Night shift begins |
| `OnShiftCycleCompleted` | Dawn reached (full cycle done) |

### Public API (server-only)

| Method | Purpose |
|---|---|
| `StartDayShift()` | Begin a day shift. Called by `GameStarter` |
| `ReportEscape()` | Increment escaped doppelgangers. Called by `CashRegister` when a doppelganger is approved |
| `OnDawnReached()` | End night shift → dawn. Called by dawn timer or when all monsters die |
| `OnMonsterKilled()` | Report a monster kill. Ends night early if all dead |
| `ForcePhase(ShiftPhase)` | **Debug only.** Force-jump to any phase |

### Debug Tools

Set `enableDebugTools = true` in the Inspector (or call `SetDebugTools(true)` at runtime) to show an OnGUI overlay (top-right corner, server/host only) with:

- **Status display**: Current phase, night number, escaped doppelgangers, NPC spawning state
- **Force Phase buttons**: Instantly jump to Dawn / DayShift / Transition / NightShift
- **Escaped Doppelganger controls**: +1 / -1 / Reset
- **Night counter controls**: +1 / -1 / Reset to 1
- **Skip Current Timer**: Advances to the next phase immediately (skips dawn wait, forces NPCs finished, skips transition, or ends night)

Debug tools are compiled out of release builds (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`).

### Inspector Fields

| Field | Purpose |
|---|---|
| `spawnManager` | Reference to the `NPCSpawnManager` in the scene |
| `defaultRoundConfig` | `RoundConfig` asset for day shifts (future: dynamic generation per night) |
| `dawnDuration` | Seconds in Dawn before next day starts (default 5) |
| `transitionDuration` | Seconds of transition effects before night (default 4) |
| `nightDuration` | Night length in seconds (default 120, 0 = infinite / monster-kill only) |
| `enableDebugTools` | Toggle the runtime debug overlay |

### Editor Setup

- [ ] Create a persistent scene GameObject with `ShiftManager` + `NetworkObject`
- [ ] Assign `spawnManager` (existing `NPCSpawnManager` in scene)
- [ ] Assign `defaultRoundConfig` (existing `RoundConfig` asset)
- [ ] On `GameStarter`: assign the new `ShiftManager` reference (clear old `spawnManager`/`roundConfig` fields)
- [ ] Optional: check `enableDebugTools` for testing

### NPC Prefab Setup (for spawning)
NPC prefabs should have these fields filled in (they survive prefabbing as asset/internal refs):
- `npcIdentity` (ScriptableObject)
- `idCardPrefab` (prefab reference)
- `wantedCategories` (ScriptableObjects)
- `dialogueFiles` (TextAssets — on `NPCDialogueTrigger`)
- `handBone` (child Transform — internal prefab ref)
- All behavior settings (detection radius, batch size, etc.)

These fields should be **left empty** on the prefab (injected by spawner at runtime):
- `counterSlots`, `exitPoint`, `idCardSlot`, `allowedShelfSlots`

---

## System 14: Doppelganger System

The core gameplay mechanic. Some NPCs are doppelgangers with discrepancies in their profile. The player investigates via the computer screen, then physically acts: cash register = approve, gun = reject. No approve/reject buttons exist on the computer — it is information-only.

### PrescriptionData.cs (ScriptableObject)
Create via **Right-click → Create → NPC → Prescription Data**.

| Field | Purpose |
|---|---|
| `medicationName` | Name of the prescribed medication |
| `quantity` | Number of units prescribed |
| `dosage` | Dosage instructions (e.g. "0.5mg twice daily") |
| `prescriberName` | Prescribing doctor's full name |
| `prescriberNPI` | National Provider Identifier number |
| `prescriberSpecialty` | Medical specialty (e.g. "Cardiology") |
| `prescriberAddress` | Prescriber's office address |
| `previousFills` | Array of fill history strings (empty for new prescriptions) |

### DoppelgangerProfile.cs (ScriptableObject)
Create via **Right-click → Create → NPC → Doppelganger Profile**.

Defines what's fake about a doppelganger. Each fake field overrides the corresponding real value from `NPCIdentity` or `PrescriptionData` when presented to the player. Null/empty fields fall back to the real data.

| Field | Purpose |
|---|---|
| `discrepancies` | `DiscrepancyType[]` — which fields are wrong (for scoring/hints) |
| `fakePhoto` | Mismatched photo (Sprite) |
| `fakeDOB` | Wrong date of birth |
| `fakeAddress` | Wrong address |
| `fakePrescriberNPI` | Invalid or wrong NPI |
| `fakePrescriberSpecialty` | Wrong prescriber specialty |
| `fakeDosage` | Suspicious dosage |
| `fakeQuantity` | Non-standard quantity (0 = use real) |

**`DiscrepancyType` enum**: `PhotoMismatch`, `InvalidNPI`, `NoFillHistory`, `WrongPrescriberSpecialty`, `DoseJump`, `NonStandardQuantity`, `PrescriberOutsideArea`, `WrongDOB`, `WrongAddress`

**Convenience methods**: `GetDOB(real)`, `GetAddress(real)`, `GetPhoto(real)`, `GetPrescriberNPI(real)`, `GetPrescriberSpecialty(real)`, `GetDosage(real)`, `GetQuantity(real)` — each returns the fake value if overridden, otherwise the real value. `HasOverride(DiscrepancyType)` checks if a specific discrepancy is present.

### PrescriberDatabase.cs (ScriptableObject)
Create via **Right-click → Create → NPC → Prescriber Database**.

Contains a list of `PrescriberEntry` records (name, NPI, specialty, address). Used by the computer screen's NPI lookup feature. Builds an internal dictionary on first lookup for O(1) access.

| Method | Purpose |
|---|---|
| `LookupByNPI(string npi)` | Returns the `PrescriberEntry` or null if NPI not found |
| `IsValidNPI(string npi)` | Returns true if the NPI exists in the database |

### NPCInteractionController — Doppelganger Fields

| Field / Property | Purpose |
|---|---|
| `prescriptionData` (serialized) | Prescription data for this NPC. Set on prefab |
| `doppelgangerProfile` (serialized) | Doppelganger profile. Null = real patient. Assigned at runtime by `NPCSpawnManager` |
| `IsDoppelganger` (property) | `true` if `doppelgangerProfile != null` |
| `DoppelgangerData` (property) | The doppelganger profile (server-only ground truth) |
| `Prescription` (property) | The prescription data |
| `AssignDoppelgangerProfile(profile)` | Called by `NPCSpawnManager` at spawn time (server-only) |

### RoundConfig — Doppelganger Extensions

**QueueEntry additions**:

| Field | Purpose |
|---|---|
| `forceDoppelganger` | If true, this queue position is always a doppelganger |
| `fixedProfile` | Specific `DoppelgangerProfile` for authored doppelgangers |

**RoundConfig additions**:

| Field | Purpose |
|---|---|
| `doppelgangerPool` | `List<DoppelgangerProfile>` — profiles available for random assignment |
| `randomDoppelgangerCount` | How many random doppelgangers to assign (in addition to forced ones) |

### NPCSpawnManager — Doppelganger Resolution

The spawn queue now stores `SpawnEntry` structs (prefab + optional `DoppelgangerProfile`) instead of raw `GameObject` prefabs.

**Resolution flow** (server-only, in `ResolveQueue`):
1. Resolve NPC prefabs as before (fixed or random from pool)
2. Apply forced doppelganger profiles from `QueueEntry.fixedProfile`
3. `AssignRandomDoppelgangers()` — picks N random entries that don't already have profiles, assigns random profiles from `doppelgangerPool` (without replacement)
4. On spawn: calls `AssignDoppelgangerProfile(profile)` on the NPC (null for real patients)

### Prescription Verification UI

The computer screen displays prescription and prescriber data alongside identity info. All populated automatically via component discovery (same pattern as `NPCIdentityField`).

#### PrescriptionField.cs
Same pattern as `NPCIdentityField`. Add to any `TextMeshProUGUI` inside a prescription panel.

| FieldType | Source |
|---|---|
| `MedicationName` | `PrescriptionData.medicationName` |
| `Quantity` | `PrescriptionData.quantity` (doppelganger override via `fakeQuantity`) |
| `Dosage` | `PrescriptionData.dosage` (doppelganger override via `fakeDosage`) |
| `PrescriberName` | `PrescriptionData.prescriberName` |
| `PrescriberNPI` | `PrescriptionData.prescriberNPI` (doppelganger override via `fakePrescriberNPI`) |
| `PrescriberSpecialty` | `PrescriptionData.prescriberSpecialty` (doppelganger override via `fakePrescriberSpecialty`) |
| `PrescriberAddress` | `PrescriptionData.prescriberAddress` |
| `FillHistory` | `PrescriptionData.previousFills` (doppelganger `NoFillHistory` overrides to "No previous fills") |

Has two `Populate()` overloads: one for real data, one that accepts a `DoppelgangerProfile` to apply fake overrides.

#### PrescriptionDisplay.cs (Singleton)
Attach to `InteractiveUI` alongside `NPCInfoDisplay`. Manages the prescription panel.

| Method | Purpose |
|---|---|
| `ShowPrescription(NPCInteractionController)` | Populates prescription fields with doppelganger overrides applied |
| `ClearPrescription()` | Clears and hides the prescription panel |

Called automatically by `NPCInfoDisplay.ShowNPCInfo()` — no manual bridging needed.

#### NPISearchPanel.cs
Attach to a panel inside a "Prescriber Database" computer screen view.

| Field | Purpose |
|---|---|
| `database` | `PrescriberDatabase` ScriptableObject reference |
| `npiInputField` | `TMP_InputField` for the NPI query |
| `searchButton` | Button to trigger search (Enter key also works) |
| `resultsPanel` | Panel showing result fields (name, specialty, address, NPI) |
| `statusText` | "Not found" / validation messages |

`PerformSearch()` calls `database.LookupByNPI()`. Shows prescriber details if found, "not found" if invalid.

#### NPCInfoDisplay — Doppelganger Awareness

`ShowNPCInfo(identity)` now:
1. Finds the `NPCInteractionController` matching the identity via `FindNPCByIdentity()`
2. Passes the `DoppelgangerProfile` to `NPCIdentityField.Populate(identity, profile)` — fake DOB/address shown
3. Uses `profile.GetPhoto()` for the photo image — fake photo shown if overridden
4. Bridges to `PrescriptionDisplay.ShowPrescription(npc)` automatically

`ClearNPCInfo()` also calls `PrescriptionDisplay.ClearPrescription()`.

New property: `CurrentNPC` — the NPC controller for the currently displayed NPC.

#### NPCIdentityField — Doppelganger Support

New overload: `Populate(NPCIdentity identity, DoppelgangerProfile profile)`. Applies fake DOB and fake address from the profile. FullName and IDNumber are never overridden (doppelgangers use the same name/ID as the real person).

### Decision Flow & Outcome Hooks

- **Approve** = cash register checkout (`CashRegister`). If doppelganger → escapes silently → `ShiftManager.ReportEscape()`
- **Reject** = shoot with gun (`GunCase`). If doppelganger → caught. If real → money penalty
- **Computer** = information-only. No action buttons. Player reads data and physically acts

**CashRegister** — new `shiftManager` serialized field. In `ProcessCheckoutServerRpc()`, checks `npc.IsDoppelganger` before triggering checkout. If true, calls `shiftManager.ReportEscape()`.

**GunCase** — new `_shiftManager` serialized field. In `ShootNPCServerRpc()`, calls `ReportShootOutcome(npc)` before `npc.Kill()`. Logs whether the kill was correct (doppelganger) or wrong (innocent).

### Doppelganger Editor Setup Checklist

- [ ] Create `PrescriptionData` assets (Right-click → Create → NPC → Prescription Data) for each NPC
- [ ] Create `DoppelgangerProfile` assets (Right-click → Create → NPC → Doppelganger Profile) — set discrepancies + fake fields
- [ ] Create one `PrescriberDatabase` asset (Right-click → Create → NPC → Prescriber Database) — add all valid prescriber entries
- [ ] On each NPC prefab: assign `prescriptionData` in `NPCInteractionController`. Leave `doppelgangerProfile` empty (injected at runtime)
- [ ] On `RoundConfig` asset: fill `doppelgangerPool` with profiles, set `randomDoppelgangerCount`. Optionally set `forceDoppelganger` + `fixedProfile` on specific queue entries
- [ ] Add `PrescriptionDisplay` component to `InteractiveUI` → assign `prescriptionPanel`
- [ ] Inside `prescriptionPanel`: add TMP text elements with `PrescriptionField` components, set `FieldType` on each
- [ ] Add a "Prescriber Database" view in `ComputerScreenController` with `NPISearchPanel` → assign `database`, `npiInputField`, `searchButton`, result texts
- [ ] On `CashRegister`: assign `shiftManager` reference
- [ ] On `GunCase`: assign `_shiftManager` reference

---

## Cross-System Dependencies

```
ObjectPickup ──→ ComputerScreen ──→ FocusStateManager
     │                                      ↑
     ├──→ PillCountingStation ──────────────┘
     │                                      ↑
     ├──→ IDCardInteraction ────────────────┘
     │         │
     │         └──→ NPCInfoDisplay (shows panel in main view, no view switch)
     │
     ├──→ DeliveryStation ──→ InventoryBox
     │
     ├──→ ItemPlacementManager ──→ ShelfSection ──→ ShelfSlot
     │         │                                      ↑
     │         └──→ BoxItemPreview                    │
     │         └──→ InventoryBox                      │
     │                                                │
     ├──→ CounterSlot ←── NPCInteractionController ──┘
     │                           │         │
     └──→ CashRegister ─────────┘         │
                                 │         ├──→ NPCDialogueTrigger ──→ DialogueManager
              NPCAnimationController       │                              ↑
                                           └──→ NPCInfoTalkButton (on NPCInfoPanel)
                                                         │
                                                         └──→ ComputerScreen.TemporaryExitForDialogue()
                                                                   └──→ DialogueManager ──→ ComputerScreen.ReactivateAfterDialogue()

NPCInteractionController ──→ IDCardSlot ──→ IDCardInteraction ──→ IDCardVisuals
                                               └──→ NPCInfoDisplay (panel toggle on scan)

GameStarter ──→ ShiftManager ──→ NPCSpawnManager ──→ NPCInteractionController (instantiate + inject scene refs)
                     │                │                        │
                     │                └── RoundConfig          └──→ OnNPCExited (static event → triggers next spawn)
                     │
                     ├── OnAllNPCsFinished ──→ ShiftManager.OnAllNPCsFinished()
                     │                              ├── 0 escaped → Dawn → next DayShift
                     │                              └── 1+ escaped → Transition → NightShift → Dawn
                     │
                     └── CashRegister / GunCase ──→ ShiftManager.ReportEscape() (doppelganger approved)

DialogueManager ──→ DialogueHistory (records exchanges)
```

### Key interaction chains:

1. **Shelf Restocking**: `DeliveryStation.SpawnBox()` → player picks up `InventoryBox` → `ItemPlacementManager` detects nearby shelves → builds queue from `ShelfSection.GetMissingItems()` → player places items → `ShelfSlot.PlaceItem()` + `InventoryBox.Decrement()`

2. **NPC Shopping**: `NPCSpawnManager` instantiates NPC prefab at `spawnPoint` → `AssignSceneReferences()` injects shared scene refs → `NPCInteractionController` scans `ShelfSlot[]` → navigates → picks up `InteractableItem` → navigates to counter → `CounterSlot.PlaceItem()` → waits → `CashRegister.TriggerCheckout()` → navigates to exit → fires `OnNPCExited` → destroys self → `NPCSpawnManager` spawns next NPC after delay

3. **Pill Counting**: `ObjectPickup` detects `PillCountingStation` → `Activate()` → `FocusStateManager.EnterFocus()` → `PillSpawner.SpawnPills()` → player uses `PillScraper` → pills enter `PillCountingChute` → count reaches target → auto-exit

4. **Computer Screen**: `ObjectPickup` detects `ComputerScreen` → `Activate()` → `FocusStateManager.EnterFocus()` → `ComputerScreenController.ResetToMain()` → player clicks tabs/buttons on World Space Canvas → Escape exits

5. **NPC Dialogue**: NPC enters `WaitingForCheckout` → `NPCDialogueTrigger` detects player nearby → `DialogueManager.StartDialogue()` → player clicks response buttons → dialogue navigates nodes → terminal node → auto-closes. Player can use `NPCInfoTalkButton` on the NPCInfoPanel to start new conversations (exits computer focus → dialogue → re-enters computer focus) → `CashRegister` checkout remains separate

6. **ID Card Verification**: NPC places items on counter → `NPCInteractionController.PlaceIDCard()` spawns ID card on `IDCardSlot` → `IDCardInteraction.Initialize()` populates `IDCardVisuals` (photo + printed name on physical card) → player looks at ID card + presses E → `IDCardInteraction.Activate()` → `FocusStateManager.EnterFocus()` → player clicks barcode zone → `NPCInfoDisplay.ShowNPCInfo()` → `NPCInfoPanel` appears on computer main view populated via `NPCIdentityField` components → NPC exits → `CleanupIDCard()` removes card + `NPCInfoDisplay.ClearNPCInfo()` hides panel

---

## Common Editor Setup Checklist

### Player Setup
- [ ] Player GameObject with `CharacterController`
- [ ] `PlayerComponents` on Player root (auto-finds all player components)
- [ ] `PlayerMovement` on Player root
- [ ] Camera as child of Player with `MouseLook` (assign `playerBody` = Player root)
- [ ] `ObjectPickup` on Camera (assign `playerCamera`, `holdPoint`)
- [ ] `ItemPlacementManager` on Camera or Player (assign `ghostMaterial`)
- [ ] `FocusStateManager` on Player (auto-finds references from PlayerComponents)

### Shelf Setup
- [ ] Shelf parent with `ShelfSection` (auto-finds child slots)
- [ ] Each shelf layer: empty child with `ShelfSlot`
- [ ] Each `ShelfSlot`: assign `acceptedCategory` (ItemCategory asset) and configure `itemPlacements[]` positions
- [ ] Ensure shelf has a Collider somewhere in hierarchy (for `IPlaceable` detection)

### NPC Prefab Setup
- [ ] NPC with `NavMeshAgent` + `NPCInteractionController` + `NPCAnimationController`
- [ ] Assign `handTransform` (child bone) — internal prefab ref, survives prefabbing
- [ ] Assign `npcIdentity` (ScriptableObject), `idCardPrefab` (prefab), `wantedCategories` (ScriptableObjects)
- [ ] **Leave empty**: `counterSlots`, `exitPoint`, `idCardSlot`, `allowedShelfSlots` — injected by `NPCSpawnManager` at runtime
- [ ] Animator Controller with params: `IsWalking` (bool), `Speed` (float), `PickUp` (trigger), `Place` (trigger)
- [ ] Item prefabs need `InteractableItem` component + `Collider` + child named `GrabTarget`
- [ ] Optional: add `NPCDialogueTrigger` with `dialogueFiles[]` (TextAsset array) + `infoDialogues[]` (key/file pairs)

### NPC Spawn System Setup
- [ ] Create `RoundConfig` asset (Right-click → Create → NPC → Round Config)
  - Fill `npcPool` with NPC prefabs eligible for random selection
  - Fill `queueEntries` — set `isFixed` + `fixedNpcPrefab` for guaranteed NPCs, leave unchecked for random picks
- [ ] Create a scene GameObject with `NPCSpawnManager`
  - Assign `spawnPoint` (entrance Transform), `exitPoint`, `counterSlots`, `idCardSlot`, `allowedShelfSlots`
- [ ] Add `GameStarter` component (same or different GameObject) — assign `spawnManager` and `roundConfig`

### Counter Checkout Setup
- [ ] Counter surfaces with `CounterSlot` (configure `itemPlacements[]`)
- [ ] Cash register model with `CashRegister` (set `npcLayerMask`, `npcDetectionRadius`)

### Computer Screen Setup
- [ ] World Space Canvas (`ScreenCanvas`) positioned on monitor mesh
- [ ] `ComputerScreen` on monitor parent — assign `screenCanvas`, `idleScreen`, `interactiveUI`, `focusCameraTarget`
- [ ] `ComputerScreenController` on `InteractiveUI` — fill `views[]` array with tab buttons + view roots
- [ ] Scene must have an **EventSystem** (GameObject → UI → Event System)
- [ ] `focusCameraTarget`: empty Transform in front of monitor at the desired camera position

### Pill Counting Setup
- [ ] Tray model with `PillCountingStation` — assign `focusCameraTarget`, or let it auto-find children
- [ ] Child with `PillSpawner` — assign `pillPrefab` or leave null for primitives
- [ ] Chute trigger zone with `PillCountingChute` + `BoxCollider` (isTrigger)
- [ ] World-space UI canvas with `PillCountUI` + TextMeshPro
- [ ] Scraper model with `PillScraper` + kinematic `Rigidbody`
- [ ] Project Settings → Physics: ensure `Physics_Debris` layer exists at index 9

### Delivery Station Setup
- [ ] GameObject with `DeliveryStation` — assign `inventoryBoxPrefab` and `spawnPoint`
- [ ] InventoryBox prefab: `Rigidbody` + `InventoryBox` + `BoxItemPreview` (optional)

### ID Card Verification Setup
- [ ] Create `NPCIdentity` ScriptableObject assets (Right-click → Create → NPC → NPC Identity)
  - Fill `fullName`, `dateOfBirth`, `address`, `idNumber`, `photoSprite` (computer screen)
  - Optionally fill `idCardName` / `idCardPhotoSprite` to override what's printed on the physical card
- [ ] Create an ID card prefab: 3D quad/plane with `IDCardInteraction` + `IDCardVisuals` components
  - Add a child collider on the barcode area → assign to `IDCardInteraction.barcodeZone`
  - Add a child with `SpriteRenderer` for the photo → assign to `IDCardVisuals.photoRenderer`
  - Add a child with `TextMeshPro` (3D) for the name → assign to `IDCardVisuals.nameText`
    - Rotate `(90, 0, 0)` to lie flat, size via Font Size (not Transform scale)
- [ ] Place an `IDCardSlot` on the counter with a `focusCameraTarget` empty Transform
- [ ] On each NPC prefab: assign `npcIdentity` (ScriptableObject), `idCardPrefab` (prefab ref). Leave `idCardSlot` empty — injected by `NPCSpawnManager`
- [ ] On the main view inside `InteractiveUI`: add a child panel (`NPCInfoPanel`) — **leave it disabled**
  - For each text element inside the panel: **Add Component → NPCIdentityField**, set `FieldType`
  - Optionally add an `Image` for the photo
- [ ] Add `NPCInfoDisplay` component to the `InteractiveUI` → assign `npcInfoPanel` + optional `photoImage`
  - No individual TMP refs needed — `NPCIdentityField` components are found automatically

---

## Layer & Tag Requirements

| Layer | Index | Used By |
|---|---|---|
| Default | 0 | Most objects |
| Physics_Debris | 9 | Pills (PillSpawner, PillCountingChute) |

| Tag | Used By |
|---|---|
| MainCamera | Main camera (for `Camera.main`) |

---

## System 10: NPC Dialogue

### DialogueData.cs + DialogueLoader
Data classes matching the JSON dialogue format. `DialogueLoader.Load(TextAsset)` parses JSON and builds a `Dictionary<string, DialogueNode>` for O(1) lookups.

**JSON format**: See `Assets/Data/Dialogues/npc_customer_01.json` for the structure. Flat array of nodes, each with `id`, `text`, `speakerName` (optional override), and `responses[]`. Empty responses = terminal node.

### DialogueManager.cs (Singleton)
Attach to a persistent GameObject with a **Screen Space Overlay Canvas**.

| Feature | Details |
|---|---|
| Start dialogue | `StartDialogue(TextAsset)` or `StartDialogue(DialogueData, Dictionary, Transform, string speakerNameOverride)` |
| Speaker name | Priority: node-level JSON name → `speakerNameOverride` → JSON root `speakerName` |
| Response buttons | Spawned dynamically from a prefab; auto-cleared between nodes |
| Terminal nodes | Shows "[Continue]" button → closes overlay |
| Cursor | Unlocks during dialogue, relocks on close (unless suppressed) |
| Suppress reset | `SetSuppressEndReset(true)` — skips cursor relock + control re-enable on `EndDialogue()`. Used by `NPCInfoTalkButton` when focus will immediately re-enter |
| Events | `OnDialogueStarted`, `OnDialogueEnded` |

**Editor setup**: Assign `dialoguePanel`, `speakerNameText` (TMP), `dialogueBodyText` (TMP), `responseContainer` (Transform), `responseButtonPrefab` (Button + TMP child).

### DialogueHistory.cs
Attach alongside `DialogueManager`. Records all exchanges with color-coded speaker labels.

| Feature | Details |
|---|---|
| Toggle | ENTER key shows/hides scrollable history panel |
| Formatting | Speaker names in gold, player responses in light blue (configurable) |
| Limit | Max 200 lines (configurable) |

**Editor setup**: Assign `historyPanel`, `historyText` (TMP), `historyScrollRect`.

### NPCDialogueTrigger.cs
Attach to NPC alongside `NPCInteractionController`.

| Feature | Details |
|---|---|
| Auto-trigger | First dialogue starts when NPC enters `WaitingForCheckout` + player within `playerRange` + line of sight |
| Repeat conversations | `StartNewConversation()` — cycles through `dialogueFiles[]` |
| Info dialogues | `StartInfoDialogue(string key)` — looks up key in `infoDialogues[]` list (`InfoDialogueEntry[]`). Falls back to `StartNewConversation()` if key not found |
| Key check | `HasInfoDialogue(string key)` — returns true if a dialogue exists for the given key |
| State check | `IsAvailableForDialogue()` — used by `NPCInfoTalkButton` |
| Speaker name | Automatically passes `NPCInteractionController.NpcIdentity.fullName` as `speakerNameOverride` to `DialogueManager` |

**Editor setup**: Assign `dialogueFiles[]` (TextAsset array), fill `infoDialogues[]` with key/file pairs (e.g. `"default"` → info.json, `"dob"` → dob.json, `"address"` → address.json), set `playerRange`, `lineOfSightMask`. The NPC's real name is sourced from the `NPCIdentity` asset on the controller — no manual speaker name needed in the JSON.

### NPCInfoTalkButton.cs
Attach to a Button **inside the `npcInfoPanel`** (child of `InteractiveUI` on the computer screen).

Orchestrates a three-phase focus chain when clicked:
1. `ComputerScreen.TemporaryExitForDialogue()` — exits computer focus without deactivating UI
2. `NPCDialogueTrigger.StartInfoDialogue(dialogueKey)` — starts keyed dialogue (camera lerps to NPC)
3. On `DialogueManager.OnDialogueEnded` → `ComputerScreen.ReactivateAfterDialogue()` — re-enters computer focus

Each button instance has a `dialogueKey` (e.g. `"default"`, `"dob"`, `"address"`) that maps to an `InfoDialogueEntry` on the NPC's `NPCDialogueTrigger`. This allows multiple buttons in the panel — including on identity field buttons — to trigger different dialogue trees.

Scans for NPCs matching `NPCInfoDisplay.Instance.CurrentIdentity` and checks `HasInfoDialogue(key)` to ensure the NPC supports this button's dialogue. Button is interactable only when both conditions are met.

**Editor setup**: Add to any Button inside `npcInfoPanel` (works alongside `NPCIdentityField` on the same button). Set `dialogueKey` to match an entry in the NPC's `infoDialogues[]`. Optionally assign a `CanvasGroup` for fade effect. Finds `ComputerScreen` automatically via hierarchy or scene search.

### Dialogue Editor Setup Checklist
- [ ] Create a persistent GameObject with `DialogueManager` + `DialogueHistory`
- [ ] Add a Screen Space Overlay Canvas under it with dialogue panel, speaker text, body text, response container
- [ ] Create a response button prefab (Button + TextMeshProUGUI child)
- [ ] Add a history panel with ScrollRect + TextMeshProUGUI
- [ ] On each NPC: add `NPCDialogueTrigger`, assign dialogue JSON files
- [ ] Inside `npcInfoPanel`: add a Button with `NPCInfoTalkButton` component

---

## Key Access Patterns

### PlayerComponents (per-player hub)
| Component | Access | Purpose |
|---|---|---|
| `PlayerComponents.Local` | Static | The local player's component hub |
| `.FocusState` | Via hub | Camera transitions, FPS control toggling |
| `.Look` | Via hub | Screen shake, sensitivity adjustment |
| `.Movement` | Via hub | Player movement control |
| `.Pickup` | Via hub | Object pickup and interaction |
| `.PlacementManager` | Via hub | Box-to-shelf item placement |
| `.PlayerCamera` | Via hub | The player's camera |

### Remaining Singletons (scene-global, not per-player)
| Singleton | Access | Purpose |
|---|---|---|
| `DialogueManager.Instance` | Static | Dialogue UI overlay, response handling |
| `NPCInfoDisplay.Instance` | Static | Populates computer UI with scanned NPC data |

---

## ScriptableObjects

| Type | Create Menu | Purpose |
|---|---|---|
| `ItemCategory` | Create → NPC → Item Category | Defines item type with prefab and rotation offset |
| `NPCIdentity` | Create → NPC → NPC Identity | Defines NPC personal data (name, DOB, address, ID#, photo) + optional ID card overrides (card name, card photo) |
| `PrescriptionData` | Create → NPC → Prescription Data | Medication name, quantity, dosage, prescriber info, fill history |
| `DoppelgangerProfile` | Create → NPC → Doppelganger Profile | Discrepancy types + fake field overrides for doppelganger NPCs |
| `PrescriberDatabase` | Create → NPC → Prescriber Database | List of valid prescriber entries for NPI lookup on computer screen |
| `RoundConfig` | Create → NPC → Round Config | NPC queue per round: pool + queue entries (fixed/random) + doppelganger pool + random doppelganger count |

---

## Multiplayer Status (Netcode for GameObjects)

**Framework**: Unity Netcode for GameObjects (NGO) 2.9.2 + Unity Transport 2.6.0
**Topology**: Host-authoritative. Host runs server + client. Clients send requests via ServerRpc; host validates and replicates via NetworkVariable / ClientRpc.
**Full plan**: See `MULTIPLAYER_PLAN.md`

### New Scripts (`Assets/Scripts/Networking/`)

| Script | Purpose |
|---|---|
| `ClientNetworkTransform.cs` | Owner-authoritative NetworkTransform. Overrides `OnIsServerAuthoritative()→false`. Add to Player prefab root. |
| `PlayerSetup.cs` | `NetworkBehaviour` on Player root. `OnNetworkSpawn()`: enables all input/camera components for the owner, disables them for non-owners. Sets `PlayerComponents.Local`. |
| `QuickConnect.cs` | Temporary `OnGUI` buttons (Start Host / Start Client). Attach to any scene object. Remove when lobby is implemented. |
| `DisconnectHandler.cs` | Server-only: listens for client disconnects, force-releases station/dialogue locks, restores physics on held objects. Attach to persistent scene object. |

### Player Prefab — Required Components (root)
`CharacterController` · `PlayerComponents` · `PlayerMovement` · `FocusStateManager` · `NetworkObject` · `ClientNetworkTransform` · `PlayerSetup`

**Camera child**: Camera component **disabled by default**. `AudioListener` **disabled by default**. `PlayerSetup.OnNetworkSpawn()` re-enables both for the owning client only.

### Scripts Converted to NetworkBehaviour

| Script | Change | Guard |
|---|---|---|
| `PlayerMovement.cs` | `MonoBehaviour` → `NetworkBehaviour` | `if (!IsOwner) return;` in `Update()` |
| `MouseLook.cs` | `MonoBehaviour` → `NetworkBehaviour` | `if (!IsOwner) return;` in `Update()` |
| `ObjectPickup.cs` | `MonoBehaviour` → `NetworkBehaviour` | `if (!IsOwner) return;` in `Update()` |
| `FocusStateManager.cs` | `MonoBehaviour` → `NetworkBehaviour` | `if (!IsOwner) return;` in `Update()` |
| `ItemPlacementManager.cs` | `MonoBehaviour` → `NetworkBehaviour` | `if (!IsOwner) return;` in `Update()` |
| `NPCDialogueTrigger.cs` | `MonoBehaviour` → `NetworkBehaviour` | `NetworkVariable<ulong> _dialogueOwnerId` lock — only one player can dialogue with an NPC at a time. `NetworkVariable<bool> _initialDialogueCompleted` prevents auto-trigger for other players once one player completes it. Requests go through `RequestDialogueLockServerRpc` → `GrantDialogueLockClientRpc`. Lock released via `ReleaseDialogueLockServerRpc` when dialogue ends. Non-spawned fallback retained for editor testing. |

### Scripts Left Unchanged (already safe)

| Script | Reason |
|---|---|
| `PillScraper.cs` | `Update()` already gates on `PlayerComponents.Local.FocusState.IsFocused` — only runs for the locally focused player |
| `IDCardInteraction.cs` | `Update()` gates on `_isActive`, which is only set by `ObjectPickup` (already owner-guarded) |
| `DialogueHistory.cs` | Local UI only; Enter key toggle has no network side effects |
| `ComputerScreenController.cs` | Debug `Update()` wrapped in `#if UNITY_EDITOR`; no gameplay input |

### PlayerComponents.Local — Ownership Rules
- `Local` setter is now `public` (was `private`)
- `Awake()` no longer sets `Local = this` (would be overwritten by each spawned player on the same machine)
- `PlayerSetup.OnNetworkSpawn()` sets `Local` **only when `IsOwner` is true**
- World scripts (NPCDialogueTrigger, PillScraper, etc.) access the local player via `PlayerComponents.Local`

### Known Timing Issue: NPCDialogueTrigger Warning
`NPCDialogueTrigger.Start()` reads `PlayerComponents.Local` before the player spawns (scene `Start()` runs before `NetworkManager.StartHost()` is called). The warning `"Could not find PlayerComponents/PlayerMovement in scene"` is harmless — `_playerTransform` stays null until a player is nearby. **Fix planned in Player Registry tier**: subscribe to a player-spawned event and assign `_playerTransform` lazily.

### Disconnect Handling

**Script**: `DisconnectHandler.cs` — Attach to a persistent scene GameObject (requires `NetworkObject`).

Server-only. Subscribes to `NetworkManager.OnClientDisconnectCallback` in `OnNetworkSpawn()` and cleans up all state held by the departing client:

| Cleanup | How |
|---|---|
| Station locks (`ComputerScreen`, `PillCountingStation`, `IDCardInteraction`) | Calls `ForceReleaseLock(clientId)` on each instance — resets `_currentUserId` to `NoUser` |
| Dialogue locks (`NPCDialogueTrigger`) | Calls `ForceReleaseLock(clientId)` — resets `_dialogueOwnerId` to `ulong.MaxValue` |
| Held objects | Scans `SpawnedObjectsList` for non-player `NetworkObject`s owned by the client, transfers ownership to server, restores `Rigidbody` (non-kinematic, gravity on) and re-enables `Collider` |

Each lockable script exposes `public void ForceReleaseLock(ulong clientId)` — server-only, no RPC needed.

### Object Pickup — Networking Status

All `NetworkObject` item mutations are server-authoritative:

| Action | RPC Flow |
|---|---|
| Pickup | `RequestPickupServerRpc` → `ConfirmPickupClientRpc` (all clients disable physics) → picker does `DoNetworkPickup` |
| Throw / Drop / Gentle Drop | `ReleaseHeldNetworkObject` → `ReleaseNetworkObjectServerRpc` → `RestoreObjectPhysicsClientRpc` (all clients re-enable physics) |
| Counter item bagging | `DeleteCounterItemServerRpc` → `CounterSlotNetwork.RecordRemoval` → `Despawn` |
| Place from box → shelf | `PlaceItemOnShelfServerRpc` (in `ItemPlacementManager`) |
| Place held item → shelf | `PlaceHeldItemOnShelfServerRpc` → `ShelfSlot.PlaceItem` + `ShelfSlotNetwork.RecordPlacement` + `RestoreObjectPhysicsClientRpc` |

**Physics sync**: `ConfirmPickupClientRpc` disables collider + sets kinematic on **all clients** (not just the picker), preventing phantom collisions. `RestoreObjectPhysicsClientRpc` re-enables on all clients when released.

Local (non-`NetworkObject`) fallback paths are retained for editor/single-player testing.

### What Still Needs Networking (pending tiers)
1. **Lobby** — Replace `QuickConnect.cs` with proper lobby UI.
