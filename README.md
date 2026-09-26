# Moneytree — SHORT LEGS

*"Lügen haben kurze Beine."* A detective / social-deduction game where every lie shrinks your legs by 25%.

- **Design & technical blueprint:** [`docs/SHORT_LEGS_GDD.md`](docs/SHORT_LEGS_GDD.md) (all 8 spec sections, plus art direction and the gameflow script)
- **Key art carousel** (styled after the reference post): [`promo/index.html`](promo/index.html)
- **Playable browser game:** [`web/index.html`](web/index.html) — main menu, Story Mode (Case 1), Party Mode vs. bots, Shrink Lab, notebook, pause menu, settings (DE/EN), Evidence Archive. Open `web/index.html` in a browser; no build step.
- **Unity reference implementation:** [`Unity/Assets/ShortLegs`](Unity/Assets/ShortLegs)

## Unity setup

Unity 6 (URP). Copy `Unity/Assets/ShortLegs` into a project and install these packages:

| Package | Used for |
|---|---|
| `com.unity.netcode.gameobjects` 2.x | networking, `NetworkVariable`, `[Rpc]` |
| `com.unity.services.multiplayer` | sessions, lobby codes, Relay |
| `com.unity.inputsystem` | `ShortLegsControls.inputactions` |
| `com.unity.localization` | Deutsch / English |
| `com.unity.render-pipelines.universal` | art-style volumes, pause vignette |
| `com.unity.test-framework` | EditMode tests |
| Vivox (`com.unity.services.vivox`) | proximity voice; route participant taps through `VoicePitchShifter.AttachVoiceSource` |

**Quick test without networking:** Tools ▸ Short Legs ▸ Create Test Scene builds a scene with stairs, a ledge and a
placeholder player driven by `Scripts/Prototype/ShortLegsEngine.cs`. In Play Mode: WASD / stick to move, Shift to sprint,
Space to jump, **L** to simulate a lie, **R** to reset.

The rules in `Scripts/Core` have no Unity dependency. The EditMode tests there also run outside Unity with the .NET SDK and NUnit.
