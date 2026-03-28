# NPC Creation Guide
*After Hours — Pharmacy Horror Game*

---

## How the NPC System Works

The game draws from a **fixed pool of 40–60 written NPCs**. Each NPC appears once per playthrough and is not reused. No two shifts will have the same cast.

There are **three types of entries** in the pool:

| Type | Description |
|---|---|
| **Real NPC** | A unique human character. Appears once. Has a legitimate prescription and a quirk. Never reused. |
| **Standalone Doppelganger** | A fully fabricated identity. Unique name, unique prescription, no relation to any real NPC in the pool. Exists only as a doppelganger. |
| **Doppelganger-Eligible Real NPC** | A real NPC with a fixed alternate version written. The game decides which version appears in a given instance — human or doppelganger. Same slot, swapped details. |

Doppelgangers are **not corrupted versions of real NPCs the player has already seen**. A doppelganger is either a completely fabricated character, or the alternate version of a specific real NPC that the player may or may not have encountered before.

---

## Pool Composition Guidelines

A well-balanced pool of 40–60 NPCs should roughly follow this distribution:

| Type | Suggested count | Notes |
|---|---|---|
| Real NPCs (human only) | 25–30 | The bulk of the pool. Variety of archetypes, conditions, ages. |
| Standalone Doppelgangers | 10–15 | Fabricated identities. Flagged details by design. |
| Doppelganger-Eligible Real NPCs | 8–12 | Each has two written versions — human and doppelganger. |
| Authored / Scripted NPCs | 2–4 | Guaranteed to appear on a specific night. Always a doppelganger. |

The game draws 6–8 NPCs per shift. Doppelgangers (standalone or eligible-flipped) make up roughly 2–3 of that draw.

---

## NPC Archetypes

Every NPC should feel like a plausible customer at a 1990s suburban strip mall pharmacy. The tone is **slightly quirky** — each person has one or two odd traits, but nothing cartoonish. Horror comes from the contrast between the mundane and the wrong.

Use these archetypes as a starting point, not a constraint:

- **Working professional** — in work clothes or business casual, picking up something on the way to or from a job
- **Chronic condition patient** — managing a long-term illness, familiar with the process
- **Transient / one-off** — a stranger with a plausible reason to be at this pharmacy instead of their usual one
- **Strip mall neighbor** — works nearby, pops in between tasks
- **Older local** — has been coming here for years, knows the layout
- **Young adult** — first or second time at any pharmacy, slightly uncertain

---

## Part 1 — Writing a Real NPC

Real NPCs are human. They appear once and are never reused. Their prescription is legitimate. They have a memorable quirk. Some are doppelganger-eligible and have a fixed alternate version written alongside them.

### Template

```
NAME:
AGE:
CONTEXT: (occupation, life situation, reason for being at this pharmacy)

PRESCRIPTION
  Drug and dose:
  Quantity:
  Refills:
  Prescriber name + NPI:
  Prescriber specialty:
  Condition (implied or stated):

QUIRK
  Primary behavior:
  What they buy from shelves (if anything):

DOPPELGANGER-ELIGIBLE: [ Yes / No ]
  (If yes, complete Part 3 for this NPC)

DESIGN NOTES:
  (Sensitive subject matter, false positive risk, authored night, etc.)
```

### Guidelines

**Identity**
- Age and occupation should imply a believable medical history
- The reason for being at this specific pharmacy should feel incidental, not constructed
- Avoid names that signal anything — nothing sinister, nothing heroic

**Prescription**
- Use real drug names and realistic doses
- Quantity must follow standard dispensing norms — deviations are flags, not defaults
- Prescriber specialty must match the drug type
- Every prescription needs a legitimate, internally consistent reason to exist

**Standard quantity reference:**

| Drug type | Standard qty | Red flag qty |
|---|---|---|
| Maintenance meds (statins, BP, diabetes) | 30 or 90 tablets | Any other number |
| Controlled substance | 30 tablets | 60, 90, or non-round numbers |
| Antibiotic | 20 or 21 capsules / tablets | Anything over 30 |
| PRN / as-needed medication | 30 tablets | 45 or 60 without a note |
| ER / extended-release opioid | 30 tablets | 60 or 90 |

**Quirk**
- Must be **visible or audible** during a normal interaction — not dependent on the player initiating conversation
- Must be **consistent** — the same behavior every time, even if the NPC only appears once, so it could theoretically be noticed if seen again
- Must be **mundane** — odd but not threatening
- The quirk is what the doppelganger-eligible version gets wrong. A missing quirk is a soft tell.

