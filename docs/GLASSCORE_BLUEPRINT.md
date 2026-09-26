# GLASSCORE — Multiplayer Technical Blueprint

> *„Wer im Glashaus sitzt, sollte nicht mit Steinen werfen.“*
> Everyone fights on floating panes of glass with stone-throwing weapons. Every shot pushes you
> backwards (Newton's third law) and presses down on the glass you stand on, so careless shooters
> break their own floor.

This document maps every section of the original specification to the implementation in this
repository. Numbers in tables are the actual constants used in code.

| Layer | Path | Runs on |
|---|---|---|
| Simulation core (rules, fracture, movement, match state machine) | `Glasscore/Assets/Scripts/Simulation` | Unity client, dedicated server, tests |
| Netcode (transport, protocol, server, client prediction) | `Glasscore/Assets/Scripts/Net` | Unity client, dedicated server, tests |
| Unity client (rendering, UI, audio, VOIP, clips) | `Glasscore/Assets/Scripts/Client` | Unity 6 |
| Build automation | `Glasscore/Assets/Scripts/Editor`, `scripts/` | Unity editor / Windows |
| Headless dedicated server | `server/Glasscore.Server` | .NET 8 (Windows/Linux) |
| Tests | `tests/Glasscore.Tests` (36 tests), `tests/UnityCompileCheck` | .NET 8 |

---

## 1. Technical Architecture, Netcode & Global State

### 1.1 Engine and network library

| Spec | Implementation | Why |
|---|---|---|
| Unity (C#) + Photon Fusion / FishNet | **Unity 6 (C#) + custom netcode** (`Net/`) | Fusion needs a Photon account/App ID and a manual SDK import, which rules out an unattended build. The custom layer implements the same architecture and needs no packages. |
| Authoritative server | `GameServer` owns the only authoritative `GameWorld`; clients only send inputs | — |
| Client-side prediction + reconciliation | `GameClient.PredictTick` / `GameClient.Reconcile` | See 1.3 |
| Voronoi fracture | `VoronoiFracture2D` (half-plane clipping) + `PrismMeshBuilder` | Tiles are 0.1 m thin, so a 2D Voronoi extruded to the tile thickness is exact and much cheaper than 3D |
| `RPC_ShatterTile(int tileID, Vector3 impactPoint, float force)` | `MsgType.ShatterTile`: the payload is exactly those 3 values (+ instigator for the kill feed) | See 1.4 |

### 1.2 Transport

* **TCP** (reliable, ordered): lobby, chat, host commands, `TileDelta`, `ShatterTile`, world events,
  phase changes, results. Frames are prefixed with a 24-bit length.
* **UDP** (unreliable): inputs (each packet repeats the last 8 inputs), snapshots at 30 Hz, ping/pong and voice.
  Every client datagram starts with `[u16 magic][u32 token]`. The token from `Welcome` binds the UDP
  endpoint to the TCP connection and survives NAT rebinding.
* **Discovery** on UDP 27100: “who hosts code X?” / “any public lobby?”. It is broadcast on the LAN and also sent
  as unicast to every server host configured in Settings → Online.
* Ports: game `27015` TCP+UDP (listen servers fall back to 27016…27024), discovery `27100` UDP.

### 1.3 Tick model, prediction, reconciliation, interpolation

| Parameter | Value |
|---|---|
| Simulation tick | 60 Hz (`MatchTimings.TickRate`) |
| Client input send rate | 60 Hz, redundancy 8 |
| Snapshot rate | 30 Hz (`SnapshotInterval = 2`) |
| Remote interpolation delay | 100 ms |
| Server input queue | max 10; when it overflows, the oldest inputs are skipped but their button presses carry forward |
| Lag compensation | Projectiles hit-test targets rewound by `RTT/2 + 100 ms` (capped at 20 ticks = 333 ms) |

1. The client samples input once per tick, quantises it exactly like the wire format does, stores it in `_pending`,
   runs `PlayerSimulation.Step` locally, and sends it.
2. The server takes one input per player per tick and repeats the last one if the queue is empty.
3. Every snapshot carries `AckSequence` = the last input the server processed for that client.
4. On reconciliation the client adopts the server state, drops acknowledged inputs, and replays the rest
   through the **same** `PlayerSimulation.Step`.
5. The difference between the old and new predicted position becomes `CorrectionOffset`, which decays
   with a 60 ms half-life (“snaps back smoothly using interpolation”). Errors over 4 m, like a respawn,
   snap instantly.

Recoil is part of `PlayerSimulation.Step`, so it is predicted locally the moment you fire and
confirmed by the server.

### 1.4 Deterministic fracture sync

```
seed = FractureSeed(matchSeed, tileID, quantize_mm(impactPoint))   // SplitMix64 mixing
sites = GenerateSites(seed, impactLocal, force, count)             // xorshift128+ — bit-identical everywhere
cells = ComputeCells(sites)                                        // clip the 2×2 m pane by bisector half-planes
mesh  = PrismMeshBuilder.Build(cell, 0.1 m)                        // local, never sent over the network
```
The server sends ~20 bytes per shattered tile. Quantising the impact point to millimetres makes the seed
immune to float serialisation noise. Fragments are local, client-side Rigidbodies and do not affect gameplay.
Collision uses the replicated integrity array, where a destroyed tile is simply no longer solid.

### 1.5 Global state

| State | Owner | Replication |
|---|---|---|
| Tile integrity (`float`, max 1024 tiles) | `GameWorld.Tiles` | `TileState` (full, on join) + `TileDelta` (changed tiles per tick, centi-HP, rounded **up** so an intact tile is never 0 on a client) |
| Players (`PlayerState`) | `GameWorld.Players[8]` | Snapshot (full state, needed for reconciliation) |
| Projectiles | `GameWorld.Projectiles` | Snapshot (position + current velocity; clients extrapolate) |
| Match phase / clock | `MatchClock` | `MatchPhase` message + every snapshot |
| Lobby (settings, roster, ready, ping) | `GameServer` seats | `LobbyState` on change and every 2 s |

### 1.6 Core loop

`Main Menu → Online Lobby/Matchmaking → server spawn (listen or dedicated) → physics-based arena combat → Dynamic Fracture Cascade → social clip export → post-game lobby`

---

## 2. Main Menu System (Hauptmenü) — `UI/MainMenuScreens.cs`, `Presentation/MenuBackdrop.cs`

* **Background:** a neon-framed glass pane shatters in slow motion, hangs in the air and rewinds back together
  on an 8 s loop. The loop is evaluated from `Time.unscaledTimeAsDouble` every rendered frame (V-Synced), so it
  stays smooth at any frame rate.
* **Title:** “GLASSCORE”, pulsing cyan `#00FFFF` with a glow. The font is Bahnschrift, a DIN-style face that ships with
  Windows; it falls back to Segoe UI/Arial.
* **Navigation (vertical, left):** QUICK MATCH · CREATE LOBBY · JOIN LOBBY · CUSTOMIZATION · SETTINGS · QUIT.
  Works with the mouse, arrow keys + Enter, or D-Pad/stick + A.
* **Interaction** (`Ui.NeonButton`): on hover, `SFX_Glass_Tink_High` plays, the button scales to 1.05× and its glow
  gets stronger. On click, `SFX_Glass_Crack_Sharp` plays and the screen flashes white.

| Button | Behaviour |
|---|---|
| QUICK MATCH | SBMM search (see 3.3). If nothing fits within 15 s, you host a public lobby that other searchers find |
| CREATE LOBBY | Starts a listen server in the background, private, and shows the 6-character code |
| JOIN LOBBY | 6-character code (`LobbyCode.Normalize`: case and dashes don't matter) or `host:port` for a direct connect |
| CUSTOMIZATION | Weapon skins (6 per weapon) and suction-boot trails (5), saved locally and synced to the cloud (see 4.5) |
| SETTINGS | Section 4 |
| QUIT | `Application.Quit()` |

---

## 3. Multiplayer Lobby System (Lobby-Verwaltung) — `Net/GameServer.cs`, `UI/LobbyScreen.cs`

### 3.1 Room management

* **Host** = first player to join. If the host leaves, the role passes to the longest-connected player (`LobbyRules.NextHost`).
* Host privileges, all validated on the server: map, player limit 2–8 (never below the current player count), friendly fire,
  max score 5–25, mode (FFA/Teams), public/private, competitive, kick.
* **Ready system:** every connected client, host included, must be READY and there must be at least 2 players;
  then START GAME runs a 3 s countdown. If anyone un-readies, the countdown is cancelled.
  Public dedicated servers start automatically once everyone is ready.
* **Sync:** names, skins, trails, teams, ratings and ping (measured by the client and reported to the server) are sent in `LobbyState`.
* **Chat:** server-side `ChatFilter` removes markup tags, collapses whitespace, caps messages at 120 characters, allows at most
  1 message per 0.5 s and 5 per 5 s, and supports a blocked-word list.
* **Reconnect:** a player who drops mid-match keeps their seat and score for 60 s, matched by player key.

### 3.2 UI layout

* Left: roster with host ★, name, team, skin + trail swatches, 3-bar ping indicator (green < 60 ms, yellow < 120 ms,
  red), rating, ready ✓ (green) / ✗ (red), KICK (host only).
* Right: match settings (map dropdown, player limit slider, max score slider, mode, friendly fire, lobby type,
  competitive). Non-hosts see them read-only.
* Top right: lobby code with a COPY button. Bottom centre: chat. Bottom: READY / START GAME / SETTINGS / LEAVE.

### 3.3 Skill-based matchmaking

* Rating: multiplayer Elo (`RatingSystem`). Every pair of players counts as one duel and K is split across the N−1
  opponents. K = 48 for the first 20 games, 24 after that. A player who leaves a competitive match is ranked last and loses an extra 15.
* Search window (`MatchmakingPolicy`): ±100, widening by 50 every 5 s up to ±600. After 60 s any rating is accepted.
  Candidates are sorted by dedicated server first, then rating distance, then ping.
* Ratings are stored per server (`data/ratings.tsv`) and mirrored in the client profile.

---

## 4. Settings Menu (Einstellungen) — `Core/GameSettings.cs`, `UI/SettingsScreen.cs`

All values persist in `PlayerPrefs` (`gc.*`). APPLY or BACK saves them and applies display changes.

### 4.1 Graphics
| Option | Values |
|---|---|
| Resolution | 1920×1080, 2560×1440, 3840×2160 (the default is the largest that fits the monitor) |
| Display mode | Fullscreen (exclusive), Borderless Window, Windowed |
| Fracture physics quality | **Low:** 4 pre-fractured 12-piece patterns · **Medium:** real-time Voronoi, 20 pieces · **High:** full Voronoi, 112 fragments (max 96 / 400 / 1500 active) |
| V-Sync | On/Off (Off caps the frame rate at 240) |
| Field of view | 70–110° (extra) |

### 4.2 Audio
Master, SFX (“Glass ASMR”) and Music at 0–100%, plus voice volume. **VOIP:** on/off, input device (Unity microphone list),
output device (*System Default*, because Unity always plays through the Windows default device), push-to-talk on/off
(off = open mic with a voice-activity gate).

### 4.3 Controls remapping
An interactive matrix: rows are the 12 actions, columns are keyboard/mouse and gamepad. Click a cell and press any key,
mouse button or gamepad button (stored as a `KeyCode`; Esc cancels). There are sliders for mouse sensitivity and gamepad look
speed, an Invert Y toggle, and RESET DEFAULTS.

### 4.4 Online
Player name, extra dedicated-server hosts (these are searched by Quick Match and lobby codes), cloud status, and the clips folder.

### 4.5 Cloud save
`CloudSave` always writes `profile.json` locally. If `GLASSCORE_CLOUD_URL` (an environment variable) or a `cloud_url.txt`
file next to the exe is set, it also does `PUT {url}/profiles/{playerKey}` and `GET` to pull. On conflict, the newer
`UpdatedUnixMs` wins. No hosted backend is included; any JSON key-value service works.

---

## 5. Gameplay Mechanics & Physics — `Simulation/GlassRules.cs`, `PlayerRules.cs`, `PlayerSimulation.cs`, `GameWorld.cs`

### 5.1 Grid and structural stability
* Tiles are **2 m × 2 m × 0.1 m**. Each has a dense `tileID` (its Network_ID), identical on every peer because
  it is derived from the ASCII map.
* Maps (`MapCatalog`) are 12×12 per layer: *Gewächshaus* (2 layers), *Skyline Atrium* (3), *Kristallturm* (3).
  Each map has 8 spawn pads (`P`, bulletproof).

| Glass | HP | Absorbs | Heavy projectile | Neighbour transfer | Break | Shard burst |
|---|---|---|---|---|---|---|
| Standard float | 50 | 0% | **instantly shatters** it | **50 %** of the kinetic energy, split among intact orthogonal neighbours (cascades through standard glass, ≤ 3 hops, stops below 1 HP) | Voronoi | 15 dmg within 1.75 m (lethal shards) |
| Tempered safety | 100 | 0% | no instant break | – | spiderweb below 30 HP, then **crumbles into tiny non-lethal cubes all at once** | 0 |
| Reinforced bulletproof | 300 | **90 %** | blocked | – | cracks visibly, **blocks projectiles until depleted** | 10 dmg within 1.5 m |

A heavy projectile that shatters standard glass continues with half its energy. Every other hit stops the projectile.

### 5.2 Newton's third law (Rückstoß-Mechanik) — `RecoilModel`
```
Force_Recoil = Shot_Energy * -Player_Look_Direction          (Δv = impulse / 80 kg)
mid-air  → full Δv          (fire down ⇒ launched up: Findling gives +12 m/s)
grounded → vertical-down part becomes compression (the floor resists it)
anchored → Δv = 0, compression = Shot_Energy × 2  (suction boots double the load)
tile damage from compression = 0.04 HP per N·s, only if the tile is weakened (< 50 % HP) — or always while anchored
weight load = 1.5 HP/s on a weakened tile (3 HP/s while anchored)
```

| Weapon | Class | Player dmg | Tile dmg | Shot energy | Recoil Δv | Rate | Speed | Special |
|---|---|---|---|---|---|---|---|---|
| KIESEL (pebble sling) | light | 12 | 20 | 160 N·s | 2 m/s | 5/s | 70 m/s | |
| PFLASTERSTEIN (cobble launcher) | heavy | 30 | 60 | 480 N·s | 6 m/s | 1.25/s | 40 m/s | shatters standard glass instantly |
| FINDLING (boulder mortar) | heavy | 55 | 140 | 960 N·s | 12 m/s | 0.5/s | 28 m/s | 3 m splash; self-knockback for rocket jumps |
| SPLITTER (shard scatter) | light | 8 × 6 | 8 × 12 | 400 N·s | 5 m/s | 1/s | 55 m/s | 6° cone |

**Lag compensation / desync:** see 1.3. Movement integration uses swept AABBs per axis against the tile slabs, so a
fall at 40 m/s can never tunnel through 0.1 m of glass (this is tested).

### 5.3 Players and scoring
100 HP. A knockout comes from 0 HP, from shards, or from falling below the kill plane (25 m under the lowest layer).
The last player who hit you within 6 s gets +1. A knockout nobody else caused costs you 1 point (minimum 0)
— *wer im Glashaus sitzt*. Respawn takes 3 s on the intact spawn pad farthest from enemies (or the healthiest intact tile), with
2 s of spawn protection that ends as soon as you fire. Teams mode respects friendly fire.

---

## 6. Controls (Steuerung) — `Core/InputService.cs`, `ProjectSettings/InputManager.asset`

| Action | Keyboard & mouse | Gamepad (Xbox / PlayStation) |
|---|---|---|
| Move | WASD | Left stick |
| Aim / camera (360°) | Mouse | Right stick |
| Jump (vaulting off fragile glass deals **10 dmg** to the tile) | Space | A / Cross |
| Fire (projectile + recoil) | Left click | RT / R2 |
| Suction-boot anchor (1.5 s, 4 s cooldown, doubles the load) | Right click | LT / L2 |
| Cycle weapons | Q / E / mouse wheel | LB / RB |
| Push-to-talk (proximity voice) | V | – (open mic optional) |
| Mute / unmute mic | M | D-Pad Down |
| Pause | Esc | START / OPTIONS |

Short taps between two 60 Hz ticks are latched, so a press is never lost. Gamepad axes are mapped to the Xbox layout on Windows
(`GC_*` axes).

---

## 7. Pause Menu (Pausen-Menü) — `UI/MatchScreens.cs`, `Services/ClipRecorder.cs` (`CameraPostFx`)

* `Time.timeScale` **stays 1.0**, the match keeps running, and the client keeps sending **neutral** inputs, so your avatar stands still but
  can still be hit.
* The HUD is hidden. A separable 9-tap **Gaussian blur** (3 iterations at half resolution, animated in over 0.25 s) is applied over
  the live viewport.
* “MATCH IN PROGRESS” pulses in white, with latency, phase and remaining time.
* RESUME · OPTIONS (Settings drawn over the pause screen) · LEAVE MATCH (asks for confirmation; in a competitive match the
  server scores the leaver as last place with an extra −15 rating and holds the seat for 60 s; then you return to the main menu).

---

## 8. Multiplayer Script & Gameflow Event Timeline — `Simulation/MatchRules.cs` (`MatchClock`), `GameWorld`, `GameServer`

The server-authoritative timeline of one online match. Times are relative to the start of each phase. Ticks are at 60 Hz.

| # | Phase / event | When | Server action | Network traffic | Client reaction |
|---|---|---|---|---|---|
| 0 | **Server spawn** | on CREATE LOBBY / Quick Match without a hit / dedicated start | `GameServer.Start()`: TCP+UDP listener, discovery responder, lobby code | – | – |
| 1 | **Join** | any time in the lobby | assign a seat, pick the host, check the rating store | `Welcome`, `LobbyState`, system chat | lobby screen |
| 2 | **Lobby countdown** | all READY, ≥ 2 players | 3 s timer, cancelled if anyone un-readies | `LobbyCountdown` | “MATCH STARTS IN 3” |
| 3 | **Match start / Loading** | T = 0 (tick 0) | `new GameWorld(matchSeed)`, add players | `MatchStart(map, seed, maxScore, ff, teams)` | build the arena, then `SceneLoaded` |
| 4 | **Countdown** | all loaded or 20 s timeout (1200 ticks); abort if < 2 players | `SpawnAll` on the safest pads, inputs locked | `MatchPhase(Countdown)`, snapshots | “GET READY · 3 · 2 · 1 · SHATTER!” |
| 5 | **Live** | +5 s (300 ticks) | full simulation: movement, recoil, projectiles, damage, cascades | `Input` ↑, `Snapshot` ↓ 30 Hz, `TileDelta`, `ShatterTile`, `WorldEvent` | first-person combat |
| 5a | Pressure wave | Live +3:00 (tick +10 800) | every standard tile takes −10 HP | `WorldEvent(PressureWave)` + deltas | banner, sub-bass, camera shake |
| 5b | Cascade warning | Live +5:30 (tick +19 800) | – | `WorldEvent(CascadeWarning)` | siren, “FRACTURE CASCADE IN 30 SECONDS” |
| 6 | **Dynamic Fracture Cascade** | Live +6:00 (tick +21 600) | 12 erosion waves, one every 10 s: every intact tile outside radius `lerp(maxR, 3.2 m, (k+1)/12)` is destabilised and drains 25 % of its max HP per second (ignoring absorption) | `WorldEvent(ErosionWave k)`, deltas, `ShatterTile` | doomed tiles pulse red, “EDGE COLLAPSE k/12” |
| 7 | Score limit | any time in 5–6 | leader ≥ max score and not tied → `MatchEnd(ScoreLimit)` | `MatchPhase` | – |
| 8 | **Overtime** | end of the cascade (+2:00) while tied | golden knockout, max 60 s | `MatchPhase(Overtime)` | banner |
| 9 | Forfeit | fewer than 2 players connected | `MatchEnd(Forfeit)` | – | – |
| 10 | **Match end** | 4 s | clear projectiles, freeze combat, **pick the top 5 highlights** | `MatchPhase(MatchEnd)`, `Highlights` | winner banner, orbit camera; highlight ticks mapped to local clip time |
| 11 | **Highlights / clip export** | 10 s | – | – | results + highlight reel, EXPORT CLIP (JPEG frames + MP4 via ffmpeg) |
| 12 | **Results** | 15 s | placements, Elo deltas, leaver penalty, save `ratings.tsv` | `Results` | rating change, profile updated |
| 13 | **Post-game lobby** | after 15 s | reset READY, remove leavers | `ReturnToLobby`, `LobbyState` | back to the lobby, same code |

Highlight rules (`HighlightTracker`): multi-knockout (2+ within 4 s), fracture cascade (5+ tiles within 1 s), *Wer im
Glashaus sitzt…* (you fell after breaking your own floor), long shot (≥ 40 m), air strike (shot fired while airborne).

---

## 9. Audio

`AudioService` synthesises every sound at startup: inharmonic glass partials (tinks), filtered noise (cracks),
stochastic shard showers (shatters), clicks (tempered crumble), whooshes and thumps (weapons), and a 16 s generative
ambient music loop (Am–F–C–G). Gain = Master × SFX (or Music, or Voice). VOIP is 16 kHz G.711 μ-law in 20 ms frames on the
game's UDP channel. In a match it plays as 3D audio on the speaker's avatar; in the lobby it is 2D.

## 10. Known limitations

* No NAT traversal or relay. Internet play needs a dedicated server, or a host with TCP+UDP 27015 forwarded.
  LAN play works with no configuration (allow the Windows firewall prompt).
* A listen-server host who leaves ends the match for everyone. Dedicated servers don't have this problem.
* The Unity scripts compile against Unity reference assemblies (`tests/UnityCompileCheck`), but the real
  editor/player has not run in CI. The first build on a real PC is the final check.
