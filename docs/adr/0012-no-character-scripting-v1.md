# No per-Character scripting in v1

Characters are defined entirely by a fixed data schema: attributes plus Move Definitions. They can't run scripts. Running scripts inside a deterministic, rollback-ready Simulation raises questions of its own: determinism, snapshotting script state, and no reflection (ADR 0002). Those deserve a separate design round. Special mechanics such as meters or stances wait until then, while Projectiles are covered as an Entity kind with their own Move Definitions.
