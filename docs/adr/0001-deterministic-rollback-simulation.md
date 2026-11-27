# Deterministic, rollback-ready Simulation from day one

The Simulation runs at a fixed 60 Ticks per second. Given the same inputs it must produce bit-identical results on every machine, and its whole state can be snapshotted and restored cheaply. We chose this from the start because rollback netcode and replays both depend on it, and adding determinism to an existing engine usually means rewriting it.

## Consequences

- No wall-clock time, unordered iteration or uncontrolled floating point inside the Simulation.
- Rendering reads Simulation state and never writes to it.
- A replay is just the initial state plus the input stream.
