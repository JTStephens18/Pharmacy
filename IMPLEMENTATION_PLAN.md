# Implementation Plan — Missing Systems

> What the Game Design Doc calls for vs what exists, and how to build the rest.
> Every system described here must work with the existing NGO multiplayer architecture (host-authoritative, 1-3 players).

---

## Status Key

- **Exists** — Implemented and working
- **Partial** — Some infrastructure exists, needs extension
- **Missing** — No code exists

---

## 1. Shift Manager (Missing)

**What the design doc says:** The game alternates between a day shift (pharmacy work, NPC customers, doppelganger detection) and a night shift (monster mode, crafting, survival). Zero escaped doppelgangers = clean end. One or more escaped = monster spawns at closing.

**What exists:** `GameStarter.cs` calls `NPCSpawnManager.StartNPCSpawning()` on Start. No concept of phases, shift end, or transitions.

### Implementation

**New script: `ShiftManager.cs`** — `NetworkBehaviour` on a persistent scene GameObject.

```
ShiftManager (server-authoritative)
├── NetworkVariable<int> _currentPhase   (DayShift, Transition, NightShift, Dawn)
├── NetworkVariable<int> _escapedDoppelgangers
├── NetworkVariable<int> _currentNight   (1, 2, 3...)
│
├── StartDayShift()
│   ├── Reset counters
│   ├── Generate RoundConfig for this night (NPC pool + doppelganger assignments)
│   ├── Spawn recipe note at random location
│   └── NPCSpawnManager.StartNPCSpawning(config)
│
├── OnAllNPCsFinished()          ← subscribe to NPCSpawnManager.OnAllNPCsFinished
│   ├── if _escapedDoppelgangers == 0 → EndShiftClean()
│   └── else → StartTransition()
│
├── StartTransition()
│   ├── _currentPhase = Transition
│   ├── TriggerLightsFlickerClientRpc()
│   └── after delay → StartNightShift()
│
├── StartNightShift()
│   ├── _currentPhase = NightShift
│   ├── Dim lights (MonsterLightingClientRpc)
│   ├── Spawn monster(s) at delivery room
│   └── Start dawn timer
│
├── OnMonsterKilled()
│   └── if all monsters dead → EndShiftClean()
│
├── OnDawnReached()              ← dawn timer expires
│   ├── Monster retreats (despawn)
│   └── Next night: harder recipe, faster monster
│
└── EndShiftClean()
    ├── _currentPhase = Dawn
    ├── Cleanup remaining entities
    ├── _currentNight++
    └── after delay → StartDayShift()
```

**Multiplayer notes:**
- `_currentPhase` is a `NetworkVariable<int>` — all clients read it to drive local UI/lighting/audio.
- Shift transitions are server-initiated. Clients react via `OnValueChanged` callbacks or ClientRpcs for one-shot effects (lights flicker, audio stings).
- Subscribe a `PhaseUIController` (local-only MonoBehaviour on each player) to `_currentPhase.OnValueChanged` for HUD updates.

**Integration points:**
- `GameStarter.cs` → calls `ShiftManager.StartDayShift()` instead of directly calling `NPCSpawnManager`.
- `NPCSpawnManager.OnAllNPCsFinished` → wired to `ShiftManager.OnAllNPCsFinished()`.
- `NPCInteractionController` → reports doppelganger escape via `ShiftManager.ReportEscape()`.

---

## 2. Doppelganger System (Missing)

**What the design doc says:** A portion of NPCs are doppelgangers with discrepancies in their profile (photo mismatch, invalid NPI, wrong DOB, etc.). Player has a limited question budget (5) per NPC. Correct rejection = eliminate doppelganger (blood, cleanup). Wrong approval = doppelganger escapes silently, triggers monster at closing.

**What exists:** `NPCIdentity` ScriptableObject has basic fields (name, DOB, address, ID number, photo). No concept of "real vs fake" or discrepancies. The gun system can kill NPCs and leave blood. The mop can clean blood.

### Implementation

#### 2a. Extend NPCIdentity or create DoppelgangerProfile

**Option A (recommended): New ScriptableObject `PrescriptionData.cs`**

Rather than polluting `NPCIdentity` (which represents the real person), create a separate SO that represents what this NPC *claims*. This cleanly separates ground truth from presented data.

