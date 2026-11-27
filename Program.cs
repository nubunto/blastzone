using Blastzone.Render;
using Brutal.SdlApi;

// Optional: --frames N closes the window after N frames (used for automated checks).
var maxFrames = -1;
var framesArg = Array.IndexOf(args, "--frames");
if (framesArg >= 0 && framesArg + 1 < args.Length)
    maxFrames = int.Parse(args[framesArg + 1]);

Renderer.PrepareSdl();
Sdl.Init.Initialize(SdlInitFlags.Video | SdlInitFlags.Gamepad);
try
{
    using var window = Sdl.Video.CreateWindow("Blastzone", 1280, 720, SdlWindowFlags.Vulkan | SdlWindowFlags.HighPixelDensity);
    using var renderer = new Renderer(window);

    var running = true;
    var frame = 0;
    while (running && (maxFrames < 0 || frame < maxFrames))
    {
        while (Sdl.Events.PollEvent(out var ev))
        {
            if (ev.type is SdlEventType.Quit or SdlEventType.WindowCloseRequested)
                running = false;
            else if (ev.type == SdlEventType.KeyDown && ev.key.scancode == SdlScancode.Escape)
                running = false;
        }
        renderer.DrawFrame();
        frame++;
    }
    Console.WriteLine($"Rendered {frame} frames");
}
finally
{
    Sdl.Init.Quit();
}
