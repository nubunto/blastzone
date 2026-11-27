# Replays are locked to the engine version and data that recorded them

A Replay stores the starting setup, the engine version, the Move Definition hashes and the input stream, and plays back only when all of them match. Making old Replays run correctly on newer Simulation code means keeping every past version of the Simulation, which is expensive. A mismatched Replay is rejected with a clear message instead of desyncing.

## Consequences

- Any Simulation change invalidates existing Replays. Keeping old Simulation versions around for playback is a post-v1 decision.