```csharp
[CreateAssetMenu(menuName = "NPC/Prescription Data")]
public class PrescriptionData : ScriptableObject
{
    [Header("Prescription")]
    public string medicationName;
    public int quantity;
    public string dosage;
    public string prescriberName;
    public string prescriberNPI;
    public string prescriberSpecialty;
    public string prescriberAddress;

    [Header("Fill History")]
    public string[] previousFills;  // e.g. "2026-01-15: 30x 0.5mg"
}
```

**New ScriptableObject: `DoppelgangerProfile.cs`**

```csharp
[CreateAssetMenu(menuName = "NPC/Doppelganger Profile")]
public class DoppelgangerProfile : ScriptableObject
{
    [Header("Discrepancies — what's wrong with this doppelganger")]
    public DiscrepancyType[] discrepancies;

    [Header("Fake Data — overrides the real NPCIdentity fields")]
    public string fakePhotoDescription;    // empty = use real photo
    public Sprite fakeDOBPhoto;            // mismatched photo
    public string fakeDOB;                 // wrong DOB
    public string fakeAddress;             // wrong address
    public string fakePrescriberNPI;       // invalid NPI
    public string fakePrescriberSpecialty; // wrong specialty
    public string fakeDosage;              // dose jump
    public int fakeQuantity;               // non-standard quantity
}

public enum DiscrepancyType
{
    PhotoMismatch,
    InvalidNPI,
    NoFillHistory,
    WrongPrescriberSpecialty,
    DoseJump,
    NonStandardQuantity,
    PrescriberOutsideArea
}
```

#### 2b. NPC Prefab Changes

Add to `NPCInteractionController`:
```csharp
[Header("Doppelganger")]
[SerializeField] private DoppelgangerProfile doppelgangerProfile;  // null = real patient
[SerializeField] private PrescriptionData prescriptionData;

public bool IsDoppelganger => doppelgangerProfile != null;
public DoppelgangerProfile DoppelgangerData => doppelgangerProfile;
public PrescriptionData Prescription => prescriptionData;
```

The `doppelgangerProfile` field is set at spawn time by `NPCSpawnManager` (or left null for real patients). This keeps it server-authoritative — the client never knows the ground truth until they investigate.

#### 2c. RoundConfig Extension

Extend `QueueEntry` to support doppelganger assignment:

```csharp
[System.Serializable]
public class QueueEntry
{
    public bool isFixed;
    public GameObject fixedNpcPrefab;

    [Header("Doppelganger")]
    public bool forceDoppelganger;              // authored doppelganger for set pieces
    public DoppelgangerProfile fixedProfile;    // specific profile for authored doppelgangers
}
```

`ShiftManager` resolves doppelganger assignments at shift start: picks N random NPCs from the queue to be doppelgangers (unless `forceDoppelganger` is already set), assigns `DoppelgangerProfile` assets from a pool. This is server-only logic.

#### 2d. Verification Flow on Computer Screen

**New computer view: "Patient Verification"** — added to `ComputerScreenController.views[]`.

Shows:
- Patient photo (from `NPCIdentity` or doppelganger override)
- Prescription details (medication, quantity, dosage, prescriber)
- Fill history
- Prescriber database lookup (NPI validation)
- **Approve** and **Reject** buttons

The verification view is populated when the player scans an NPC's ID card (existing `NPCInfoDisplay.ShowNPCInfo` flow). Extend `NPCInfoDisplay` to also populate prescription fields from the NPC's `PrescriptionData`.

**Approve button:** `ApprovePatientServerRpc(npcNetworkId)` — server checks if NPC is a doppelganger. If yes, increments `ShiftManager._escapedDoppelgangers` and the NPC exits silently. If no, normal checkout proceeds.

**Reject button:** `RejectPatientServerRpc(npcNetworkId)` — server checks. If correct rejection (NPC is a doppelganger), NPC enters a new `Rejected` state. If wrong rejection (real patient), money penalty via the quota system.

**Multiplayer notes:**
- Approve/Reject are ServerRpcs. Server validates and broadcasts result.
- Only the player currently at the computer (locked via `ComputerScreen._currentUserId`) can submit decisions.
- Other players see the NPC's reaction but not the computer screen details.

#### 2e. Question Budget

**New script: `QuestionBudget.cs`** — tracks questions asked per NPC per player interaction.

Each NPC has `maxQuestions = 5`. The dialogue system's `NPCInfoTalkButton` (which triggers info dialogues like "ask about DOB", "ask about address") decrements the budget. When budget hits 0, the NPC gets impatient — dialogue auto-closes and the NPC demands checkout.