Good quirk examples:
- Always asks the same question
- References something ongoing in their life (parking, a family member, their job)
- Has a physical habit tied to their condition or context (moves stiffly, checks their watch, has a bandage)
- Buys the same item from the shelves every time
- Volunteers information before being asked

Bad quirk examples:
- Anything that reads as inherently suspicious
- Information the player must remember across multiple visits to notice

---

## Part 2 — Writing a Standalone Doppelganger

Standalone doppelgangers are fabricated identities — they have no corresponding real NPC in the pool. They are built to fail verification. Their name, prescription, and backstory are invented, and at least one detail is wrong enough to catch.

### Template

```
NAME:
AGE:
CONTEXT: (stated occupation or reason for visit — plausible on the surface)

FABRICATED PRESCRIPTION
  Drug and dose:
  Quantity:
  Prescriber name + NPI:
  Prescriber specialty:
  Stated condition:

FLAGS
  Hard flag(s): (verifiable prescription discrepancy)
  Soft tell(s): (behavioral oddity — something feels slightly off)
  Total flag count:
  Flag difficulty: [ Easy / Medium / Subtle ]

BEHAVIOR
  How they present at the counter:
  What makes them slightly wrong without being obviously monstrous:

AFTERMATH
  What the cleanup looks like if shot:

DESIGN NOTES:
```

### Guidelines

**Fabricated identity**
- Should feel like a real person at first glance — plausible name, age, stated reason for visit
- The stated context should explain why there's no prior history in the system
- Avoid making them obviously creepy — the wrongness should be in the paperwork, not the face

**Flags**
- Every standalone doppelganger needs **at least one hard flag** — a verifiable discrepancy the player can check against the database
- Hard flags come from the prescription: invalid NPI, missing records, wrong prescriber specialty, non-standard quantity, prescriber outside service area
- Soft tells are behavioral — the NPC presents without the warmth or specificity a real person would have. They answer questions correctly but not quite naturally.

**Hard flag types:**

| Flag | How it appears | Difficulty |
|---|---|---|
| Photo mismatch | ID photo doesn't match NPC appearance | Easy |
| Invalid NPI | Prescriber not found in state database | Easy–Medium |
| No fill history | No prior records, claiming to be a local | Medium |
| Wrong prescriber specialty | Drug outside prescriber's stated scope | Medium |
| Dose anomaly | Unusually high dose for stated condition | Medium–Subtle |
| Non-standard quantity | Unusual tablet count, no clinical note | Subtle |
| Prescriber outside service area | Doctor's address implausibly far away | Subtle |
| Self-prescribing | Physician prescribing to immediate family | Easy (tutorial use) |

**Flag difficulty scale:**

| Difficulty | Description | Best used on |
|---|---|---|
| Easy | Single obvious flag, may surface automatically | Night 1, tutorial |
| Medium | One or two flags requiring active cross-reference | Night 1–2 |
| Subtle | Only visible against known norms or careful reading | Night 2–3+ |

**Behavior**
- Standalone doppelgangers don't have a real version to deviate from, so their tell is in the texture of their behavior — slightly too composed, slightly too vague, answers questions without the personal detail a real person would include
- They should not be obviously alien or threatening at the counter. The horror is in the paperwork.

**Aftermath**
- What they leave behind when shot can vary — blood is the default, but authored or late-game doppelgangers can leave something different. Note it explicitly.

---

## Part 3 — Writing a Doppelganger-Eligible Alternate Version

When a real NPC is marked doppelganger-eligible, a fixed alternate version is written for them. The game decides which version appears in any given instance. The player may encounter either — they will never know in advance which one is coming.

This section is written **as an addendum to the real NPC's entry** — not as a separate character.

### Template

```
DOPPELGANGER VERSION OF: [NPC name]

SWAPPED PRESCRIPTION DETAILS
  What changed and why it's a flag:

WHAT THE QUIRK GETS WRONG
  Missing behavior:
  Or: distorted behavior:

ADDITIONAL FLAGS (if any):

FLAG DIFFICULTY: [ Easy / Medium / Subtle ]

AFTERMATH:

DESIGN NOTES:
  (e.g. "Do not assign this version until the human version has appeared at least once in this playthrough")
```

### Guidelines

