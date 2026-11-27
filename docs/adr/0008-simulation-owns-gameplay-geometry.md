# The Simulation owns all gameplay geometry, and rendering is read-only

Every Tick, the Simulation produces a read-only snapshot of its state. The renderer interpolates between the last two snapshots and never writes back. Hitboxes, Hurtboxes and Collision Boxes come only from data the Simulation owns, never from animation bones or meshes, and animations are timed in Ticks. This keeps the Simulation deterministic (ADR 0001) and lets a Game use 2D sprites or 3D models without changing gameplay.

## Consequences

- A 3D model can look out of sync with its Hurtboxes. Training Mode's hitbox display is how we catch that.