This integrates directly with the existing `NPCDialogueTrigger.StartInfoDialogue(key)` flow. Add a counter that decrements on each call and fires an `OnBudgetExhausted` event.

---

## 3. Prescription Verification UI (Missing)

**What the design doc says:** Pull up patient record on the computer. Cross-reference physical script with database: photo ID, DOB, address, prescriber NPI, fill history, dose consistency. Decide to approve or reject.

**What exists:** `ComputerScreenController` supports tabbed views. `NPCInfoDisplay` shows basic NPC identity (name, DOB, address, photo). `NPCIdentityField` auto-populates TMP elements.

### Implementation

Extend the existing computer screen with two new views:

**View: "Patient Record"** — Shows full prescription data alongside identity.

| Section | Data Source | UI Element |
|---|---|---|
| Photo + Name | `NPCIdentity` (or doppelganger override) | Image + TMP |
| DOB / Address / ID# | `NPCIdentity` fields | TMP via `NPCIdentityField` |
| Prescription details | `PrescriptionData` on the NPC | New `PrescriptionField` component (same pattern as `NPCIdentityField`) |
| Fill history | `PrescriptionData.previousFills` | Scrollable TMP list |
| Prescriber info | `PrescriptionData` prescriber fields | TMP fields |

**View: "Prescriber Database"** — Player can look up NPI numbers.

| Feature | Implementation |
|---|---|
| NPI search field | TMP_InputField + search button |
| Results panel | Shows prescriber name, specialty, address if NPI is valid |
| Validation | Compare entered NPI against a `PrescriberDatabase` ScriptableObject (list of valid NPIs) |

**New ScriptableObject: `PrescriberDatabase.cs`** — contains a list of valid prescriber entries. Shared across all clients as a game asset.

**Approve/Reject buttons** live on the Patient Record view. They call ServerRpcs on a new `VerificationManager.cs` (NetworkBehaviour) that resolves outcomes.

**Multiplayer notes:**
- The computer screen is already exclusively locked per player. No contention issues.
- Prescription data is read from ScriptableObjects on the NPC prefab — available on all clients.
- Approve/Reject decisions go through ServerRpcs. Results broadcast via ClientRpc (NPC reaction visible to all).

---

## 4. Question Budget & NPC Impatience (Missing)

**What the design doc says:** Player can ask each NPC a limited number of questions (suggested: 5) before the NPC gets impatient and leaves.

**What exists:** `NPCDialogueTrigger` supports keyed info dialogues (`StartInfoDialogue(key)`). `NPCInfoTalkButton` on the computer screen triggers these.

### Implementation

Add to `NPCDialogueTrigger`:

```csharp
[Header("Question Budget")]
[SerializeField] private int maxQuestions = 5;
private NetworkVariable<int> _questionsRemaining;

public int QuestionsRemaining => _questionsRemaining.Value;
public event Action OnBudgetExhausted;
```

Each `StartInfoDialogue(key)` call decrements `_questionsRemaining` (server-side). When it hits 0:
- Fire `OnBudgetExhausted`
- NPC dialogue auto-says "I don't have time for this" (terminal node)
- NPC demands checkout or leaves

`NPCInfoTalkButton` reads `QuestionsRemaining` to show a counter on the UI ("3 questions remaining") and disables itself at 0.

**Multiplayer notes:**
- `_questionsRemaining` is a `NetworkVariable<int>` — all players see the same budget.
- Only the player with the dialogue lock can ask questions (existing `_dialogueOwnerId` lock handles this).
- Budget is per-NPC, not per-player. If Player A asks 2 questions and Player B asks 3, the NPC's budget is exhausted.

---

## 5. Dispensing Flow (Missing)

**What the design doc says:** After approving a prescription, retrieve medication from the dispensary cabinet, count the correct dose at the pill counting station (or measure/compound at the mortar), label the bottle, hand it over.

**What exists:** `PillCountingStation` works as a mini-game (spawn pills, scrape into chute, count to target). No dispensary cabinet, no labeling, no bottle handover.

### Implementation

**New script: `DispensaryCabinet.cs`** — `NetworkBehaviour` on a shelf/cabinet GameObject.

Functions like `DeliveryStation` but for medication bottles. Player presses E to retrieve the correct medication bottle (based on the current active prescription). Server spawns a `MedicationBottle` prefab.

**New script: `MedicationBottle.cs`** — pickable item (Rigidbody + NetworkObject). Has an `ItemCategory` and a `requiredPillCount` field. Player takes it to the pill counting station.

