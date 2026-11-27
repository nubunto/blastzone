using Blastzone.Engine.Data;
using Blastzone.Engine.Input;
using Blastzone.Engine.Render;
using Blastzone.Simulation;
using Brutal.SdlApi;

// Options (for automated checks):
//   --frames N         close the window after N rendered frames
//   --screenshot PATH  save the last rendered frame as a PNG
//   --debug            start with the debug display (F1) on
//   --demo             drive both Players with a canned input pattern instead of the devices
var maxFrames = IntOption("--frames") ?? -1;
var screenshotPath = StringOption("--screenshot");
var debug = args.Contains("--debug");
var demo = args.Contains("--demo");

var data = GameDataLoader.LoadFolder(Path.Combine(AppContext.BaseDirectory, "Data"),
    "Stages/proving-grounds.json", "Characters/kestrel.json", "Characters/bastion.json");
// Player 1 (keyboard) plays Kestrel, Player 2 (first gamepad) plays Bastion.
var match = new Match(data.Settings, data.Stage, data.Characters);
var previous = match.CreateSnapshotBuffer();
match.SaveSnapshot(previous);

Renderer.PrepareSdl();
Sdl.Init.Initialize(SdlInitFlags.Video | SdlInitFlags.Gamepad);
SdlGamepad? gamepad = null;
try
{
    using var window = Sdl.Video.CreateWindow("Blastzone", 1280, 720, SdlWindowFlags.Vulkan | SdlWindowFlags.HighPixelDensity);
    using var renderer = new Renderer(window);

    var timeline = new InputTimeline(new TickClock(Sdl.PInvoke.GetTicksNS()), gamepadId: 0);
    OpenFirstGamepad();

    var camera = new Camera(CenterX: 0, CenterY: 28, HalfHeight: 68);
    const int maxTicksPerFrame = 5; // after a long stall, catch up over several frames instead of all at once
    var shapes = new List<Shape>();
    var inputs = new PlayerInput[match.CharacterCount];
    long simulated = 0;
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
            else if (ev.type == SdlEventType.KeyDown && ev.key.scancode == SdlScancode.F1 && ev.key.repeat == 0)
                debug = !debug;
            else if (ev.type == SdlEventType.GamepadAdded && timeline.GamepadId == 0)
                OpenFirstGamepad();
            else if (InputEvent.FromSdl(ev) is { } input)
                timeline.Add(input);
        }

        var now = Sdl.PInvoke.GetTicksNS();
        var due = timeline.Clock.CompletedTicks(now);
        for (var step = 0; step < maxTicksPerFrame && simulated < due; step++)
        {
            var local = timeline.Sample(simulated);
            inputs[0] = demo ? Demo(0, simulated) : local.Keyboard;
            if (inputs.Length > 1)
                inputs[1] = demo ? Demo(1, simulated) : local.Gamepad;
            match.SaveSnapshot(previous);
            match.Step(inputs);
            simulated++;
        }

        var alpha = simulated < due ? 1 : timeline.Clock.Interpolation(now, simulated);
        MatchView.Build(match, previous, alpha, debug, shapes);
        var lastFrame = maxFrames >= 0 && frame == maxFrames - 1;
        renderer.DrawFrame(camera, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(shapes), lastFrame ? screenshotPath : null);
        frame++;
    }

    Console.WriteLine($"Rendered {frame} frames, simulated {simulated} Ticks, gamepad: {(gamepad is null ? "none" : Sdl.Gamepad.GetGamepadName(gamepad.Value))}");
    for (var i = 0; i < match.CharacterCount; i++)
    {
        var c = match.State.Characters[i];
        Console.WriteLine($"  Player {i + 1} {match.Attributes(i).Name}: {c.CurrentAction} at ({c.PositionX}, {c.PositionY})");
    }
    if (screenshotPath is not null)
        Console.WriteLine($"Screenshot: {screenshotPath}");

    void OpenFirstGamepad()
    {
        var ids = Sdl.Gamepad.GetGamepads();
        if (ids.Length == 0)
            return;
        gamepad = Sdl.Gamepad.OpenGamepad(ids[0]);
        timeline.GamepadId = ids[0];
    }
}
finally
{
    if (gamepad is { } open)
        Sdl.Gamepad.CloseGamepad(open);
    Sdl.Init.Quit();
}

// A canned 4-second loop: Player 1 dashes, jumps and double jumps; Player 2 walks and short hops.
static PlayerInput Demo(int player, long tick)
{
    var t = (int)(tick % 240);
    if (player == 0)
        return t switch
        {
            < 20 => new PlayerInput { StickX = 127 },
            < 22 => new PlayerInput { Buttons = InputButtons.Jump },
            < 40 => default,
            < 42 => new PlayerInput { Buttons = InputButtons.Jump },
            < 120 => default,
            < 140 => new PlayerInput { StickX = -127 },
            _ => default,
        };
    return t switch
    {
        < 60 => new PlayerInput { StickX = -60 },
        < 62 => new PlayerInput { Buttons = InputButtons.Jump },
        < 120 => default,
        < 180 => new PlayerInput { StickX = 60 },
        _ => default,
    };
}

int? IntOption(string name) => StringOption(name) is { } value ? int.Parse(value) : null;

string? StringOption(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
