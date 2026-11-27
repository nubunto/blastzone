# Simulation state is fixed-capacity arrays of plain structs

All Simulation state lives in fixed-capacity arrays of plain (blittable) structs, one array per Entity kind (Characters, Projectiles, items, hazards), in a single contiguous block of memory. Saving or restoring a Rollback snapshot is then one memory copy. We chose this over an ECS because a Match has at most 4 Characters and a bounded number of Projectiles, so an ECS's flexibility isn't worth its cost. A class hierarchy was rejected because snapshotting it requires walking and copying every object individually.

## Consequences

- There are no managed references inside Simulation state. Entities reference each other by index.
- Every Entity kind has a fixed maximum count, set per Game.
