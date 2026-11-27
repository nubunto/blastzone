using Blastzone.Simulation;

namespace Blastzone.Engine.Data;

/// <summary>
/// Reads Game data (settings, Characters, Stages) into Simulation types, converting every number to
/// fixed-point once at load time (ADR 0004, ADR 0006). Hand-written mapping, no reflection (ADR 0002).
/// </summary>
public static class GameDataLoader
{
    /// <summary>The Air Dodge preset used when settings don't name one.</summary>
    public const string DefaultAirDodgePreset = "melee";

    public static MatchSettings LoadSettings(ReadOnlySpan<byte> utf8, string source)
    {
        var root = JsonData.Parse(utf8, source);
        var stick = root["stick"];
        var buffer = root["bufferWindow"];
        var bufferDefault = buffer["default"].Int;
        int Window(string action) => buffer.Has(action) ? buffer[action].Int : bufferDefault;
        var ledge = root["ledge"];

        var presetName = root.Has("airDodge") ? root["airDodge"].String : DefaultAirDodgePreset;
        AirDodgeSettings? airDodge = null;
        foreach (var (name, preset) in root["airDodgePresets"].Members)
        {
            var knobs = new AirDodgeSettings
            {
                Directional = preset["directional"].Bool,
                ZeroVelocityAtEnd = preset["zeroVelocityAtEnd"].Bool,
                HelplessAfter = preset["helplessAfter"].Bool,
            };
            if (name == presetName)
                airDodge = knobs;
        }
        if (airDodge is null)
            throw new GameDataException($"{source}: Air Dodge preset '{presetName}' is not in 'airDodgePresets'.");

        var settings = new MatchSettings
        {
            MaxCharacters = root["maxCharacters"].Int,
            Stick = new StickSettings
            {
                WalkThreshold = stick["walkThreshold"].Int,
                TapThreshold = stick["tapThreshold"].Int,
                TapOriginMax = stick["tapOriginMax"].Int,
                DownTapThreshold = stick["downTapThreshold"].Int,
                LedgeThreshold = stick["ledgeThreshold"].Int,
                AirDodgeThreshold = stick["airDodgeThreshold"].Int,
            },
            Buffer = new BufferWindows
            {
                Jump = Window("jump"),
                Shield = Window("shield"),
                Dash = Window("dash"),
                Down = Window("down"),
            },
            AirDodge = airDodge,
            Ledge = new LedgeSettings
            {
                Trump = ledge["trump"].Bool,
                IntangibilityTicks = ledge["intangibilityTicks"].Int,
                RegrabIntangibilityShrinkTicks = ledge["regrabIntangibilityShrinkTicks"].Int,
                ExitIntangibilityTicks = ledge["exitIntangibilityTicks"].Int,
                RegrabCooldownTicks = ledge["regrabCooldownTicks"].Int,
                TrumpVelocityX = ledge["trumpVelocityX"].Fixed,
                TrumpVelocityY = ledge["trumpVelocityY"].Fixed,
                TrumpStunTicks = ledge["trumpStunTicks"].Int,
                TrumpRegrabCooldownTicks = ledge["trumpRegrabCooldownTicks"].Int,
            },
            TeeterMaxSpeed = root["teeterMaxSpeed"].Fixed,
            WallJumpReach = root["wallJumpReach"].Fixed,
        };
        root.RejectUnknownKeys();
        return settings;
    }

