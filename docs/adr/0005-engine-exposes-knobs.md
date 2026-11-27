# The Engine hardcodes mechanisms, not gameplay values

Every gameplay constant (knockback scaling, weight factors, clank thresholds, Buffer Windows, Hitlag multipliers and so on) is a setting supplied by the Game, never a literal in Engine code. The Engine's goal is expressiveness: it ships the mechanisms with sensible defaults, and each Game tunes them to its own feel. Someone reading the code might expect familiar platform-fighter numbers to be hardcoded. They aren't, on purpose.
