# Move Definitions are JSON, read directly and checked by hash

Move Definitions are authored as JSON and read as text in every build, with no binary compile step. Values are converted to fixed-point once at load time (ADR 0004). Parsing uses .NET's low-level `Utf8JsonReader`, so there's no reflection (ADR 0002). The engine hashes the content of every Move Definition, and replays and netplay sessions compare these hashes, so mismatched data is rejected instead of desyncing silently.

## Considered Options

- A binary format compiled from text: deferred, and worth adding only if console load times demand it.
- TOML or a custom frame-data format: deferred. A custom format can compile down to the same data later.
