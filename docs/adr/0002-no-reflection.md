# No runtime reflection in the Engine

Consoles are a long-term target. Their toolchains are ahead-of-time compiled and handle reflection poorly, so the Engine avoids runtime reflection from day one: no reflection-based serialization, no attribute scanning and no dynamic type loading. Data such as Move Definitions is read through explicit, generated or hand-written code.
