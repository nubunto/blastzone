# C# on .NET with BRUTAL and SDL3 as the platform layer

The Engine is written from scratch in C# (.NET 11), using BRUTAL only for low-level bindings: SDL3 for windowing, input and audio, Vulkan 1.3 for rendering, and ImGui for debug UI. BRUTAL provides bindings, not an engine architecture, so the Engine owns its own game loop, memory and Simulation.

## Considered Options

- Odin with SDL3: rejected in favour of C#.
- GLFW for windowing and input: used at first, then replaced by SDL3. GLFW only supports polling for gamepads, with no timestamps, no GameCube adapter support, no rumble and no audio. SDL3 has all of these. Input events are assigned to Ticks by their SDL timestamp.
- RakNet for networking: rejected in favour of our own UDP layer behind a transport interface.

## Consequences

- BRUTAL's license requires a logo before gameplay, a credits notice and a shipped NOTICE.txt.
- Brutal.Core depends on Microsoft.Extensions DI, Configuration.Binder and Options, which are reflection-heavy. The Simulation must not use them (ADR 0002, ADR 0013).
- On macOS, Vulkan runs through MoltenVK (`brew install molten-vk vulkan-loader`). SDL won't find Homebrew's loader by itself, so the Engine sets the `SDL_VULKAN_LIBRARY` hint before initializing video. Rendering through SDL3 was verified on macOS arm64 on 2026-11-26.
