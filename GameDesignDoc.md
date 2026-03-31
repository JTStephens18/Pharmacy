# After Hours
*Pharmacy Horror Game — Design Document*

---

## Overview

A first-person co-op horror game (1–3 players) set in a 1990s pharmacy. During the day shift, players run the pharmacy — restocking shelves, filling prescriptions, cleaning up after customers, and identifying doppelgangers posing as real patients. Miss a doppelganger and they escape, alerting a monster that invades the pharmacy at closing time. The player must then craft a weapon from pharmacy ingredients and kill it before dawn.

The core design principle: **every mechanic used at night is the same mechanic used during the day, just under hostile conditions.** No mode-switch feeling — the same pharmacy becomes dangerous.

---

## Inspirations & References

| Game | What it contributes |
|---|---|
| Shift at Midnight | Core loop: routine job sim + horror consequence for failure. Doppelganger detection as the primary skill expression. |
| Papers, Please | Document verification as gameplay. Limited questions per customer. Decision under time pressure. |
| Darkwood | Day-prep → night-siege loop. Sound sensitivity, barricading, crafting from found ingredients. |
| Don't Starve | Shift structure: prepare during the day, survive at night. Crafting to counter specific threats. |

---

## Core Loop

```
Shift starts
    → Player finds and reads the recipe note posted somewhere in the pharmacy
    → NPCs arrive, browse, approach counter
    → Player verifies prescriptions + dispenses medication
    → Player cleans up biohazards left by browsing NPCs
    → Doppelgangers hidden among real patients
    → Player detects or misses each one
Shift ends
    → 0 doppelgangers escaped = no monster, shift complete
    → 1+ escaped = lights flicker, monster enters delivery room
    → Player gathers 3 ingredients, processes one, crafts weapon at counter
    → Uses weapon to permanently kill the monster before dawn
    → Next shift begins — harder, new recipe, new monster patrol
```

**Key timing note:** The recipe note is posted and readable during the day shift — before any monster appears. The player can find and memorize it while going about normal work. Its presence during the day is the only hint that something is coming.

---

## Narrative

### The Predecessor

The pharmacy has been here since at least the early 1970s. The previous pharmacist ran it for nearly two decades — long enough that the regulars knew her schedule, her habits, and the particular way she kept things. She was meticulous. The records she left behind are dense and careful. Long-term patients have years of fill history in the system, logged with a consistency that suggests she paid attention.

She stopped coming in on a Tuesday. No notice. No forwarding address. The register was balanced, the shelves were stocked, the open sign was still on. The pharmacy sat closed for several months before the ownership transferred. The official explanation, if asked, is a family matter. No one followed up.

The recipe notes are hers. Their tone — clinical, bureaucratic, dry — matches the handwriting visible elsewhere in the building: margin notes on old delivery logs, a second set of observations tucked behind the dispensary cabinet, a list of dates with no names on the back of a pinboard card. She was tracking something for a long time before she was gone. The notes she left are the distilled result of that work. She wrote them to be legible to whoever came next, because she knew she might not be there to explain.

What happened to her is not answered by the game directly. The regulars remember her. The pharmacy still carries her shape. Whether she left, was taken, or became something else is a question the player can sit with or ignore.

### How the Story Surfaces

The predecessor is never named, never shown, and never explained. Her presence is felt through three sources:

- **The physical space** — margin notes, a second ledger behind the dispensary cabinet, the recipe notes themselves. These are findable, not mandatory.
- **A small number of long-term regulars** who mention her offhand. These lines are written as social texture, not clues. A player not looking for a story will hear them as small talk.
- **The recipe notes**, which grow slightly more worn-looking across nights, as if some were written recently and some were not.

No more than three NPCs carry any trace of her. The lines are short, unprompted, and not repeated. The player who notices will notice. The player who doesn't will have a complete experience regardless.

---

## Phase 1 — The Day Shift

### The Player's Job

**Verify + Dispense.** Two distinct steps per prescription customer:

1. **Verification** — Pull up patient record on the computer. Cross-reference the physical script with the database: photo ID, DOB, address, prescriber NPI, fill history, dose consistency. Decide to approve or reject.
2. **Dispensing** — If approved, retrieve medication from the dispensary cabinet, count the correct dose at the pill counting station (or measure and compound at the dosing/mortar station), label the bottle, hand over and ring up.

These two steps are separated in time and space. Verification happens at the computer. Dispensing happens at the pill station, mortar, and dispensary cabinet. Moving between them while a queue builds is how mistakes happen.

### Doppelganger Detection

Each shift draws from a randomized NPC pool. A portion of those NPCs are doppelgangers — for example, 2–3 out of 7 total customers in a shift. Each doppelganger has at least one discrepancy in its profile that the player must identify during verification.

