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
├── PlayerMovement.cs          # FPS movement + jumping
├── MouseLook.cs               # FPS camera look + screen shake
├── ObjectPickup.cs            # Central interaction hub (pickup, throw, place, interact)
├── HoldableItem.cs            # Per-item hold offset overrides
├── FrameRateManager.cs        # Static 60 FPS initializer
├── CashRegister.cs            # Checkout trigger
├── ComputerScreen.cs          # Computer focus + UI activation
├── ComputerScreenController.cs# View/tab manager for computer UI
│
├── Counter/
│   └── CounterSlot.cs         # NPC item placement + player "bagging"
│
├── Delivery/
│   └── DeliveryStation.cs     # Spawns InventoryBox prefabs
│
├── NPC/
│   ├── IInteractable.cs       # Interface: NPC-pickable objects
│   ├── IPlaceable.cs          # Interface: player-placeable targets
│   ├── InteractableItem.cs    # Shelf item that NPCs pick up
│   ├── ItemCategory.cs        # ScriptableObject: item type + prefab
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
└── Shelf/
    ├── ShelfSection.cs        # IPlaceable shelf with multiple slots
    ├── ShelfSlot.cs           # Individual slot with multi-item support
    ├── InventoryBox.cs        # Portable item container (decrements + shrinks)
    ├── BoxItemPreview.cs      # Visual preview of next items inside box
    └── ItemPlacementManager.cs# Box-to-shelf placement workflow + ghost previews
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

### MouseLook.cs
Attached to the **Camera** (child of Player). Singleton (`MouseLook.Instance`).

| Feature | Details |
|---|---|
| Look | Mouse X/Y with configurable sensitivity |
| Smoothing | SmoothDamp (default), Lerp, or raw input |
| Acceleration | Optional mouse acceleration above threshold |
| Screen Shake | `Shake(intensity, duration)` — callable from any script |

**Editor setup**: Assign `playerBody` to the Player root Transform.

---

## System 2: Focus State Manager (Singleton)

**Script**: `FocusStateManager.cs` — Attach to Player or any persistent GameObject.  
**Singleton**: `FocusStateManager.Instance`

Manages transitions between **FPS mode** and **focused mode** (computer screen, pill counting). When entering focus:

1. Saves camera parent, local position, and rotation
2. Disables `PlayerMovement`, `MouseLook`, `ObjectPickup`
3. Unlocks + shows cursor (`CursorLockMode.Confined`)
4. Lerps camera to target Transform over `transitionDuration` (0.6s)
5. On exit (Escape key): reverse lerps camera back, re-parents, re-enables FPS scripts

**API**:
```csharp
FocusStateManager.Instance.EnterFocus(Transform target, Action onExit);
FocusStateManager.Instance.ExitFocus();
```

**Events**: `OnFocusChanged(bool entering)` — subscribe for custom focus reactions.

**Auto-finds** at Start: `PlayerMovement`, `MouseLook`, `ObjectPickup`, `Camera.main` (with fallbacks).

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

**Editor setup**: Assign `handTransform`, `counterTarget`, `exitTarget`, `allowedShelfSlots[]`, `counterSlots[]`.

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

## Cross-System Dependencies

```
ObjectPickup ──→ ComputerScreen ──→ FocusStateManager
     │                                      ↑
     ├──→ PillCountingStation ──────────────┘
     │
     ├──→ DeliveryStation ──→ InventoryBox
     │
     ├──→ ItemPlacementManager ──→ ShelfSection ──→ ShelfSlot
     │         │                                      ↑
     │         └──→ BoxItemPreview                    │
     │         └──→ InventoryBox                      │
     │                                                │
     ├──→ CounterSlot ←── NPCInteractionController ──┘
     │                           │
     └──→ CashRegister ─────────┘
                                 │
              NPCAnimationController (subscribes to NPC events)
```

### Key interaction chains:

1. **Shelf Restocking**: `DeliveryStation.SpawnBox()` → player picks up `InventoryBox` → `ItemPlacementManager` detects nearby shelves → builds queue from `ShelfSection.GetMissingItems()` → player places items → `ShelfSlot.PlaceItem()` + `InventoryBox.Decrement()`

2. **NPC Shopping**: `NPCInteractionController` scans `ShelfSlot[]` → navigates → picks up `InteractableItem` → navigates to counter → `CounterSlot.PlaceItem()` → waits → `CashRegister.TriggerCheckout()` → navigates to exit → destroys self

3. **Pill Counting**: `ObjectPickup` detects `PillCountingStation` → `Activate()` → `FocusStateManager.EnterFocus()` → `PillSpawner.SpawnPills()` → player uses `PillScraper` → pills enter `PillCountingChute` → count reaches target → auto-exit

4. **Computer Screen**: `ObjectPickup` detects `ComputerScreen` → `Activate()` → `FocusStateManager.EnterFocus()` → `ComputerScreenController.ResetToMain()` → player clicks tabs/buttons on World Space Canvas → Escape exits

---

## Common Editor Setup Checklist

### Player Setup
- [ ] Player GameObject with `CharacterController`
- [ ] `PlayerMovement` on Player root
- [ ] Camera as child of Player with `MouseLook` (assign `playerBody` = Player root)
- [ ] `ObjectPickup` on Camera (assign `playerCamera`, `holdPoint`)
- [ ] `ItemPlacementManager` on Camera or Player (assign `ghostMaterial`)
- [ ] `FocusStateManager` on Player (auto-finds everything)

### Shelf Setup
- [ ] Shelf parent with `ShelfSection` (auto-finds child slots)
- [ ] Each shelf layer: empty child with `ShelfSlot`
- [ ] Each `ShelfSlot`: assign `acceptedCategory` (ItemCategory asset) and configure `itemPlacements[]` positions
- [ ] Ensure shelf has a Collider somewhere in hierarchy (for `IPlaceable` detection)

### NPC Setup
- [ ] NPC with `NavMeshAgent` + `NPCInteractionController` + `NPCAnimationController`
- [ ] Assign `handTransform` (child bone), `counterTarget`, `exitTarget` Transforms
- [ ] Assign `allowedShelfSlots[]` and `counterSlots[]`
- [ ] Animator Controller with params: `IsWalking` (bool), `Speed` (float), `PickUp` (trigger), `Place` (trigger)
- [ ] Item prefabs need `InteractableItem` component + `Collider` + child named `GrabTarget`

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

## Key Singletons

| Singleton | Access | Purpose |
|---|---|---|
| `FocusStateManager.Instance` | Static | Camera transitions, FPS control toggling |
| `MouseLook.Instance` | Static | Screen shake, sensitivity adjustment |

---

## ScriptableObjects

| Type | Create Menu | Purpose |
|---|---|---|
| `ItemCategory` | Create → NPC → Item Category | Defines item type with prefab and rotation offset |