**Extend `PillCountingStation`:**
- Accept a `MedicationBottle` as input (placed on the station)
- Set `targetPillCount` from the bottle's `requiredPillCount`
- On completion, bottle becomes "filled" (visual change, flag set)

**New script: `LabelStation.cs`** (optional, lower priority) — Focus station where player applies a label. Could be as simple as pressing E on a label printer while holding the filled bottle.

**Handover:** Player places the filled+labeled bottle on the counter. NPC picks it up (reuse existing `CounterSlot` + NPC placing logic in reverse — NPC takes item from counter instead of placing).

**Multiplayer notes:**
- `DispensaryCabinet` follows the same exclusive-lock pattern as `ComputerScreen` — `NetworkVariable<ulong> _currentUserId`.
- `MedicationBottle` is a `NetworkObject` — pickup/drop uses existing `ObjectPickup` networking.
- Pill counting station already has exclusive access networking.

---

## 6. Mortar & Pestle Station (Missing)

**What the design doc says:** Day use: compounds certain prescriptions requiring grinding. Night use: grinds base/catalyst ingredients for weapon crafting. Rotation-based input (~6-8 rotations).

**What exists:** `PillCountingStation` and `FocusStateManager` provide the pattern for focus-mode stations.

### Implementation

**New script: `MortarStation.cs`** — `NetworkBehaviour`, same pattern as `PillCountingStation`.

```
MortarStation
├── NetworkVariable<ulong> _currentUserId     (exclusive access)
├── Activate() → RequestActivationServerRpc → EnterFocus
├── focusCameraTarget (Transform)
│
├── Input: mouse circular motion detection
│   ├── Track mouse delta angle each frame
│   ├── Accumulate total rotation (threshold per "grind")
│   └── After N grinds (6-8) → grinding complete
│
├── OnGrindComplete()
│   ├── Transform input item into processed version
│   └── ExitFocus after delay
│
└── Deactivate() → DoDeactivate + ReleaseActivationServerRpc
```

**Input detection:** Track `Input.mousePosition` delta. Calculate angle change around center of screen. Accumulate clockwise rotation. Each 360 degrees = one grind. Show progress UI (world-space, same pattern as `PillCountUI`).

**Dual use:**
- Day: player places an unground prescription ingredient → grinds → receives ground version.
- Night: player places a crafting ingredient that requires grinding → grinds → receives processed ingredient.

Both cases use the same mechanic — the item placed determines the output.

**Multiplayer notes:**
- Same exclusive-access pattern as other stations (`_currentUserId` NetworkVariable).
- Grinding progress is local (visual only). Completion fires a ServerRpc that spawns the processed item.
- Add `ForceReleaseLock(clientId)` for `DisconnectHandler`.

---

## 7. Monster AI (Missing)

**What the design doc says:** Uses same NavMesh as daytime NPCs. Reacts to sound (running, dropping items, mop dragging). Walking is silent, sprinting is risky. Lights dim as dawn approaches. At least one ingredient always spawns in/near the monster's patrol route.

**What exists:** `NPCInteractionController` uses NavMeshAgent with a full state machine. The gun can kill NPCs. Blood splatter + cleanup exists.

### Implementation

**New script: `MonsterController.cs`** — `NetworkBehaviour` with NavMeshAgent. Server-authoritative AI.

```
MonsterController (server-only AI, clients get position via NetworkTransform)
├── NetworkVariable<int> _monsterState  (Patrol, Investigating, Chasing, Stunned, Dying, Dead)
│
├── Patrol
│   ├── Walk between patrol waypoints (reuse NPC NavMesh paths)
│   ├── Speed: slow base, scales up as dawn approaches
│   └── Random pause at waypoints
│
├── Investigating
│   ├── Triggered by NoiseEvent (position + loudness)
│   ├── Navigate to noise source
│   ├── Search area briefly
│   └── Return to patrol if nothing found
│
├── Chasing
│   ├── Triggered by line-of-sight detection of a player
│   ├── Pursue at chase speed
│   └── Attack on contact (damage/kill player)
│
├── Stunned (weapon effect)
│   ├── Triggered by specific weapon types (aerosol first spray, belladonna)
│   ├── Duration based on base ingredient
│   └── Returns to Patrol or transitions to Dying
│
├── Dying
│   ├── Triggered by lethal weapon hit
│   ├── Play death animation
│   └── Despawn → report to ShiftManager.OnMonsterKilled()
│
└── Detection
    ├── Sight: SphereCast forward, angle check, LOS raycast
    ├── Sound: subscribe to NoiseSystem.OnNoiseEmitted
    └── Proximity: close-range detection ignoring LOS
```

