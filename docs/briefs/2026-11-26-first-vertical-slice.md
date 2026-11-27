# First vertical slice

## Goal
Prove the core of the Blastzone Engine end to end: a deterministic, rollback-ready Simulation with data-driven moves drives two playable capsule Characters on one Stage. Every later feature (netcode, full movesets, Training Mode, Replays) builds on this foundation, so it has to be verified first.

## Done when
- Running the build opens a window where 2 local Players (keyboard and gamepad) can walk, dash, jump, double jump, fast-fall, drop through platforms and grab ledges.
- Each Character (one fast and light, one heavy) has 3 moves (jab, a tilt and an aerial) loaded from JSON Move Definitions. Hits apply damage %, Hitlag, Hitstun and knockback that scales with %.
- Crossing a Blast Zone costs a Stock and triggers a respawn, and the Match ends when one Player has no Stocks left.
- A test runs a recorded 10,000-Tick input stream twice and gets identical state hashes on every Tick.
- A Rollback test restores a snapshot from 8 Ticks earlier, re-simulates, and matches straight playback hash for hash.
- CI on macOS ARM64 and Windows x64 produces identical hashes for the same input stream.
- Adding a `float` to `Blastzone.Simulation` fails the build.
- Editing a Move Definition's JSON while the game runs takes effect without a restart (development builds only).
- A key toggles the display of Hitboxes, Hurtboxes, Collision Boxes and Ground Probes.

## Constraints
- Follow ADRs 0001–0014 in `docs/adr/`, and use the terms in `GLOSSARY.md`.
- Four assemblies: `Blastzone.Simulation` (no dependencies), `Blastzone.Engine` (on BRUTAL and SDL3), `Blastzone.Game` and `Blastzone.Tests`.
- Simulation: 60 Ticks per second, Q32.32 fixed-point, fixed-capacity arrays of plain structs, no reflection, no floats.
- Every gameplay value comes from settings or data, never from Engine code (ADR 0005).
- Each new mechanic gets a failing determinism or behaviour test before its implementation.
- Rendering is Vulkan through BRUTAL. Characters are drawn as capsules generated from their Hurtboxes.
- Visual results are checked by running the build and taking a screenshot, not by assuming.

## Out of scope
- Netcode, Replays, Training Mode beyond the hitbox display, ImGui panels, audio.
- Shields, Grabs, Throws, DI, SDI, ASDI, SSDI, LSI and Tech: these go in the next combat brief.
- Full movesets, Projectiles, 3D models, input remapping (fixed default bindings for now).

## Unknowns / risks
- **Q32.32 fixed-point performance and correctness in C#** (128-bit intermediate math, lookup-table trigonometry). Retire this first with a test spike: benchmark multiply and sine, and check results are bit-identical on both CI machines. If it fails, revisit ADR 0004 (for example, Q16.16) before any gameplay code depends on it.
- **SDL3 gamepad support through BRUTAL** is untested. Check a controller works early.
- **Cross-platform CI**: GitHub-hosted macOS ARM64 and Windows runners must both build BRUTAL's native packages.

## Milestones
1. Fixed-point spike passes on both platforms; the four assemblies exist with the analyzer and determinism test setup.
2. Capsule Characters move on the Stage with the full movement set, and determinism tests pass.
3. Hits, knockback, Stocks and respawn work, with Move Definitions loaded from JSON and hot-reloaded.
4. Rollback test and cross-platform hash comparison pass in CI.
