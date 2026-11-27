# Fixed-point numbers in the Simulation

The Simulation uses 64-bit Q32.32 fixed-point numbers (32 integer bits, 32 fractional bits) for every quantity, with trigonometry and square roots taken from deterministic lookup tables. Players on x64 Windows and ARM64 macOS must stay bit-identical for Rollback (ADR 0001). .NET doesn't guarantee identical floating-point results across architectures or compiler versions, so floats aren't safe.

## Considered Options

- Floats with strict controls: rejected, because the JIT and FMA behaviour aren't ours to control.
- Floats with same-architecture-only matchmaking: rejected, because it splits the player base.

## Consequences

- Floats appear only when rendering reads Simulation state.
- Move Definitions are converted to fixed-point at load time, never at Tick time.