**Multiplayer notes:**
- AI runs on server only (`if (!IsServer) return` in Update).
- Position synced via `NetworkTransform`.
- State synced via `NetworkVariable<int>` — clients use it for animations and audio.
- Monster spawn: server instantiates prefab + `NetworkObject.Spawn()`. Same flow as NPC spawning.
- Player damage/kill: `MonsterController` detects collision on server → `DamagePlayerClientRpc(clientId)` → target player's local scripts handle death/damage UI.

**Patrol waypoints:** Reuse NPC navigation targets. `ShiftManager` passes the list of shelf/counter/exit positions used during the day shift as patrol waypoints. This matches the design doc: "patrols the paths customers walked."

---

## 8. Noise System (Missing)

**What the design doc says:** Monster reacts to sound. Running, dropping items, opening crates draw it. Walking is silent. Mop creates deliberate noise as a lure.

**What exists:** `PlayerMovement` has walk/sprint states. `Mop.cs` exists. Objects can be dropped. No sound tracking.

### Implementation

**New script: `NoiseSystem.cs`** — static event bus (no MonoBehaviour needed).

```csharp
public static class NoiseSystem
{
    public static event Action<Vector3, float> OnNoiseEmitted;  // position, loudness (0-1)

    public static void EmitNoise(Vector3 position, float loudness)
    {
        OnNoiseEmitted?.Invoke(position, loudness);
    }
}
```

**Noise sources (server-only emissions):**

| Source | When | Loudness | Integration Point |
|---|---|---|---|
| Sprinting | Each frame while sprinting | 0.3 | `PlayerMovement.Update()` — emit when `isSprinting && IsServer` (or via ServerRpc from client) |
| Dropping items | On throw/drop | 0.5 | `ObjectPickup.ReleaseNetworkObjectServerRpc()` |
| Opening crates | On delivery box spawn | 0.4 | `DeliveryStation.SpawnBoxServerRpc()` |
| Mop dragging | While mopping | 0.6 | `Mop.CleanDecalsServerRpc()` — emit noise at clean position |
| Doors | On open/close | 0.3 | `Door` interaction |
| Gun shot | On fire | 1.0 | `GunCase.ShootNPCServerRpc()` |

**Multiplayer notes:**
- Noise emissions happen on the server. `MonsterController` subscribes to `NoiseSystem.OnNoiseEmitted` on the server.
- Client actions that generate noise send ServerRpcs (most already do — sprinting is the main new one).
- Add a `NoiseEmissionServerRpc(Vector3 pos, float loudness)` helper that any script can call through a shared `NoiseEmitter` component on the player, or emit directly in existing ServerRpcs.

---

## 9. Recipe & Crafting System (Missing)

**What the design doc says:** Recipe note posted during the day. 3 ingredients across 3 roles (Base, Catalyst, Vessel). Some require grinding. Player gathers ingredients, processes one, places all 3 on counter in correct order. Wrong order consumes ingredients.

**What exists:** `CounterSlot` supports item placement with ordered positions. `ItemCategory` ScriptableObject exists. `PillCountingStation` provides the station pattern.

### Implementation

#### 9a. Ingredient Data

**New ScriptableObject: `IngredientData.cs`**

```csharp
public enum IngredientRole { Base, Catalyst, Vessel }
public enum ProcessingType { None, Grinding, Measuring }

[CreateAssetMenu(menuName = "Crafting/Ingredient")]
public class IngredientData : ScriptableObject
{
    public string ingredientName;
    public IngredientRole role;
    public ProcessingType processingRequired;
    public string effectDescription;
    public GameObject prefab;              // world item prefab
    public GameObject processedPrefab;     // prefab after processing (null if no processing needed)
    public Sprite icon;                    // for recipe note UI
}
```

#### 9b. Recipe Generation

**New ScriptableObject: `RecipeDatabase.cs`** — holds all ingredient assets, organized by role. Contains the randomization rules from the design doc.

**New script: `RecipeGenerator.cs`** — static utility. Takes `currentNight` and `previousVessel`, returns a `Recipe` (Base + Catalyst + Vessel + processing steps).

Hard rules enforced:
- Never two grinding ingredients in one recipe
- Vessel assigned first
- Night 1: only vial or syringe
- No repeat vessel on consecutive nights

#### 9c. Recipe Note