- **At least one prescription detail must change** — dose, quantity, prescriber, or drug
- **The quirk must be missing or slightly wrong** — this is the soft tell that rewards players who paid attention when the real version appeared
- The doppelganger version should feel like a person who did their homework on who they're impersonating but missed one or two details
- If the real version appears after the doppelganger version in the same playthrough, the player will experience it in reverse — that's fine and interesting
- Flag the design note if this version should not appear until after the real version has been seen at least once

---

## Part 4 — Authored / Scripted NPCs

Authored NPCs are guaranteed to appear on a specific night at a specific position in the queue. They are always doppelgangers. They exist to create memorable set pieces and to teach or test the player at a controlled moment.

### Template

```
NAME:
NIGHT: (which night they appear)
QUEUE POSITION: (where in the shift queue)

FABRICATED PRESCRIPTION
  Drug and dose:
  Quantity:
  Prescriber name + NPI:
  Prescriber specialty:

FLAGS
  Hard flag(s):
  Soft tell(s):
  Flag count:
  Flag difficulty:

BEHAVIOR
  How they present — what makes them feel authored rather than random:

AFTERMATH
  What cleanup looks like — can deviate from standard blood/mess:

NARRATIVE PURPOSE
  What this NPC teaches or establishes in the game's arc:

DESIGN NOTES:
```

### Guidelines

- Night 1 authored NPCs must be **easy** — single auto-surfaced flag, friendly demeanor, no ambiguity. They teach the system, not the fear.
- Night 2+ authored NPCs can carry **multiple simultaneous flags** and more unsettling behavior
- The aftermath of shooting an authored NPC is a narrative beat — use it deliberately
- No more than one authored NPC per night
- Authored NPCs should feel slightly different in texture from pool NPCs — a little too still, a little too prepared — without being obviously wrong before the player checks the paperwork

---

## Completed Examples

---

### Example A — Real NPC (no doppelganger version)

**NAME:** Derek Solis
**AGE:** 29
**CONTEXT:** Courier. Still in his uniform. Says he pulled a muscle on a delivery and stopped at the urgent care clinic around the corner.

**PRESCRIPTION**
- Drug and dose: Naproxen 500mg
- Quantity: 20 tablets
- Refills: 0
- Prescriber: Dr. Reyes, urgent care clinic — NPI 5544332211
- Prescriber specialty: General / urgent care
- Condition: Acute muscle strain

**QUIRK**
- Primary: In a hurry but not rude about it. Asks if the pharmacy validates parking.
- Shelf purchase: Nothing — he's on the clock.

**DOPPELGANGER-ELIGIBLE:** No

**DESIGN NOTES:** Derek is a clean, low-stakes NPC. No flags, no drama. His function is to fill out the shift with a believable one-off customer and give the player a fast, frictionless interaction between harder ones.

---

### Example B — Real NPC (doppelganger-eligible)

**NAME:** Pat Nguyen
**AGE:** 61
**CONTEXT:** Works at the dry cleaner two doors down. Comes in on her lunch break. Has been a customer here for years.

**PRESCRIPTION**
- Drug and dose: Atorvastatin 20mg
- Quantity: 30 tablets
- Refills: 5 remaining
- Prescriber: Dr. Carol Vance, GP — NPI 1234567890
- Prescriber specialty: General practice
- Condition: High cholesterol, managed

**QUIRK**
- Primary: Always mentions something happening at the strip mall — new tenant moving in, a restaurant that closed, someone parked in the fire lane again. Low-key gossip.
- Secondary: Always in a hurry, never rude about it.
- Shelf purchase: A single bottle of water.

**DOPPELGANGER-ELIGIBLE:** Yes

---

**DOPPELGANGER VERSION OF: Pat Nguyen**

**SWAPPED PRESCRIPTION DETAILS**
- Prescriber changed to Dr. Alan Park — a different practice with no transfer note on file
- Flag: Prescriber change without documented reason, different practice group

**WHAT THE QUIRK GETS WRONG**
- Missing: No mention of anything happening at the strip mall. Doesn't seem to know or care about the neighborhood.
- Missing: Not in a hurry. Lingers slightly longer than necessary.

**ADDITIONAL FLAGS:** None

**FLAG DIFFICULTY:** Medium — prescriber change requires active cross-reference; behavioral tell requires the player to have seen real Pat at least once

**AFTERMATH:** Standard mess.

**DESIGN NOTES:** Do not assign the doppelganger version until the human version has appeared at least once in this playthrough. The behavioral tell only lands if the player has a baseline. If the doppelganger appears first, the prescriber flag alone is medium-difficulty — still catchable, just less satisfying.

