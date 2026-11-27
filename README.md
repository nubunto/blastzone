# Blastzone, a platform fighter engine

## Layout

| Project | Role |
| --- | --- |
| `src/Blastzone.Simulation` | The deterministic Simulation. No dependencies (ADR 0013); Q32.32 fixed point (ADR 0004); flat state (ADR 0011). |
| `src/Blastzone.Simulation.Analyzers` | Roslyn analyzer loaded only by the Simulation: floats, `System.Math`, clocks, `Random` and reflection are build errors. |
| `src/Blastzone.Engine` | Rendering and platform code on BRUTAL (SDL3 + Vulkan). |
| `src/Blastzone.Game` | The first-party Game executable. |
| `tests/Blastzone.Tests` | xUnit tests: fixed point, analyzer, determinism and Rollback. |
| `bench/Blastzone.Benchmarks` | BenchmarkDotNet micro-benchmarks for Simulation primitives. |

## Commands

```sh
dotnet build Blastzone.slnx
dotnet test Blastzone.slnx
dotnet run --project src/Blastzone.Game                 # add -- --frames 300 to exit after 300 frames
dotnet run -c Release --project bench/Blastzone.Benchmarks -- --filter '*' --inProcess
python3 tools/gen_sine_table.py > src/Blastzone.Simulation/Numerics/QuarterSineTable.g.cs
```

On macOS the Vulkan loader comes from Homebrew (`brew install vulkan-loader molten-vk`).
`--inProcess` is needed for benchmarks until BenchmarkDotNet recognises .NET 11.
