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

## Lore Integration

### The Predecessor

The pharmacy has a history that predates the player's arrival. The previous pharmacist ran the place for nearly two decades before disappearing without explanation. She left the building in order, the records dense and careful, and the recipe notes posted where whoever came next would find them.

The player is never told this directly. It surfaces, if at all, through a small number of long-term regulars who knew her — Marlene, Carl, and Pat — and through one anonymous customer who appears once and is not explained.

### Rules for Lore Dialogue

- No more than three NPCs in the entire pool carry any reference to the predecessor.
- Each reference appears at most once per NPC, unprompted, and is never repeated.
- The lines are written as social texture — small talk, not clues. A player not looking for a story will hear them as mundane.
- No NPC explains what happened, speculates about why she left, or treats her absence as strange. They simply remember her.
- The anonymous one-off woman (NPC 12) is the only entry that crosses into the uncanny. She appears once and does not return.

The goal is that a player paying close attention will feel the predecessor's absence as a texture across the playthrough — not as a mystery to solve, unless they choose to make it one.

---

## Dialogue File Structure

Each NPC has four dialogue JSON files. They serve different purposes and follow different rules.

| File | Key | Trigger | Player responses? |
|---|---|---|---|
| `npc_customer_XX.json` | *(none)* | Auto-triggers when NPC reaches counter | **No** |
| `npc_customer_XX_info.json` | `"default"` | Player clicks a button on the NPC info panel | Yes |
| `npc_customer_XX_dob.json` | `"dob"` | Player clicks a button on the NPC info panel | Yes |
| `npc_customer_XX_address.json` | `"address"` | Player clicks a button on the NPC info panel | Yes |

### Main dialogue — NPC monologue only

The main file (`npc_customer_XX.json`) is an **NPC-only monologue**. It contains no player response branches. All nodes must have an empty `responses` array, making them terminal — the player sees a [Continue] prompt and the dialogue closes.

This file exists to deliver character texture: the quirk, the reason for the visit, the tone of the person. The player cannot interrogate the NPC here. Investigation happens entirely through the computer screen and info panel buttons.

**Rules:**
- No `responses` entries in any node
- One to three nodes maximum — keep it brief
- The quirk must appear here. This is its only guaranteed delivery point.
- The NPC's stuff is already on the counter before this triggers. Do not write them "presenting" their ID or prescription — that already happened.

### Verification dialogues — player-initiated

The info, DOB, and address files are triggered by the player clicking buttons on the NPC info panel (after scanning the ID card). These files **may** have player response branches. They are the only place where the player can ask questions.

---

## NPC Profile Files

Every NPC must have a profile JSON file at `Assets/Data/NPCProfiles/npc_customer_XX_profile.json`. This file is the single source of truth for all structured data about an NPC — it is what you fill out to create the Unity ScriptableObject assets and what you check dialogue against for accuracy.

Create this file **before** writing dialogue or creating any Unity assets.

### Schema

```json
{
    "npcId": "npc_customer_XX",
    "npcNumber": 0,
    "type": "real",
    "doppelgangerEligible": false,

    "identity": {
        "fullName": "",
        "dateOfBirth": "MM/DD/YYYY",
        "address": "",
        "idNumber": "",
        "photoSprite": "",
        "idCardName": "",
        "idCardPhotoSprite": ""
    },

    "prescription": {
        "medicationName": "",
        "quantity": 0,
        "dosage": "",
        "prescriberName": "",
        "prescriberNPI": "",
        "prescriberSpecialty": "",
        "prescriberAddress": "",
        "previousFills": []
    },

    "quirk": {
        "behavior": "",
        "shelfPurchase": ""
    }
}
```

**`type` values**: `"real"` · `"standalone_doppelganger"` · `"authored"`

**`photoSprite` / `idCardPhotoSprite`**: Leave empty until art exists. Fill in the sprite asset name when assigned.

**`idCardName` / `idCardPhotoSprite`**: Only needed if the physical ID card should show different data than the computer screen. Leave empty to inherit from `fullName` / `photoSprite`.

---

### For doppelganger-eligible real NPCs

Add a `doppelgangerVersion` block after `quirk`:

```json
"doppelgangerVersion": {
    "swappedFields": {
        "prescriberName": "",
        "prescriberNPI": "",
        "prescriberSpecialty": "",
        "dosage": "",
        "quantity": 0,
        "dateOfBirth": "",
        "address": "",
        "photoSprite": ""
    },
    "discrepancies": [],
    "quirkDeviations": "",
    "flagDifficulty": "Medium",
    "designNote": ""
}
```