---

### Example C — Standalone Doppelganger

**NAME:** Victor Crane
**AGE:** 47
**CONTEXT:** Claims to be in town for a job interview. Says his regular pharmacy is across the state.

**FABRICATED PRESCRIPTION**
- Drug and dose: Oxycodone HCl 10mg
- Quantity: 60 tablets
- Prescriber: Dr. Alan Park — NPI 9988776655
- Prescriber specialty: NPI resolves to a retired physician — no longer in practice
- Stated condition: Chronic back pain from an old work injury

**FLAGS**
- Hard flag 1: NPI resolves to a retired physician — no longer licensed to prescribe
- Hard flag 2: Quantity 60 is double the standard 30-day supply for this drug class
- Soft tell: Answers every question without hesitation. Too composed. Doesn't fidget, doesn't check his phone, doesn't look around. Sits completely still while waiting.
- Total flag count: 2 hard + 1 soft
- Flag difficulty: Medium — both hard flags require active checking, soft tell is atmospheric

**BEHAVIOR:** Polite, unhurried, makes eye contact consistently. Has a rehearsed quality without being robotic. If asked about his back injury, gives a complete and plausible answer that somehow contains no personal detail.

**AFTERMATH:** Blood, but the wrong color. Dark, almost black. Leaves a mark on the floor that doesn't come fully clean.

**DESIGN NOTES:** Victor is a mid-pool standalone doppelganger suitable for nights 2–3. The blood color is a late-shift environmental detail that unsettles without explaining anything.

---

### Example D — Authored NPC

**NAME:** Karen Holt
**NIGHT:** 1
**QUEUE POSITION:** 2 (second customer of the shift)

**FABRICATED PRESCRIPTION**
- Drug and dose: Alprazolam 0.5mg
- Quantity: 30 tablets
- Prescriber: Dr. James Holt — the patient's stated husband
- Prescriber specialty: GP

**FLAGS**
- Hard flag: A physician cannot legally prescribe controlled substances to an immediate family member — surfaced automatically by the computer without cross-referencing
- Soft tell: None needed — flag is automatic and unambiguous
- Total flag count: 1 hard (auto-surfaced)
- Flag difficulty: Easy

**BEHAVIOR:** Friendly and chatty. Volunteers the explanation before being asked — "James said it would be fine." Refers to the prescriber by first name. Seems mildly embarrassed but entirely reasonable. Nothing about her behavior is threatening.

**AFTERMATH:** Standard mess. The next customer in queue watches it happen and says nothing.

**NARRATIVE PURPOSE:** Teaches the player how the auto-flag system works on night 1. The flag is unambiguous so the decision is easy, but Karen's warmth makes the player feel slightly bad about it — establishing that correct decisions can still feel uncomfortable. The witness in line sets the tone for the rest of the game without a word of exposition.

**DESIGN NOTES:** Must appear second in the queue on night 1. Early enough to function as a tutorial, late enough that the player has settled into one normal interaction first. Her friendliness is load-bearing — a hostile or suspicious first authored doppelganger would teach the player to look for hostility. Karen teaches them to look at the paperwork.

---

## Quick Reference Checklist

Before submitting any NPC entry, confirm:

**Real NPC**
- [ ] Identity is grounded — plausible person, plausible reason to be at this pharmacy
- [ ] Prescription is internally consistent — drug, dose, quantity, and prescriber all fit together
- [ ] Prescriber specialty matches the drug type
- [ ] Quantity follows standard norms
- [ ] At least one quirk that is visible, consistent, and mundane
- [ ] If doppelganger-eligible, Part 3 is completed
- [ ] Design notes flag any sensitive subject matter or false positive risk

**Standalone Doppelganger**
- [ ] Identity is plausible on the surface
- [ ] At least one hard flag — verifiable prescription discrepancy
- [ ] At least one soft tell — behavioral oddity
- [ ] Flag difficulty is appropriate for intended night placement
- [ ] Aftermath is documented

**Doppelganger-Eligible Alternate Version**
- [ ] At least one prescription detail is changed and the change is a clear flag
- [ ] The quirk is missing or distorted
- [ ] Design note states whether this version requires the human version to have appeared first

**Authored NPC**
- [ ] Night number and queue position are specified
- [ ] Flag difficulty matches the night (easy on night 1)
- [ ] Aftermath is documented and used as a narrative beat
- [ ] Narrative purpose is clearly stated