**NPC structure across shifts:**
- The NPC pool is randomized each night — different customers appear each shift
- Doppelgangers are drawn from the same pool as regular NPCs, so any NPC type could be either real or fake on any given night
- Some doppelgangers are authored — specific NPCs are scripted to appear and always be a doppelganger on a set night (e.g. a man in a suit is forced as a doppelganger and appears 4th in the queue on Night 2). These create memorable set pieces within an otherwise unpredictable system.

**Flag types, from obvious to subtle:**

| Flag | What to look for |
|---|---|
| Photo mismatch | ID photo doesn't match the NPC's appearance |
| Invalid NPI | Prescriber number not in the state database |
| No fill history | First-ever controlled substance fill — no prior records |
| Wrong prescriber specialty | Dermatologist prescribing opioid pain management |
| Dose jump | Previous fill was 0.5mg, current is 1mg — no titration note |
| Non-standard quantity | 45 tablets instead of standard 30 or 60 |
| Prescriber outside service area | Doctor's office address is 200 miles away |

**Question budget.** The player can ask each NPC a limited number of questions (suggested: 5) before the NPC gets impatient and leaves. Questions must be chosen carefully — ask about DOB, their doctor, their address, their last fill, or their symptoms. Choosing what to investigate under time pressure is the primary skill expression.

**There are no approve/reject buttons.** The player's decisions are expressed entirely through physical actions:

- **Approve** = let the NPC check out at the cash register (the existing checkout flow). The player walks to the register and rings them up.
- **Reject** = shoot the NPC with the gun. This is the only way to eliminate a suspected doppelganger.

**Consequences of decisions:**

- Correct kill (shot a doppelganger) → blood, ichor, or residue that must be cleaned up before the next customer arrives. Body must be disposed of. Doppelganger neutralized.
- Correct checkout (approved a real patient) → patient filled, quota progresses
- Wrong kill (shot a real patient) → severe money penalty, blood cleanup required, customer lost
- Wrong checkout (approved a doppelganger) → doppelganger receives medication and escapes silently → monster spawns at closing

The tension comes from uncertainty. The computer gives you information, but **you** have to decide what to do with it — and then physically do it. Walking to the register is an act of trust. Picking up the gun is an act of judgment.

The delay between a missed doppelganger and its consequence is intentional. The horror arrives at closing — not immediately.

### Cleanup

NPCs leave biohazards as they browse and interact with the pharmacy. Eliminated doppelgangers always leave a mess. Cleanup is an ongoing task throughout the shift — unaddressed messes affect customer ratings or quota.

**Cleanup sources:**
- General NPC browsing — spills, dropped items, minor messes on shelves and floor
- Eliminated doppelgangers — blood, ichor, or residue. Always requires mopping and body disposal before the next customer arrives

The mop is a primary tool during the day shift. At night, dragging or running with the mop creates noise that can be used deliberately to attract or redirect the monster.

### NPC Queue Pressure

Multiple NPCs waiting at the counter creates time pressure during verification. Rushing through a script check is how doppelgangers slip past. This is the correct failure mode — the game teaches players to slow down by punishing speed.

### The Shop

NPCs browse and purchase items alongside their prescription. Shelves deplete naturally throughout the shift.

**Shelf restocking:** Delivery boxes arrive periodically, or the player can order them through the computer terminal during the shift. To restock, the player holds a delivery box and looks at empty shelf slots — items populate the shelf as the player faces each gap. Simple, low-friction, no sorting mini-game.

**Over-the-counter requests:** Some NPCs approach the counter to ask for OTC medication — cold medicine, pain relief, antacids — without a prescription. Player retrieves from the behind-counter shelf and rings it up. Lighter than a full Rx interaction. Gives a doppelganger a plausible reason to approach the counter without triggering the verification flow — a deliberate ambiguity.

**What the shop does not include:** No inventory management screen, no expiry dates, no quality decisions. Ordering a new delivery box through the computer is the extent of the management layer.

---

## Phase 2 — Monster Mode

### Trigger Condition

Any doppelganger that escapes during the shift triggers monster mode at closing. The number of monsters scales with the number that escaped (demo: max 1). If zero escaped, the shift ends cleanly.

### The Recipe Note

The recipe note is posted somewhere visible in the pharmacy at the start of each shift — before the monster appears. The player is expected to find and read it during normal work. When the lights flicker at closing, the player should already know what they need to make.

**Note locations (rotates each night, never hidden):**
- Pinboard behind the counter
- Taped to the compounding station
- Stuck to the delivery room door

**Note tone:** Written as dry internal protocol — clinical, bureaucratic, slightly wrong. Not a horror document. The uncanniness comes from the fact that someone prepared for this in advance and left instructions without explanation.

> *"Night Protocol 3 — Compound as directed. Do not deviate."*