**New script: `RecipeNote.cs`** — a world-space interactable (like a pickup but reads instead of carries). Player presses E to read.

- Server generates the recipe in `ShiftManager.StartDayShift()` → stores as `NetworkVariable` or syncs via ClientRpc.
- `RecipeNote` spawns at one of several predefined locations (pinboard, compounding station, delivery door).
- On interact: shows a UI overlay with the recipe (ingredient names, roles, processing steps, slot order). Local-only UI.

#### 9d. Crafting at the Counter

**New script: `CraftingManager.cs`** — `NetworkBehaviour`.

Repurposes `CounterSlot` during night shift. When `ShiftManager._currentPhase == NightShift`:
- Counter slots accept `IngredientData` items instead of regular shop items.
- Player places 3 ingredients in counter slots (existing placement flow).
- On placing the 3rd ingredient: `ValidateCraftServerRpc()`.
  - Server checks: correct ingredients? correct order? all processed?
  - **Success:** despawn ingredients, spawn weapon prefab on counter.
  - **Failure:** despawn ingredients (consumed), no weapon. Player must re-gather.

**Multiplayer notes:**
- Recipe is generated server-side, synced to all clients.
- Ingredient placement uses existing `CounterSlot` + networking.
- Craft validation is a ServerRpc. Result broadcast via ClientRpc.
- Any player can place ingredients (collaborative gathering is intended).

---

## 10. Weapon System — Night Use (Partial)

**What the design doc says:** 4 weapon types determined by vessel: thrown vial (instant kill on hit, miss = gone), aerosol spray (two-stage), deployed trap (lure-based), syringe (close contact instant kill).

**What exists:** `GunCase.cs` handles gun pickup + shooting NPCs. Not designed for crafted weapons.

### Implementation

**New script: `CraftedWeapon.cs`** — base class (NetworkBehaviour) for all crafted weapons. Spawned by `CraftingManager` on successful craft.

```
CraftedWeapon (abstract base)
├── NetworkVariable<ulong> _holderId
├── IngredientData baseIngredient    (determines effect)
├── PickUp() / Drop()               (same pattern as GunCase)
│
├── ThrownVial : CraftedWeapon
│   ├── Left-click: spawn projectile, apply velocity
│   ├── On collision with MonsterController → instant kill
│   └── On collision with anything else → shatter, weapon lost
│
├── AerosolSpray : CraftedWeapon
│   ├── Left-click: cone raycast forward
│   ├── First hit → MonsterController.ApplyStun()
│   ├── Second hit during stun window → MonsterController.Kill()
│   └── NetworkVariable<int> _usesRemaining (2)
│
├── DeployedTrap : CraftedWeapon
│   ├── Left-click: place on ground at feet
│   ├── Spawns trap prefab with trigger collider
│   ├── Monster walks over → MonsterController.Kill()
│   └── Player must lure monster via NoiseSystem
│
└── Syringe : CraftedWeapon
    ├── Left-click: melee range check (short raycast)
    ├── Hit MonsterController → instant kill
    └── Miss → weapon not consumed (can retry)
```

**Multiplayer notes:**
- Weapon pickup uses the same `NetworkVariable<ulong> _holderId` pattern as `GunCase`.
- Weapon effects (stun, kill) go through ServerRpcs.
- Trap placement: `PlaceTrapServerRpc(position, rotation)` → server spawns trap NetworkObject.
- Projectile (vial): `ThrowVialServerRpc(direction)` → server spawns projectile NetworkObject with velocity.

**Integration with GunCase:** The gun is a day-shift tool (for eliminating confirmed doppelgangers). Crafted weapons are night-shift tools (for killing the monster). They use the same hold-slot pattern but are mutually exclusive — player can hold one weapon at a time.

---

## 11. Doppelganger Elimination Flow (Missing)

**What the design doc says:** Correct rejection → doppelganger can be physically eliminated. Always leaves a mess (blood, ichor) that must be cleaned before the next customer.

**What exists:** Gun + `Kill()` + blood splatter + mop cleanup all work.

### Implementation

When a player correctly rejects a doppelganger via the computer's Reject button:
1. Server sets NPC state to new `Rejected` state.
2. NPC plays a reaction (agitated animation, backs away from counter).
3. NPC enters `Vulnerable` state briefly — player can shoot them with the gun.
4. `Kill()` triggers blood splatter (existing system).
5. `ShiftManager` tracks that this doppelganger was caught (not escaped).
6. Next NPC does not spawn until blood is cleaned (check `BloodDecal.Active.Count == 0`).

