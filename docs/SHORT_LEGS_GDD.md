# SHORT LEGS — Game Design & Technical Blueprint

> *"Lügen haben kurze Beine."* — Lies have short legs.

**Genre:** Detective / Deduction · Social Deduction (4–8 players)
**Engine target:** Unity 6 (C#, primary — reference implementation in `Unity/`) · Unreal Engine 5 (C++, port notes in §1.6)
**Tagline:** *Solve the crime, catch the liar, and watch their legs shrink with every lie.*

---

## 0. ART DIRECTION — "LIKE THE PHOTO"

The visual reference is the stylized co-op survival look from the reference screenshot (Rerouted-style promo):
chunky caricatured 3D characters, painterly textures, cold ambient light cut by warm practical lights,
and first-person hands in the foreground. SHORT LEGS adopts that look and moves it indoors, into crime scenes.

### 0.1 Character look
| Trait | Reference (photo) | SHORT LEGS rule |
|---|---|---|
| Proportions | Oversized head and hands, bulky coat | Head = 1/4.5 of body height. **Legs are exaggerated: 1.35× the normal ratio**, so every 25% shrink reads clearly from across the room |
| Face | Huge round eyes, tiny pupils, bushy beard, "panic stare" | Every suspect has 3 face states: *Calm*, *Sweat* (1–2 lies), *Panic stare* (3+ lies, the eyes from the photo) |
| Hands | Big, soft, peach-toned first-person hands | Investigator first-person hands hold the magnifier, notebook and evidence cards |
| Materials | Hand-painted, low-frequency detail, no photoreal noise | Stylized PBR, roughness ≥ 0.6, painted albedo, 1-pixel ink outline post-process |

### 0.2 Light & color
- **Base palette:** cold blue-teal ambience (`#2E4A5C`, `#6F93A6`, `#C9DCE4`) against warm practical light (`#FF9A3C` lamps, fireplaces, desk lights) — the campfire-in-the-snow contrast from the photo.
- **Accent:** lie-detector neon red `#FF2D55` — used only for lies, shrink events and the title glow.
- **Fog:** light height fog in every map so silhouettes (and leg lengths) read against the background.

### 0.3 Marketing key art (social / store carousel)
Layout copied 1:1 from the reference post — see the working mockup in [`promo/index.html`](../promo/index.html):
- 2×2 grid of gameplay panels, edge-to-edge, no gutters.
- **Game title** centered on the horizontal seam of the top row: white, heavy sans, 2px dark outline + soft drop shadow.
- **Tagline** centered across the middle seam in 3–4 lines, white with black outline.
- Panels: (1) close-up of a panicking bearded suspect, (2) first-person hand presenting evidence to suspects, (3) interrogation table under a warm hanging lamp with first-person hand, (4) two players running down a hallway — one with shrunken legs falling behind.
- Carousel of 5 slides: Key art → Shrink Matrix → Multiplayer (4–8 players) → Story Mode → Main Menu.

---

## 1. TECHNICAL ARCHITECTURE & MULTIPLAYER NETCODE

### 1.1 Modes
| Mode | Players | Authority | Opponent |
|---|---|---|---|
| Story Mode | 1 | Local, offline (`StoryCaseDirector`) | Scripted AI suspects (`ScriptedSuspect`) |
| Social Deduction | 4–8 | Dedicated or host server | 1 Liar vs. 3–7 Investigators |

Both modes share the same pure-C# rules (`DeceptionEngine`, `ShrinkMatrix`) and the same `LegRig` / `VoicePitchShifter` presentation, so lies are judged identically in SP and MP. Story Mode runs offline and needs no network session.

### 1.2 Authoritative server model
```
 ┌──────────── SERVER (truth) ─────────────┐
 │ CaseTruth      – what really happened   │
 │ EvidenceLedger – discovered clues       │
 │ StatementLog   – everything said        │
 │ DeceptionEngine→ DeceptionIndex, lies   │
 │ ShrinkMatrix   → leg scale per player   │
 └───────┬─────────────────────────────────┘
         │ NetworkVariable<float> LegScale (state, late-join safe)
         │ RPC_SyncBoneScale(scale, lieCount, reason)  (event, drives VFX/SFX)
 ┌───────▼───────┐ ┌───────────────┐ ┌───────────────┐
 │ Client A      │ │ Client B      │ │ Client C      │
 │ LegRig tween  │ │ LegRig tween  │ │ LegRig tween  │
 └───────────────┘ └───────────────┘ └───────────────┘
```
- Clients **never** decide whether a statement is a lie. They send `SubmitStatementServerRpc(StatementPacket)`; the server evaluates it against `CaseTruth` + `EvidenceLedger`.
- Leg scale is **state** (a `NetworkVariable<float>`), so late joiners and reconnecting players see the correct height. The shrink **event** is additionally sent with `RPC_SyncBoneScale()` so every client plays the shrink animation, the lie-detector flash and the pitch-shift sting in sync.
- Movement is client-predicted but the server clamps speed with the same `ShrinkMatrix` math (`ServerMovementValidator`) — a shrunk player cannot speed-hack back to full speed.

### 1.3 The Deception Index
For each statement the server computes:

```
DeceptionIndex = Σ over contradicted facts ( fact.Weight × visibility )
visibility     = 1.0  if contradicting evidence is already discovered (public)
               = 0.0  if it is still hidden  → the lie is stored as a LATENT LIE
IsLie          = DeceptionIndex ≥ LIE_THRESHOLD (default 1.0)
```
**Latent lies are the heart of the proverb.** A lie that no discovered clue contradicts yet is not punished immediately — it waits. The moment an investigator discovers the contradicting clue, every latent lie tied to it resolves and the liar's legs shrink *right then*, in front of everyone. Lies always catch up with you.

### 1.4 Bone transform sync
- Humanoid rig bones scaled: `LeftUpperLeg, LeftLowerLeg, RightUpperLeg, RightLowerLeg` (Y axis only — feet keep their size, which is funnier).
- Hips are lowered by `legLength × (1 − scale)` so feet stay on the ground; the `CharacterController` height/center are recomputed.
- Scale is applied in `LateUpdate()` after the Animator, tweened over 0.6 s with an overshoot ease ("boing").

### 1.5 Networking stack (Unity)
- **Netcode for GameObjects 2.x** (`NetworkVariable`, `[Rpc(SendTo.…)]`), Unity Transport, **Unity Multiplayer Services (Sessions over Relay)** for 6-char lobby codes, **Vivox** for proximity voice.
- NGO requires RPC methods to end in `Rpc`, so the spec's `RPC_SyncBoneScale()` is the server-side entry point that fires `SyncBoneScaleRpc` to every client.
- Tick rate 30 Hz, interest management off (max 8 players, small maps).
- Reference code: `Unity/Assets/ShortLegs/Scripts/Networking/`.

### 1.6 Unreal Engine 5 port notes
| Unity | UE5 |
|---|---|
| `NetworkVariable<float> LegScale` | `UPROPERTY(ReplicatedUsing=OnRep_LegScale) float LegScale;` |
| `RPC_SyncBoneScale` (Rpc to everyone) | `UFUNCTION(NetMulticast, Reliable) void Multicast_SyncBoneScale(float Scale, int32 Lies);` |
| `SubmitStatementServerRpc` | `UFUNCTION(Server, Reliable) void Server_SubmitStatement(FStatementPacket P);` |
| `LegRig` (LateUpdate bone scale) | `AnimBP` → `Transform (Modify) Bone` nodes driven by `LegScale` |
| AudioMixer pitch | MetaSound `Pitch Shift` node / `VoiceChat` Vivox plugin |

---

## 2. MAIN MENU SYSTEM (HAUPTMENÜ)

**Scene:** a dark 3D interrogation room in the art style from §0. Center stage: a stylized suspect silhouette on a chair under a hanging lamp. Every 4–7 s the neon lie-detector light on the wall flashes red — the silhouette's legs shrink one step (1.0 → 0.75 → 0.5 → 0.25) with a "boing" and a squeaky sting, then pop back to full length after the 4th step. Dust particles float in the lamp cone.

### 2.1 UI layout
- **Title banner:** `SHORT LEGS` — Retro Noir Serif, white with red neon under-glow. Letters drip/shrink: the vertical scale of each glyph's lower half animates in sync with the silhouette's legs.
- **Navigation list** (vertical, left aligned, 64 px from left edge, 1st item focused by default):
  1. `[STORY MODE]` → Case select (solo detective campaign)
  2. `[MULTIPLAYER LOBBY]` → Host / Join by 6-char lobby code / Quick match
  3. `[EVIDENCE ARCHIVE]` → Unlocked lore, solved cases, cosmetic hats
  4. `[SETTINGS]` → §4
  5. `[QUIT]` → Confirmation dialog → `Application.Quit()`
- **Focus feedback:** hovered item slides 12 px right, gains a red bullet `▸` and the lie-detector ticks once.
- **Footer:** build version, online status dot, profile hat preview.

Reference code: `UI/MainMenuController.cs`.

---

## 3. MULTIPLAYER LOBBY & PRE-GAME SYSTEM

### 3.1 Lobby
- Waiting room = the manor's entrance hall; players can walk around, try on hats, and test voice chat.
- **Host settings** (replicated `LobbySettings` struct, host-only edit):

| Setting | Range | Default |
|---|---|---|
| Max Lies Allowed (before instant defeat) | 1 – 4 | 4 |
| Proximity voice chat | On / Off | On |
| Crime scene map | The Grand Manor · The Sunken Yacht · The Night Train · The Frozen Lodge | The Grand Manor |
| Match timer | 8 – 20 min | 12 min |
| Meetings per player | 1 – 3 | 1 |
| Clues required | 3 (fixed) | 3 |

- **Ready check:** all players must press Ready; host presses Start (min. 4 players).

### 3.2 Role assignment
At match start the server draws a seed from a CSPRNG (`System.Security.Cryptography.RandomNumberGenerator`), stores it in the match log, and uses it to pick the Liar uniformly. Roles are sent **only to the owning client** (`RpcTarget.Single`) — other clients never receive who the Liar is, so memory inspection cannot reveal it.

| Role | Goal | Tools |
|---|---|---|
| **Investigators** | Find 3 physical clues **and** accuse the correct player | Magnifier (inspect), Notebook, Evidence Binding Wheel, 1 Emergency Meeting each |
| **The Liar** | Stall until the timer runs out **or** get investigators eliminated by framing | Plant false clue (2×), "Alibi" statement, Sabotage lights (1×) |

**Elimination by framing:** a wrong accusation that reaches a majority vote eliminates the accused investigator (they become a Ghost who can still read the notebook but not speak). If investigators drop to 1, the Liar wins.

**Instant defeat:** when the Liar reaches `MaxLiesAllowed`, their leg scale hits 0 (or the cap), they are exposed and the Investigators win.

---

## 4. SETTINGS & CONFIGURATION MENU (EINSTELLUNGEN)

Persisted to `Application.persistentDataPath/profile_settings.json` (versioned JSON, see `Settings/SettingsManager.cs`). Changes preview live; `Apply` saves, `Back` reverts.

### 4.1 Graphics & Audio
- **Resolution:** all supported resolutions up to 3840×2160 (4K).
- **Display mode:** Fullscreen · Borderless · Windowed.
- **Art Style Filter:**
  - *Classic Noir* — full desaturation post-process, contrast +20%, red channel kept for `#FF2D55` accents (lies, blood, the title).
  - *Vibrant Comic-Book* — the default painterly look from §0 plus halftone shading and 2 px ink outlines.
- **Audio channels** (independent sliders, 0–100%, mapped to AudioMixer dB via `20·log10(v)`): Master · Music · Voice Chat (VOIP) · Mechanic SFX.

### 4.2 Accessibility & Language
- **Text-to-Speech:** reads incoming text chat and dialogue options aloud.
- **Speech-to-Text:** transcribes incoming VOIP into the chat feed (for deaf / hard-of-hearing players).
- **Subtitles:** On/Off, size S/M/L/XL, high-contrast box (black 85% background, white text, speaker name colored).
- **Language:** Deutsch · English (all UI + dialog localized via Unity Localization).
- **Reduce Flashing:** replaces the red lie-detector flash with a steady border pulse.

---

## 5. THE CORE MECHANIC: THE DECEPTION-SHRINK MATRIX

### 5.1 The Dialogue & Accusation Engine — Evidence Binding Wheel
Statements are built, not typed, so the server can evaluate them:

```
[ SUBJECT ]  +  [ CLAIM ]           +  [ VALUE ]          (+ optional [ EVIDENCE ])
  "I"           "was in"               "the Library"
  "Player 3"    "had"                  "the Key"
  "Nobody"      "touched"              "the Money Tree"
```
- The wheel has 3 rings (Subject / Claim / Value); the 4th slot binds a clue from the notebook as proof.
- Each statement is converted to a `FactClaim(subjectId, predicate, value, timeSlot)` and compared against `CaseTruth`.
- **Shrink Event** triggers when a claim contradicts a *discovered* fact. Contradicting an *undiscovered* fact creates a latent lie (§1.3).
- **Presenting evidence** against another player's statement lets investigators force a re-evaluation (and earns bonus score if it exposes a lie).
- Singleplayer: NPC suspects use the same engine — their scripted lies are latent until you find the matching clue, then you *Present* it and watch the suspect shrink.

### 5.2 Physical consequences — the Shrink Matrix
Every lie scales the leg bones down by **25% per lie**:

| Lies | Leg scale | Speed (`Base × scale`) | Sprint | Step up stairs/ledges | Jump | Posture | Voice pitch |
|---|---|---|---|---|---|---|---|
| 0 | 1.00 | 100% | ✔ | ✔ | ✔ | Stand | 1.00× |
| 1 | 0.75 | 75% | ✘ | ✔ | ✔ | Stand | 1.15× |
| 2 | 0.50 | 50% | ✘ | ✘ | ✔ | Waddle | 1.30× |
| 3 | 0.25 | 25% | ✘ | ✘ | ✘ | **Crawl** | 1.45× |
| 4 | 0.00 → exposed | — | — | — | — | Stuck | 1.60× |

- `Speed = Base_Speed × Current_Leg_Scale` (linear, as specified). Crawl uses the same formula — it just switches the animation set.
- Step-offset of the `CharacterController` is set to 0 at 2+ lies; stairs become walls. Maps are designed so a 2-lie player can still reach every room by ramps, but slower.
- **Voice pitch:** `pitch = 1 + 0.15 × lies`, applied by a pitch-shifter DSP on the player's VOIP channel (formant-preserving off — the chipmunk effect is intended).
- **Readability:** shrinking plays a 0.6 s "boing" tween, a red lie-detector flash on the HUD of every player, and a squeaky SFX. Face switches to Sweat / Panic stare (§0.1).

Reference code: `Core/ShrinkMatrix.cs`, `Core/DeceptionEngine.cs`, `Gameplay/LegRig.cs`, `Gameplay/ShrinkAwareLocomotion.cs`, `Audio/VoicePitchShifter.cs`.

---

## 6. CONTROLS SYSTEM (STEUERUNG)

Implemented with the Unity Input System — see `Unity/Assets/ShortLegs/Input/ShortLegsControls.inputactions`. All bindings are rebindable in Settings.

### 6.1 Keyboard & Mouse (PC default)
| Input | Action |
|---|---|
| **W A S D** | Move character / navigate interrogation room |
| **Mouse move** | Look / inspect clues |
| **Left click** | Interact · select dialogue option · present evidence |
| **Shift** | Sprint (disabled at ≥ 1 lie) |
| **Space** | Jump (disabled at ≥ 3 lies) |
| **Tab** | Detective Notebook (clues, player profiles, **Leg Status Tracker**) |
| **V** | Push-to-talk voice chat |
| **Esc** | Pause menu |

### 6.2 Gamepad
| Input | Action |
|---|---|
| **Left stick** | Move |
| **Right stick** | Camera |
| **RT** | Inspect clue closer (zoom) |
| **South (A / ✕)** | Confirm dialogue / accuse |
| **D-Pad Up / Down** | Scroll notebook |
| **View / Touchpad** | Open notebook |
| **LB (hold)** | Push-to-talk |
| **Start / Options** | Pause menu |

---

## 7. PAUSE MENU SYSTEM (PAUSEN-MENÜ)

- **Singleplayer:** `Time.timeScale = 0`, audio listener paused except UI bus.
- **Multiplayer:** server time **does not stop**; the local player only gets the overlay (input to the pawn is blocked, a small "⏸ paused" badge appears above their head for others).
- Dark vignette (post-process, intensity 0.55) + background blur.

### 7.1 Layout
- **Header:** `INVESTIGATION PAUSED`
- `[RESUME]` · `[REVIEW NOTEBOOK]` · `[OPTIONS]` · `[ABANDON CASE]` (confirmation; in MP counts as disconnect — the Liar's abandon counts as an Investigator win).

Reference code: `UI/PauseMenuController.cs`.

---

## 8. GAMEFLOW EVENT TIMELINE (SCRIPT)

### 8.1 Multiplayer match — "The Grand Manor: The Missing Money Tree" (12 min default)

| Time | Phase | Server event | What players see / do |
|---|---|---|---|
| −00:30 | **Lobby** | `LobbySettings` locked, ready check | Players walk the entrance hall, try hats |
| 00:00 | **Role Reveal** | CSPRNG seed → Liar picked; `RoleAssignedRpc` to each owner only | Screen fades to noir; card flips: *INVESTIGATOR* or *THE LIAR* (red) |
| 00:05 | **Crime Briefing** | `CaseTruth` spawned: the golden *Money Tree* bonsai is gone from the study; 9 clue spots, 3 true clues + 6 decoys | Butler NPC narrates the crime; subtitles; timer starts |
| 00:10 | **Opening Alibis** | Each player must submit 1 statement (where they were at 23:00) | Evidence Binding Wheel opens for 30 s. The Liar's alibi becomes a latent lie |
| 00:40 – 05:00 | **Investigation I** | Clue spots active; Liar may plant 1 false clue | Search rooms, inspect with magnifier / RT, log clues in notebook |
| any time | **Clue discovered** | `EvidenceLedger.Add()` → resolve latent lies | If it contradicts a latent lie: **Shrink Event** — red flash for everyone, liar's legs "boing" down 25%, voice pitches up |
| 05:00 | **Lights Out** (optional Liar sabotage) | Lights off 20 s, flashlights only | Liar's chance to move unseen — but short legs still show in silhouettes |
| 05:00 – 06:30 | **Meeting I** (auto or emergency) | Everyone teleported to the interrogation room | Voice discussion, present evidence against statements, new statements |
| 06:30 – 10:00 | **Investigation II** | Liar's 2nd false clue unlocked | Remaining clues; players with short legs are visibly slower on stairs |
| 10:00 | **Final Meeting** | Final statements + vote | Each player: 1 statement + 1 accusation vote |
| 10:30 | **Accusation** | Majority vote resolved | Wrong target → framed investigator eliminated, game continues if time remains. Correct target **and** ≥3 true clues → Investigators win |
| 12:00 | **Time up** | Timer = 0 | Liar wins if not exposed |
| end | **Reveal** | Match log replay | "Lie Replay": every lie of the match shown with timestamp and shrink moment; XP, hats unlocked |

**Instant ends:** Liar reaches `MaxLiesAllowed` → exposed (legs 0) → Investigators win · only 1 investigator left → Liar wins.

### 8.2 Story Mode — Case 1: "The Missing Money Tree" (≈ 45 min)

| Beat | Location | Script |
|---|---|---|
| 1. Cold open | Manor gates, night, snow | Detective (player) arrives. First-person hands, flashlight. Tutorial: move, look, interact |
| 2. The crime | Study | The golden bonsai "Money Tree" is gone. Tutorial: inspect clues (muddy boot print size 46, torn green glove, a snapped watch at 23:07) |
| 3. Suspect lineup | Great Hall | Three suspects, all long-legged: **Baron Brumm** (bearded, the panic-stare guy from the key art), **Madame Velours** (housekeeper), **Kip** (the gardener) |
| 4. Interrogation I | Interrogation room | Each suspect gives an alibi. Brumm: *"I was asleep at 23:00."* → stored as latent lie. Tutorial: Evidence Binding Wheel |
| 5. Investigation | Greenhouse, kitchen, cellar | Find: Brumm's wet coat (snow melt), the green glove's twin in Kip's shed (planted!), cellar door key |
| 6. First shrink | Interrogation room | Present the wet coat → Brumm's alibi contradicted → **first Shrink Event** tutorial: his legs drop to 0.75, voice squeaks |
| 7. Red herring | Shed | Kip is framed by the planted glove. Presenting it wrongly costs the *player* reputation (Story mode fail-state: 3 wrong accusations = case lost) |
| 8. The chase | Manor halls | Brumm flees — but with 0.5 legs he can't take the stairs. Short chase, he waddles into the cellar |
| 9. Final interrogation | Cellar | Present the snapped watch + boot print → Brumm at 0.25, crawling, confesses in a chipmunk voice |
| 10. Resolution | Study | Money Tree returned. Unlock: *Evidence Archive* entry, hat "Brumm's Beanie" |

### 8.3 Event flow (state machine)
```
Lobby → RoleReveal → Briefing → OpeningAlibis → Investigation ⇄ Meeting → FinalMeeting → Accusation → Reveal
                                        ↑ Shrink Events can fire in any phase ↓
                              LiarExposed (max lies)  ·  TimeUp  ·  InvestigatorsWiped
```
Reference code: `Networking/MatchDirector.cs`.

---

## 9. REPO MAP

```
docs/SHORT_LEGS_GDD.md                              ← this document
promo/index.html                                    ← key-art carousel in the style of the reference photo
web/index.html + web/js/{engine,data,game}.js       ← playable browser build (same rules as Scripts/Core)
Unity/Assets/ShortLegs/
  Input/ShortLegsControls.inputactions              ← §6 bindings (KB&M + Gamepad schemes)
  Scripts/Core/            (pure C#, no UnityEngine — unit tested)
    CaseModels.cs                                   ← facts, clues, statements, contradiction rules
    DeceptionEngine.cs                              ← §1.3 / §5.1 Deception Index, latent lies, framing
    ShrinkMatrix.cs                                 ← §5.2 leg scale, speed, locks, voice pitch
  Scripts/Cases/CaseDefinition.cs                   ← ScriptableObject case data (clues → facts)
  Scripts/Networking/
    NetTypes.cs                                     ← roles, phases, LobbySettings, StatementPacket
    ShortLegsPlayer.cs                              ← LegScale NetworkVariable, RPC_SyncBoneScale, intents, speed validation
    MatchDirector.cs                                ← §3 roles, §8.1 timeline, clues, meetings, votes, win conditions
    SessionConnector.cs                             ← host / join by code / quick match
  Scripts/Gameplay/
    LegRig.cs                                       ← §1.4 bone scaling + collider
    ShrinkAwareLocomotion.cs                        ← §5.2 movement gating
    ClueSpot.cs, Interactor.cs                      ← searching / inspecting
  Scripts/Story/                                    ← §8.2 StoryCaseDirector, ScriptedSuspect, StoryClue, Progression
  Scripts/Audio/VoicePitchShifter.cs                ← §5.2 voice pitch via mixer groups
  Scripts/Settings/SettingsManager.cs, ArtStyleFilter.cs ← §4
  Scripts/UI/                                       ← §2 MainMenu, §7 PauseMenu, Notebook + Leg Status Tracker,
                                                      Evidence Binding Wheel, HUD, Lobby room
  Tests/EditMode/                                   ← NUnit tests for ShrinkMatrix + DeceptionEngine
  Scripts/Prototype/ShortLegsEngine.cs              ← offline single-object Shrink Matrix prototype (no network)
  Editor/ShortLegsSetup.cs                          ← Tools ▸ Short Legs ▸ Create Test Scene
```