Only fill in the fields that actually change — leave others empty. `discrepancies` maps directly to the `DoppelgangerProfile.discrepancies` array in Unity (valid values: `PhotoMismatch`, `InvalidNPI`, `NoFillHistory`, `WrongPrescriberSpecialty`, `DoseJump`, `NonStandardQuantity`, `PrescriberOutsideArea`, `WrongDOB`, `WrongAddress`).

---

### For standalone doppelgangers

The `identity` and `prescription` blocks contain the fabricated data. Add a `flags` block instead of `doppelgangerVersion`:

```json
"flags": {
    "hardFlags": [],
    "softTells": [],
    "flagDifficulty": "Medium",
    "aftermath": ""
}
```

---

### For authored NPCs

Add an `authored` block:

```json
"authored": {
    "night": 1,
    "queuePosition": "3-4",
    "narrativePurpose": ""
}
```

---

See `Assets/Data/NPCProfiles/npc_customer_01_profile.json` for a complete real NPC example.

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

LORE LINE: [ None / Yes — state the line ]
  (Used by at most 3 NPCs total. Short, unprompted, not repeated.)

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

## Quick Reference Checklist

Before submitting any NPC entry, confirm:

**Real NPC**
- [ ] Identity is grounded — plausible person, plausible reason to be at this pharmacy
- [ ] Prescription is internally consistent — drug, dose, quantity, and prescriber all fit together
- [ ] Prescriber specialty matches the drug type
- [ ] Quantity follows standard norms
- [ ] At least one quirk that is visible, consistent, and mundane
- [ ] If doppelganger-eligible, Part 3 is completed
- [ ] If carrying a lore line, it is short, unprompted, and does not repeat. Confirm total lore-carrying NPCs does not exceed 3.
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

---

## NPC Roster — Entries 01–12

---

### NPC 01 — Derek Solis

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

**LORE LINE:** None

**DESIGN NOTES:** Derek is a clean, low-stakes NPC. No flags, no drama. His function is to fill out the shift with a believable one-off customer and give the player a fast, frictionless interaction between harder ones.

---

### NPC 02 — Marlene Hoffman

**NAME:** Marlene Hoffman
**AGE:** 67
**CONTEXT:** Retired school librarian. Monthly regular for 22 years. Comes in mid-morning on weekdays. Has never once been in a hurry.

**PRESCRIPTION**
- Drug and dose: Amlodipine 5mg
- Quantity: 30 tablets
- Refills: Ongoing maintenance
- Prescriber: Dr. Ruth Ellison, GP — NPI 2345678901
- Prescriber specialty: General practice
- Condition: Hypertension, long-managed

**QUIRK**
- Primary: Always asks if you're new, with a kind apology — "I'm terrible with faces, you'll have to forgive me." Wistfully mentions it used to be better when pharmacists knew their customers by name.
- Shelf purchase: A bag of hard candy from the same spot on the same shelf every time.

**DOPPELGANGER-ELIGIBLE:** No

**LORE LINE:** Yes. On her second or third appearance, after her usual comment about faces: *"The woman before you — she always remembered what I took. You'll get there."* Said warmly, without expectation. She does not elaborate and does not repeat it.

**DESIGN NOTES:** Marlene is a low-stakes warmth anchor. Her lore line is genuinely kind — it reads as encouragement, not as a clue. A player not thinking about it will hear reassurance. A player paying attention will notice that there was someone here before them who left an impression on a 22-year regular. The line does not appear on her first visit — it needs the relationship to feel established first.

---

### NPC 03 — Tommy Briggs

**NAME:** Tommy Briggs
**AGE:** 34
**CONTEXT:** Auto mechanic. Came straight from a job. Grease on his knuckles, work shirt still on. Stopped in on the way home after getting caught in traffic.

**PRESCRIPTION**
- Drug and dose: Lisinopril 10mg
- Quantity: 30 tablets
- Refills: 3 remaining
- Prescriber: Dr. Marcus Webb, GP — NPI 3456789012
- Prescriber specialty: General practice
- Condition: Hypertension, early management

**QUIRK**
- Primary: Holds up both hands immediately when he reaches the counter — "Sorry, I know, I meant to clean up first. Traffic was murder out there today." Sheepish about it but not embarrassed enough to leave.
- Shelf purchase: An energy drink from the refrigerated section near the door.

