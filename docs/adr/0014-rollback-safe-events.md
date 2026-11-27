# Presentation effects come from Tick-tagged Simulation events

The Simulation never plays sounds or spawns effects directly. It emits events tagged with their Tick, and presentation (audio, visual effects) consumes them. During Rollback the same Ticks are re-simulated, so presentation de-duplicates events by Tick and identity: an event plays once, and an event a re-simulation no longer emits is cancelled where possible. Otherwise every Rollback would replay sounds.