The notes are in the same handwriting throughout the game. They predate the player's arrival. Who wrote them, and when, is not explained.

### Ingredient System

Every recipe requires exactly three ingredients across three roles. The Base determines the weapon's effect. The Catalyst triggers or binds it. The Vessel determines delivery type.

#### Role 1 — Base (determines weapon effect)

| Ingredient | Effect | Processing needed |
|---|---|---|
| Belladonna Extract | Paralytic — freezes the entity briefly | Grinding required |
| Aconite Tincture | Alkaloid burn — caustic on contact | Ready to use |
| Silver Nitrate Solution | Scorch — leaves a visible mark | Ready to use |
| Hemlock Compound | Progressive shutdown — slows then stops | Grinding required |
| Foxglove Distillate | Cardiac disruption — fast-acting | Ready to use |
| Wormwood Oil | Hallucinogenic — disrupts navigation | Grinding required |

#### Role 2 — Catalyst (triggers or binds the effect)

| Ingredient | Effect | Processing needed |
|---|---|---|
| Activated Charcoal | Binds and concentrates the base | Grinding required |
| Ethanol 99% | Accelerant — makes base volatile | Ready to use |
| Sodium Bicarbonate | Stabilizer — safe transport in vessel | Ready to use |
| Potassium Permanganate | Oxidizer — reacts on contact with moisture | Ready to use |
| Calcium Chloride | Desiccant — exothermic reaction with bases | Grinding required |

#### Role 3 — Vessel (determines weapon delivery type)

| Ingredient | Delivery type |
|---|---|
| Sealed empty vial | Thrown projectile — shatters on impact |
| Large-bore syringe | Injectable — close contact required |
| Empty aerosol canister | Directional spray — short cone range |
| Large empty capsule | Deployed trap — triggered by entity weight |

### Crafting Flow

```
Note already found and read during the day shift
    → Lights flicker — monster enters delivery room
    → Gather 3 ingredients (scattered across shelves, crates, storage)
    → Process one ingredient at the appropriate station
        → Grinding: mortar and pestle (~6–8 rotations)
        → Measuring: dosing station (exact quantity required)
    → Bring all 3 to the counter
    → Place in correct slot order as specified in the note
    → Weapon produced
    → Use on the monster
```

**Carry limit:** Player carries a maximum of 2 items at once — same rule as the day shift. Ingredient gathering requires multiple trips through the pharmacy while the monster is active.

**Wrong order penalty:** Placing counter slots in the wrong order consumes the ingredients and produces nothing. Player must re-gather. One mistake can cost the run.

### Processing Stations

**Mortar and pestle** — Active during both phases.

- *Day use:* Compounds certain prescriptions that require grinding — a legitimate pharmacy task. Sits on the compounding bench throughout the shift as normal equipment.
- *Night use:* Grinds base and catalyst ingredients for weapon crafting. Same gesture, same station, directly hostile stakes.

Because the mortar is present during the day, it carries no special signal at night. The shift to horror is communicated by everything else — the silence, the darkness, the broken door.

**Pill counting / dosing station** — Active during both phases.

- *Day use:* Count pills into a bottle for dispensing, or measure liquid doses.
- *Night use:* Measure exact ingredient quantities for crafting.

Same gesture, same station, different stakes.

### Weapon Types & Kill Mechanics

Weapon delivery type is determined by the vessel assigned in the recipe. Each has a distinct risk profile and kill style. The vessel assigned shapes the entire feel of that night.

#### Thrown Vial — Instant kill
- Thrown at the monster, shatters on contact
- Kills immediately on a direct hit
- Miss = vial gone, no recovery, no second attempt
- **Design role:** The forgiving weapon. Easy to use, punishing to miss. Best for night 1.

#### Aerosol Spray — Two-stage progressive kill
- First spray disorients the monster: staggers, misnavigates for ~8 seconds
- Second spray applied during that window permanently kills it
- Wait too long between applications and both uses are wasted
- No health bar — kill is communicated through visible behavior change only
- **Design role:** The tension weapon. Forces two close-range encounters. Best introduced on night 2.

#### Deployed Trap — Setup kill
- Capsule placed on the floor in a doorway or chokepoint
- Monster must be lured across it — noise draws it (running, dropping items, dragging the mop)
- Triggers on proximity or weight, releases a caustic burst
- Safest kill if executed well; most likely to fail spectacularly
- **Design role:** The planning weapon. Rewards spatial knowledge of patrol routes. Best for nights 2–3.

#### Syringe — Instant kill, close contact
- Must be jabbed directly into the monster at arm's reach
- Instant kill on contact — no range, no margin
- Most dramatic kill moment of the four weapons
- **Design role:** The bravery weapon. Best on night 1 before fear is established, or as a late reward for confident players.

