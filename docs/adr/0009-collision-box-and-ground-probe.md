# Stage collision uses an axis-aligned rectangle plus a Ground Probe

Each Character collides with the Stage using an axis-aligned rectangle (the Collision Box) for walls and ceilings, plus a single Ground Probe point at its bottom-center for floors, slopes and edges. A Character leaves an edge when the Ground Probe does. The Collision Box's size changes per animation state, set in data.

## Considered Options

- Diamond, as in Melee: handles slopes and corners naturally, but its collision math is more complex and a known source of bugs.
- Rectangle only: makes Characters float on slopes and stand on air at edges.