**Alternative (simpler, depends on design decision):** Rejection auto-eliminates the doppelganger (no shooting required). Triggers blood splatter at NPC position, NPC despawns. This avoids the open design question about physical elimination and uses existing systems directly.

**Multiplayer notes:**
- Rejection is a ServerRpc (already described in section 2).
- Blood splatter broadcast via existing `SpawnBloodSplatterClientRpc`.
- Cleanup check (`BloodDecal.Active.Count`) is server-side before allowing next NPC spawn.

---

## 12. Quota & Money System (Missing)

**What the design doc says:** Wrong rejection = real patient complaint, money penalty. Doppelgangers escaping has consequences. Quota/money system tracks player performance.

**What exists:** Nothing.

### Implementation

**New script: `ShiftScoreManager.cs`** — `NetworkBehaviour`.

```csharp
public class ShiftScoreManager : NetworkBehaviour
{
    public NetworkVariable<int> Money;
    public NetworkVariable<int> CustomersServed;
    public NetworkVariable<int> CustomerComplaints;
    public NetworkVariable<int> DoppelgangersCaught;
    public NetworkVariable<int> DoppelgangersEscaped;

    // Called by VerificationManager
    public void RecordCorrectApproval()    { Money.Value += 50; CustomersServed.Value++; }
    public void RecordWrongApproval()      { DoppelgangersEscaped.Value++; }
    public void RecordCorrectRejection()   { DoppelgangersCaught.Value++; Money.Value += 25; }
    public void RecordWrongRejection()     { CustomerComplaints.Value++; Money.Value -= 30; }
}
```

**UI:** A simple HUD element on each player showing money and shift stats. Reads `NetworkVariable` values.

**Shift-end summary:** At `ShiftManager.EndShiftClean()`, display a results screen showing performance. Server writes final values; clients read and display.

**Multiplayer notes:**
- All values are `NetworkVariable` — visible to all players.
- Only the server modifies values (write permission = Server).
- Individual player contributions could be tracked with a `Dictionary<ulong, int>` if needed, but the design doc implies shared team performance.

---

## 13. Lighting & Atmosphere (Missing)

**What the design doc says:** Lights flicker at transition. Gradually dim during night. Full dark = monster moves faster.

**What exists:** No lighting control scripts.

### Implementation

**New script: `ShiftLighting.cs`** — MonoBehaviour on each client (not networked, reads `ShiftManager._currentPhase`).

```
ShiftLighting
├── Subscribe to ShiftManager._currentPhase.OnValueChanged
├── DayShift → full brightness, warm color temp
├── Transition → flicker sequence (coroutine toggling lights rapidly)
├── NightShift → dim ambient, blue-tinted, gradual dimming over time
└── Dawn → gradual return to full brightness
```

Controls:
- `RenderSettings.ambientLight`
- Scene directional light intensity + color
- Point lights in the pharmacy (array of `Light` references)

**Monster speed scaling:** `MonsterController` reads a `NetworkVariable<float> _lightLevel` from `ShiftManager` (server decrements over time during night). Monster speed = `baseSpeed + (1 - lightLevel) * speedBonus`.

---

## 14. Ingredient Spawning & Placement (Missing)

**What the design doc says:** Ingredients scattered across shelves, crates, storage. At least one always spawns in/near the monster's patrol route.

**What exists:** `ShelfSlot` + `ShelfSection` for item placement. `DeliveryStation` for box spawning.

### Implementation

**New script: `IngredientSpawner.cs`** — server-only, called by `ShiftManager.StartNightShift()`.

```
IngredientSpawner
├── ingredientSpawnPoints[]     (Transform array: shelves, crates, storage, delivery room)
├── dangerousSpawnPoints[]      (Transforms near monster patrol route)
│
├── SpawnIngredients(Recipe recipe)
│   ├── For each ingredient in recipe:
│   │   ├── Pick a random spawn point
│   │   ├── Ensure at least one ingredient uses a dangerousSpawnPoint
│   │   └── Instantiate prefab + NetworkObject.Spawn()
│   └── Also spawn some decoy ingredients (wrong items, adds uncertainty)
```

Ingredients are regular pickable items (Rigidbody + NetworkObject). Player picks them up with existing `ObjectPickup`. Items that need grinding go to the `MortarStation`. All items eventually go to the counter.