**DOPPELGANGER-ELIGIBLE:** Yes

---

**DOPPELGANGER VERSION OF: Tommy Briggs**

**SWAPPED PRESCRIPTION DETAILS**
- Prescriber changed from Dr. Marcus Webb (NPI 3456789012) to Dr. Ellen Cord, GP (NPI 4477889900) — no transfer note, no documented reason for switch

**WHAT THE QUIRK GETS WRONG**
- Missing: Does not hold up his hands or apologize for the grease
- Missing: No mention of traffic or being late
- Missing: Does not buy the energy drink
- Present: The grease is still there — the surface detail is correct. What's absent is the social reflex a real person would have.

**ADDITIONAL FLAGS:** None — the prescriber flag alone is sufficient

**FLAG DIFFICULTY:** Medium — prescriber change requires active cross-reference; behavioral tell requires having seen the real Tommy at least once

**AFTERMATH:** Standard.

**DESIGN NOTES:** Do not assign the doppelganger version until the human version has appeared at least once in this playthrough. The tell is specifically in the missing apology — a person impersonating Tommy knows about the grease (it's visible) but didn't rehearse the embarrassment.

**LORE LINE:** None

---

### NPC 04 — Diane Yoo

**NAME:** Diane Yoo
**AGE:** 52
**CONTEXT:** Works at the nail salon in the strip mall, two doors down from the pharmacy. Came in on a slow afternoon between appointments.

**PRESCRIPTION**
- Drug and dose: Levothyroxine 50mcg
- Quantity: 30 tablets
- Refills: Ongoing maintenance
- Prescriber: Dr. Fiona Blake, Endocrinology — NPI 5678901234
- Prescriber specialty: Endocrinology
- Condition: Hypothyroidism, managed

**QUIRK**
- Primary: On her phone texting the entire time. Puts it face-down on the counter for exactly as long as it takes to sign, then immediately picks it back up.
- Shelf purchase: A box of nail files — doesn't look at the shelf, reaches directly for them, knows exactly where they are.

**DOPPELGANGER-ELIGIBLE:** No

**LORE LINE:** None

**DESIGN NOTES:** Diane is a strip mall neighbor — she knows the layout, doesn't need to browse, and is only half-present. Her distraction is entirely mundane. False positive risk: a player unfamiliar with nail salon workers might read her as evasive. She is not. Her prescription and prescriber are fully appropriate (Endocrinology for hypothyroidism).

---

### NPC 05 — Ray Colton *(Standalone Doppelganger)*

**NAME:** Ray Colton
**AGE:** 38
**CONTEXT:** Claims to be passing through the area on the way to a new job. Says his regular pharmacy is three states away and he needs a one-time fill. Has a printed prescription from an out-of-area doctor.

**FABRICATED PRESCRIPTION**
- Drug and dose: Oxycodone HCl 10mg
- Quantity: 60 tablets
- Prescriber: Dr. Gail Rosen — NPI 6677889900
- Prescriber specialty: NPI resolves to a retired/delisted physician — no longer licensed to prescribe
- Stated condition: Chronic back pain from a workplace injury, "ongoing for two years"

**FLAGS**
- Hard flag 1: NPI resolves to a delisted prescriber — no longer authorized to issue prescriptions
- Hard flag 2: Quantity 60 is double the standard 30-day supply for this drug class
- Soft tell: Completely still at the counter. Doesn't fidget, doesn't look at the shelves, doesn't check his phone. Makes consistent, unblinking eye contact. Answers every question with the correct information and no additional texture.
- Total flag count: 2 hard + 1 soft
- Flag difficulty: Medium — both hard flags require active database checking; soft tell is atmospheric

**BEHAVIOR:** Polite, patient, unhurried. His composure reads as confidence at first glance. If asked about the injury, he gives a medically plausible account — date, mechanism, treatment history — that contains no personal detail whatsoever. The information is correct. The person behind it is not there.

**AFTERMATH:** The prescription printout, left on the counter, lists a delivery address that does not correspond to any building at that street number. The street exists. The address does not.

**DESIGN NOTES:** Ray is a mid-pool standalone doppelganger suitable for nights 1–2. His two hard flags are both findable by cross-referencing the computer database. The non-existent address is an environmental detail discovered after the fact — it does not affect the decision window. Do not telegraph the aftermath before the player acts.

---

### NPC 06 — Sandra Kowalski

**NAME:** Sandra Kowalski
**AGE:** 44
**CONTEXT:** Elementary school teacher at the school four blocks away. End-of-semester crunch. Came in after dismissal, still has her teacher's ID lanyard on.

**PRESCRIPTION**
- Drug and dose: Metformin 500mg
- Quantity: 90 tablets
- Refills: 2 remaining
- Prescriber: Dr. Ruth Ellison, GP — NPI 2345678901
- Prescriber specialty: General practice
- Condition: Type 2 diabetes, early management

**QUIRK**
- Primary: Constantly references school. Mentions grading, one of her kids said something funny, how she hasn't slept properly in a week.
- Secondary: Her tote bag is stuffed to capacity with papers. Some slide onto the counter when she sets it down. She grabs them with mild, practiced embarrassment — "Sorry, sorry, they're everywhere."
- Shelf purchase: Ibuprofen. "For the headaches."

**DOPPELGANGER-ELIGIBLE:** Yes

---

**DOPPELGANGER VERSION OF: Sandra Kowalski**

**SWAPPED PRESCRIPTION DETAILS**
- Prescriber changed from Dr. Ruth Ellison (NPI 2345678901) to Dr. Steven Hale, GP (NPI 7788990011) — no transfer note, no documented reason for switch

**WHAT THE QUIRK GETS WRONG**
- Missing: No mention of school, grading, the kids, or being tired
- Missing: The tote bag is present but sits flat on the counter. Nothing slides out.
- Missing: Does not buy ibuprofen
- Present: The lanyard is still there. She has the prop but not the person.

**ADDITIONAL FLAGS:** None

**FLAG DIFFICULTY:** Medium — prescriber flag is the hard tell; behavioral flag requires having seen the real Sandra at least once

**AFTERMATH:** Standard.

**DESIGN NOTES:** Do not assign the doppelganger version until the human version has appeared at least once in this playthrough. The empty tote bag is the key visual tell — Sandra's bag is always in motion because she's always just come from somewhere. A flat bag means she packed it to look the part.

**LORE LINE:** None

---

### NPC 07 — Theo Marsh

**NAME:** Theo Marsh
**AGE:** 23
**CONTEXT:** College student. Just came from urgent care — has the patient wristband still on and a copy of the urgent care discharge summary in his pocket. Got strep throat and is visibly uncomfortable.

**PRESCRIPTION**
- Drug and dose: Amoxicillin 500mg
- Quantity: 21 capsules
- Refills: 0
- Prescriber: Dr. Reyes, urgent care — NPI 5544332211
- Prescriber specialty: General / urgent care
- Condition: Streptococcal pharyngitis, acute

**QUIRK**
- Primary: Full of questions. Side effects? Can he take it with food? Does he need to finish the whole course even if he feels better? Has already read the printout but wants confirmation.
- Secondary: Mentions his roommate had the exact same prescription last month. "Is that weird? Is strep going around or something?"
- Shelf purchase: Nothing. He's already spent money at urgent care and is managing on a budget.

**DOPPELGANGER-ELIGIBLE:** No

**LORE LINE:** None

**DESIGN NOTES:** Theo is the anxious first-timer archetype. His questions are genuine and slightly exhausting. His prescription is completely standard — urgent care physician, correct drug and quantity, appropriate refill count (zero, because it's an acute infection). False positive risk: his anxiety might read as nervousness. It isn't. He's just unwell and underprepared for adulthood.

---

### NPC 08 — Ellen Voss *(Standalone Doppelganger)*

**NAME:** Ellen Voss
**AGE:** 55
**CONTEXT:** Claims to be a local resident who has been meaning to switch pharmacies for a while. Says she usually goes to the chain across town but heard good things about this one. Has no prior records here.

**FABRICATED PRESCRIPTION**
- Drug and dose: Hydrocodone/Acetaminophen 5/325mg
- Quantity: 30 tablets
- Prescriber: Dr. Paul Manders, DDS — NPI 7899001234
- Prescriber specialty: Dentistry
- Stated condition: Chronic knee pain following an old surgery, "long-term"

**FLAGS**
- Hard flag 1: Prescriber specialty mismatch — a dentist has no scope to prescribe for chronic orthopedic pain management
- Hard flag 2: No fill history despite claiming to be a long-term local patient on ongoing pain medication
- Soft tell: Overly polite, slightly stiff. Never looks at the shelves. Doesn't make small talk. Maintains a careful stillness that reads as good manners until it doesn't.
- Total flag count: 2 hard + 1 soft
- Flag difficulty: Medium — specialty mismatch is checkable; no-fill-history flag requires knowing that chronic pain patients have dense records

**BEHAVIOR:** Well-dressed, pleasant, measured. She volunteers nothing unexpected. If asked how she heard about the pharmacy, she gives a vague but plausible answer. If asked about the knee, she describes the surgery in general terms — nothing that couldn't be lifted from a pamphlet.

**AFTERMATH:** When she steps back from the counter, a business card falls from her purse. The name on it does not match her ID. The card is from a medical supply company.

**DESIGN NOTES:** Ellen is a mid-to-late pool standalone doppelganger appropriate for nights 2–3. The business card aftermath is a narrative detail that lands after the decision point. The specialty mismatch is the primary hard flag and is findable by cross-referencing the NPI database against the stated condition.

---

### NPC 09 — Carl Dunbar

**NAME:** Carl Dunbar
**AGE:** 71
**CONTEXT:** Retired postal carrier. Tuesday regular for 15+ years. Came in at his usual time.

**PRESCRIPTION**
- Drug and dose: Warfarin 5mg
- Quantity: 30 tablets
- Refills: Ongoing maintenance
- Prescriber: Dr. Ruth Ellison, GP — NPI 2345678901
- Prescriber specialty: General practice
- Condition: Atrial fibrillation, anticoagulation management

**QUIRK**
- Primary: Always comments on the weather, even when there's nothing to say. "Cold out today." "Nice enough, I suppose." A ritual more than an observation.
- Secondary: Briefly complains about how mail runs work now compared to when he did the route. Has a specific grievance each time — different grievance, same energy.
- Shelf purchase: A newspaper or a TV Guide, whichever is closer to the door.

**DOPPELGANGER-ELIGIBLE:** Yes

---

**DOPPELGANGER VERSION OF: Carl Dunbar**

**SWAPPED PRESCRIPTION DETAILS**
- Prescriber changed from Dr. Ruth Ellison (NPI 2345678901) to Dr. Marcus Webb, GP (NPI 3456789012) — no transfer note
- Fill history for warfarin is missing — a patient on long-term anticoagulation for atrial fibrillation would have years of continuous, closely monitored fill records

**WHAT THE QUIRK GETS WRONG**
- Missing: No weather comment
- Missing: No complaint about the mail
- Missing: Does not buy a newspaper
- Present: Says he "stops in sometimes" when asked if he's a regular — doesn't claim the 15-year history, just doesn't deny being familiar with the place

**ADDITIONAL FLAGS:** Missing fill history is the secondary hard flag — requires knowing that warfarin patients need continuous INR monitoring and refills

**FLAG DIFFICULTY:** Medium-Subtle — prescriber change is the primary flag; missing fill history requires the player to know that anticoagulation patients don't have gaps in their records; behavioral tell requires having seen the real Carl first

**AFTERMATH:** Standard.

**DESIGN NOTES:** Do not assign the doppelganger version until the human version has appeared at least once in this playthrough. Carl's doppelganger is the hardest of the doppelganger-eligible alternates in this batch — appropriate for later nights.

---

**LORE LINE:** Yes. On his first or second visit, after paying, while picking up his newspaper: *"Glad someone's keeping the place."* He doesn't look up when he says it. It's not a compliment directed at the player specifically — it's closer to relief. He does not explain it and does not repeat it.

**LORE DESIGN NOTES:** Carl's line is the most ambiguous of the three. It could read as a comment about the pharmacy closing and reopening — a mundane relief that the local pharmacy is still running. A player not looking for subtext will hear exactly that. The line lands differently after the player has found the pinboard note or the ledger, but it does not require that context to be believable.

---

### NPC 10 — Becca Stinson

**NAME:** Becca Stinson
**AGE:** 28
**CONTEXT:** Registered nurse at the hospital three blocks away. Just finished a shift. Still in scrubs, badge clipped to her chest. Stopped in on the way to her car.

**PRESCRIPTION**
- Drug and dose: Sertraline 50mg
- Quantity: 30 tablets
- Refills: 5 remaining
- Prescriber: Dr. Helen Okafor, Psychiatry — NPI 8899001122
- Prescriber specialty: Psychiatry
- Condition: Depression / anxiety, managed

**QUIRK**
- Primary: Efficient, not rude. Presents everything before being asked. If there's any hesitation on the player's part, she says "I know how this works" — gently, not unkindly. She's filled prescriptions for patients all day. She knows the drill.
- Shelf purchase: Nothing. She has a bag over one shoulder and she's going home.

**DOPPELGANGER-ELIGIBLE:** No

**LORE LINE:** None

**DESIGN NOTES:** Becca is a deliberate false positive bait. Her prescription (sertraline from a psychiatrist) is entirely appropriate — this is exactly what that drug class looks like on a legitimate prescription. Her impatience is occupational, not suspicious. The player who flags her because she seems "too confident" or because they're uncomfortable with the medication type has made an error. Design intent: teach the player to check the paperwork, not the affect.

---

### NPC 11 — Frank Delaney *(Authored, Night 1)*

**NAME:** Frank Delaney
**NIGHT:** 1
**QUEUE POSITION:** 3–4 (third or fourth customer of the first shift)

**FABRICATED PRESCRIPTION**
- Drug and dose: Hydrocodone/Acetaminophen 10/325mg
- Quantity: 30 tablets
- Prescriber: Dr. Frank Delaney — NPI 9900112233
- Prescriber specialty: General practice (his own active NPI — the prescriber and patient names are identical)

**FLAGS**
- Hard flag: Physician self-prescribing a Schedule II controlled substance — auto-surfaced by the computer without requiring cross-reference. The prescriber name matches the patient name.
- Soft tell: Warm, confident, over-prepared. Volunteers the explanation before the player has looked anything up. Uses the word "documented" twice. His white coat is folded over the counter.
- Total flag count: 1 hard (auto-surfaced) + 1 soft
- Flag difficulty: Easy — auto-surfaced, no active checking required

**BEHAVIOR:** Friendly and slightly self-deprecating. "I know how it looks — believe me, I do." Explains that his usual prescribing partner is on vacation, that he has documentation, that this is not his habit, and that he would never do this with anything that mattered. Everything is "documented." He is the most reasonable-sounding person you've talked to all night.

**AFTERMATH:** His white coat stays folded on the counter after. It is the only thing left. There is no name embroidered on it.

**NARRATIVE PURPOSE:** Frank is the second authored tutorial NPC on night 1 (Karen Holt being the first). Where Karen teaches that a warm demeanor is not verification, Frank teaches the corollary: credentials and a good explanation are also not verification. He is a doctor. He sounds right. The paperwork says no. His aftermath — the coat with no name — is the first image in the game that does not have an explanation, and is not meant to.

**DESIGN NOTES:** Must appear in queue position 3 or 4 on night 1. Late enough that the player has processed Karen and one or two normal NPCs first. Frank's warmth must not feel like a trick — it is genuine. He believes his explanation. The game is not asking the player to decide if Frank is a bad person. It is asking them to read the flag on the screen and act accordingly. The nameless coat is a deliberate loose thread.

---

### NPC 12 — [No Name Given]

**TYPE:** Real NPC — one-time appearance, no prescription
**AGE:** Indeterminate. Somewhere between 40 and 60. Nothing about her appearance is memorable after the fact.
**CONTEXT:** Buys a single item over the counter — a bottle of aspirin. Does not approach for a prescription. Has no prior record in the system.

**QUIRK**

She completes the transaction without incident. While the player is ringing her up, without prompting and without waiting for a response: *"Tell her I said hello, if she's still around."*

She is out the door before the player can respond. If the player checks the computer for her name after she leaves, there is no record matching her description.

**PRESCRIPTION:** None. OTC only.

**DOPPELGANGER-ELIGIBLE:** No

**LORE LINE:** The line above is her entire function. It is the only uncanny lore moment in the NPC roster. Every other predecessor reference is mundane enough to be ignored. This one is not.

**NIGHT:** Night 3 or later. Never on night 1 or 2. The player should have had enough time with Marlene and Carl that "her" has a referent that feels almost within reach.

**DESIGN NOTES:** She is not a doppelganger. She is not explained. Her name is not in the system because it was never entered — she paid cash, gave no ID, bought aspirin. The line she delivers is the only moment in the game where the predecessor's existence becomes impossible to read as coincidence. Whether "if she's still around" is hopeful or uncertain is left entirely open. Do not follow up on her in any other NPC's dialogue. She appears once and is not referenced again.