    public static CharacterAttributes LoadCharacter(ReadOnlySpan<byte> utf8, string source)
    {
        var root = JsonData.Parse(utf8, source);
        var ground = root["ground"];
        var air = root["air"];
        var jump = root["jump"];
        var dodge = root["airDodge"];
        var wall = root["wallJump"];
        var ledge = root["ledge"];
        var boxes = root["collisionBoxes"];
        static BoxSize Box(JsonData box) => new(box["width"].Fixed, box["height"].Fixed);

        var hurtboxes = new List<Capsule>();
        foreach (var h in root["hurtboxes"].Items)
            hurtboxes.Add(new Capsule(h["x1"].Fixed, h["y1"].Fixed, h["x2"].Fixed, h["y2"].Fixed, h["radius"].Fixed));

        var character = new CharacterAttributes
        {
            Name = root["name"].String,
            Weight = root["weight"].Fixed,
            WalkSpeed = ground["walkSpeed"].Fixed,
            WalkAcceleration = ground["walkAcceleration"].Fixed,
            DashInitialSpeed = ground["dashInitialSpeed"].Fixed,
            DashTicks = ground["dashTicks"].Int,
            RunSpeed = ground["runSpeed"].Fixed,
            RunAcceleration = ground["runAcceleration"].Fixed,
            RunTurnTicks = ground["runTurnTicks"].Int,
            Traction = ground["traction"].Fixed,
            AirSpeed = air["airSpeed"].Fixed,
            AirAcceleration = air["airAcceleration"].Fixed,
            AirFriction = air["airFriction"].Fixed,
            Gravity = air["gravity"].Fixed,
            MaxFallSpeed = air["maxFallSpeed"].Fixed,
            FastFallSpeed = air["fastFallSpeed"].Fixed,
            JumpSquatTicks = jump["jumpSquatTicks"].Int,
            FullHopHeight = jump["fullHopHeight"].Fixed,
            ShortHopHeight = jump["shortHopHeight"].Fixed,
            DoubleJumpHeight = jump["doubleJumpHeight"].Fixed,
            AirJumps = jump["airJumps"].Int,
            LandingTicks = jump["landingTicks"].Int,
            HelplessLandingTicks = jump["helplessLandingTicks"].Int,
            AirDodgeSpeed = dodge["speed"].Fixed,
            AirDodgeDecay = dodge["decay"].Fixed,
            AirDodgeTicks = dodge["ticks"].Int,
            AirDodgeIntangibilityTicks = dodge["intangibilityTicks"].Int,
            AirDodgeLandingTicks = dodge["landingTicks"].Int,
            CanWallJump = wall["enabled"].Bool,
            WallJumpSpeedX = wall["speedX"].Fixed,
            WallJumpSpeedY = wall["speedY"].Fixed,
            LedgeGrabHalfWidth = ledge["grabHalfWidth"].Fixed,
            LedgeGrabBottom = ledge["grabBottom"].Fixed,
            LedgeGrabTop = ledge["grabTop"].Fixed,
            LedgeHangOffsetX = ledge["hangOffsetX"].Fixed,
            LedgeHangOffsetY = ledge["hangOffsetY"].Fixed,
            LedgeActionableTicks = ledge["actionableTicks"].Int,
            LedgeGetupTicks = ledge["getupTicks"].Int,
            LedgeGetupDistance = ledge["getupDistance"].Fixed,
            LedgeGetupIntangibilityTicks = ledge["getupIntangibilityTicks"].Int,
            LedgeRollTicks = ledge["rollTicks"].Int,
            LedgeRollDistance = ledge["rollDistance"].Fixed,
            LedgeRollIntangibilityTicks = ledge["rollIntangibilityTicks"].Int,
            LedgeJumpHeight = ledge["jumpHeight"].Fixed,
            StandingBox = Box(boxes["standing"]),
            CrouchingBox = Box(boxes["crouching"]),
            AirborneBox = Box(boxes["airborne"]),
            LedgeBox = Box(boxes["ledge"]),
            Hurtboxes = hurtboxes.ToArray(),
        };
        root.RejectUnknownKeys();
        return character;
    }

    public static Stage LoadStage(ReadOnlySpan<byte> utf8, string source)
    {
        var root = JsonData.Parse(utf8, source);
        var platforms = new List<Platform>();
        foreach (var p in root["platforms"].Items)
            platforms.Add(new Platform(p["left"].Fixed, p["right"].Fixed, p["top"].Fixed, p["bottom"].Fixed,
                p["solid"].Bool, p["grabbableLedges"].Bool));
        var spawns = new List<SpawnPoint>();
        foreach (var s in root["spawns"].Items)
            spawns.Add(new SpawnPoint(s["x"].Fixed, s["y"].Fixed, s["facing"].Int));
        var blast = root["blastZone"];

        var stage = new Stage
        {
            Name = root["name"].String,
            Platforms = platforms.ToArray(),
            Spawns = spawns.ToArray(),
            BlastZone = new Rect(blast["left"].Fixed, blast["right"].Fixed, blast["bottom"].Fixed, blast["top"].Fixed),
        };
        root.RejectUnknownKeys();
        return stage;
    }

    /// <summary>The Game's data as loaded from a data folder.</summary>
    public sealed record GameData(MatchSettings Settings, Stage Stage, CharacterAttributes[] Characters);

    /// <summary>Loads settings.json, one Stage and the named Character files from <paramref name="folder"/>.</summary>
    public static GameData LoadFolder(string folder, string stageFile, params string[] characterFiles)
    {
        T Load<T>(string relative, Func<byte[], string, T> parse)
        {
            var path = Path.Combine(folder, relative);
            return parse(File.ReadAllBytes(path), relative);
        }
        var settings = Load("settings.json", (b, s) => LoadSettings(b, s));
        var stage = Load(stageFile, (b, s) => LoadStage(b, s));
        var characters = characterFiles.Select(f => Load(f, (b, s) => LoadCharacter(b, s))).ToArray();
        return new GameData(settings, stage, characters);
    }
}