**Multiplayer notes:**
- Server spawns all ingredients. Clients see them via NetworkObject sync.
- Any player can pick up any ingredient (collaborative gathering).
- Carry limit: design doc says max 2 items. Current `ObjectPickup` holds 1. Need to extend to support 2-item carrying or treat this as a design simplification.

---

## 15. Carry Limit (Missing)

**What the design doc says:** Player carries a maximum of 2 items at once.

**What exists:** `ObjectPickup` holds exactly 1 item.

### Implementation

Extend `ObjectPickup`:
- `_heldObjects` list (max size 2) instead of single `_heldObject`.
- Second hold point (`holdPoint2`) offset from the first.
- E picks up into first empty slot. G drops most recent.
- Throwing with left-click throws the most recent item.

**Multiplayer notes:**
- Each pickup/drop already goes through ServerRpcs. Extending to 2 slots means the ServerRpc payloads include a slot index, but the pattern is identical.

**Consideration:** This is a significant change to `ObjectPickup` which is the most complex script in the codebase. Could defer this to polish phase and keep 1-item carry for initial implementation.

---

## 16. OTC (Over-the-Counter) Requests (Missing)

**What the design doc says:** Some NPCs approach the counter to ask for OTC medication without a prescription. Player retrieves from behind-counter shelf and rings it up. Gives doppelgangers a plausible reason to approach without triggering verification.

**What exists:** NPC shopping behavior (browse shelves, bring items to counter). Cash register checkout.

### Implementation

Add a new NPC behavior variant. In `NPCInteractionController`:

```csharp
[Header("OTC Settings")]
[SerializeField] private bool isOTCCustomer = false;
[SerializeField] private ItemCategory otcRequestedItem;
```

When `isOTCCustomer` is true:
- NPC skips shelf browsing, walks directly to counter.
- NPC dialogue requests a specific OTC item ("I need some cold medicine").
- Player retrieves from a behind-counter shelf (new `OTCShelf` — just a `ShelfSection` behind the counter).
- Player places item on counter → NPC takes it → checkout.
- **No ID card, no verification** — this is the ambiguity. A doppelganger posing as an OTC customer bypasses the verification system entirely. The player must decide: is this really just an OTC request, or should I ask for ID anyway?

**Multiplayer notes:**
- Same NPC networking as regular customers. The `isOTCCustomer` flag is on the prefab (synced implicitly since the same prefab is spawned on all clients).

---

## Implementation Priority Order

### Phase 1 — Core Loop (makes the game playable as designed)
1. **Shift Manager** — backbone for everything else
2. **Doppelganger System** — primary gameplay mechanic
3. **Prescription Verification UI** — the decision-making interface
4. **Quota/Money System** — consequences for decisions

### Phase 2 — Night Mode (the horror half)
5. **Monster AI** — the threat
6. **Noise System** — monster's detection method
7. **Recipe & Crafting System** — the counter-threat
8. **Ingredient Spawning** — crafting materials
9. **Mortar Station** — ingredient processing
10. **Lighting & Atmosphere** — tension and pacing

### Phase 3 — Weapon Variety
11. **Crafted Weapons** (all 4 types) — night mode resolution
12. **Doppelganger Elimination Flow** — day mode consequence

### Phase 4 — Polish & Depth
13. **Question Budget** — verification skill expression
14. **Dispensing Flow** — full prescription workflow
15. **OTC Requests** — ambiguity layer
16. **Carry Limit (2 items)** — design doc fidelity

---

## Multiplayer Architecture Summary

Every new system follows the established patterns:

| Pattern | Used By | New Systems Using It |
|---|---|---|
| `NetworkVariable<int>` state sync | `NPCInteractionController`, `ShiftManager` | `MonsterController`, `ShiftManager`, `CraftingManager` |
| `NetworkVariable<ulong>` exclusive lock | `ComputerScreen`, `PillCountingStation`, `GunCase` | `MortarStation`, `CraftedWeapon`, `DispensaryCabinet` |
| ServerRpc → ClientRpc action broadcast | `ObjectPickup`, `Mop`, `GunCase` | `VerificationManager`, `CraftingManager`, `NoiseSystem` |
| Server-only AI + NetworkTransform | `NPCInteractionController` | `MonsterController` |
| `ForceReleaseLock(clientId)` disconnect cleanup | All lockable scripts | `MortarStation`, `CraftedWeapon` |
| Non-spawned local fallback | All NetworkBehaviours | All new NetworkBehaviours |

No new networking patterns are needed. The existing architecture scales directly to all missing systems.