### Monster Behavior

- Uses the same NavMesh as daytime NPCs — patrols the paths customers walked during the shift
- Reacts to sound: running, dropping items, and opening crates all draw it toward the source
- Walking is silent. Sprinting is a calculated risk.
- Lights gradually dim as dawn approaches — in full dark the monster moves faster
- At least one crafting ingredient always spawns in or near the monster's patrol route, forcing the player into a dangerous zone at least once per night
- The mop can be used deliberately to create noise as a lure — especially useful when setting up the trap weapon

### Escalation Across Nights

| Night | Recipe complexity | Monster behavior | Ingredient placement |
|---|---|---|---|
| Night 1 | 2 ingredients, one grinding step | Slow patrol, forgiving | All ingredients on accessible shelves |
| Night 2 | 3 ingredients, grinding or measuring | Faster, all vessel types available | One ingredient in the delivery room (spawn zone) |
| Night 3+ | 3 ingredients + a processing step | Faster, lights dim sooner | One ingredient in active patrol path |

**Demo scope:** Night 1 teaches the system. Night 2 is the real test. Two nights is a complete, shippable demo arc.

**Dawn failsafe:** If the player fails to craft and use the weapon before dawn, the monster retreats. The shift technically ends — but it returns the following night faster, with a more complex recipe.

---

## Mechanics Cross-Reference

The same objects and mechanics serve both phases. No mode-switch — just changed conditions.

| Mechanic / Object | Day shift use | Night shift use |
|---|---|---|
| Mortar and pestle | Compound prescriptions requiring grinding | Grind base and catalyst ingredients for crafting |
| Pill counting / dosing station | Dispense correct dose to patient | Measure exact ingredient quantity for crafting |
| Computer terminal | Verify patient records, order deliveries | Reference recipe note, check environmental clues |
| Dispensary cabinet | Retrieve medication to fill prescriptions | Source of some crafting ingredients |
| Counter slots | Ring up customer transactions | Place ingredients in recipe order to craft weapon |
| Carry limit (2 items) | Carry items to shelve or fill prescriptions | Multiple ingredient trips under threat |
| Delivery boxes | Unpack and restock shelves | Randomized ingredient spawns in delivery room |
| Mop | Clean biohazards and doppelganger mess | Create deliberate noise to lure and reposition monster |
| NPC queue | Time pressure during verification | Absent — silence replaces queue noise (contrast is intentional) |
| ID card scanner | Verify patient photo against record | Inspect monster-related environmental clues |

---

## Recipe Randomization Rules

The recipe note is generated fresh each night using the following constraints:

**Hard rules (always enforced):**
- Never assign two ingredients that both require grinding in the same recipe — one processing step per night
- The vessel is assigned first; base and catalyst are drawn from the remaining pool
- Night 1 always uses the vial or syringe — no trap or spray on the learning run
- No repeat vessel on consecutive nights

**Soft rules (preferred, not enforced):**
- Prefer pairing a grinding base with a ready-to-use catalyst, and vice versa
- The trap vessel pairs best with bases that have visible environmental effects (charcoal burst, silver nitrate scorch)
- The syringe pairs best with fast-acting bases (Foxglove, Aconite)
- Wormwood + aerosol is a preferred combination — the two-burst mechanic works best with the disorientation effect

---

## Multiplayer Design

Supports 1–3 players. Proximity voice chat enhances tension.

**Natural role split during monster mode:**
- One player reads the recipe note and calls out ingredients
- One player gathers ingredients from shelves and delivery room
- One player monitors the monster's position and warns the others

**Natural role split during the day shift:**
- One player handles prescription verification at the computer
- One player handles dispensing at the pill station and mortar
- One or both handle restocking, cleanup, and OTC requests

No roles are locked — players coordinate naturally based on situation.

---

## Open Design Questions

- [ ] Does the monster have a visible tell before it attacks, or is contact with it an immediate consequence?
- [ ] Does the player have a health / strike system, or is any monster contact a full fail state?
- [ ] How does the quota / money system work — what does failing to meet it cost the player across shifts?
- [ ] NPC cast design: names, conditions, prescriptions, doppelganger variants, authored night appearances
- [ ] Should the monster leave any environmental hint during the day — a shadow, a sound, a wrong reflection?
- [ ] How much of the predecessor's history should be recoverable — is there an ending that depends on what the player finds?
- [ ] Does completing a successful run (zero doppelgangers escaped) ever surface a different kind of night — one where the monster appears anyway?

**Resolved:**
- [x] There is no approve/reject button. The player shoots suspected doppelgangers with the gun.
- [x] Should there be an in-world explanation for the recipe notes — who left them and why. **Resolved:** The notes were left by the previous pharmacist. Their origin is implied, not stated. No in-world explanation is given directly.