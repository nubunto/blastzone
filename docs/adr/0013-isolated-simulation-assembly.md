# The Simulation is an isolated assembly

The code is split into `Blastzone.Simulation` (no dependencies: no BRUTAL, no floats, no reflection), `Blastzone.Engine` (rendering, input, audio and netcode on BRUTAL), `Blastzone.Game` (our Characters and Stage) and `Blastzone.Tests`. The Simulation can't reference anything outside itself, so the compiler enforces ADRs 0002, 0004 and 0008, which is stronger than code review.

A Roslyn analyzer on `Blastzone.Simulation` fails the build on any `float`, `double`, `decimal`, `System.Math`, wall-clock or reflection API, since assembly isolation alone can't ban language types.
