using System.Text;
using Blastzone.Engine.Data;
using Blastzone.Simulation;
using static Blastzone.Tests.TestData;

namespace Blastzone.Tests.Data;

public class GameDataLoaderTests
{
    static string GameData(string relative) => Path.Combine(AppContext.BaseDirectory, "GameData", relative);
    static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);

    static CharacterAttributes Kestrel => GameDataLoader.LoadCharacter(File.ReadAllBytes(GameData("Characters/kestrel.json")), "kestrel.json");
    static CharacterAttributes Bastion => GameDataLoader.LoadCharacter(File.ReadAllBytes(GameData("Characters/bastion.json")), "bastion.json");
    static MatchSettings GameSettings => GameDataLoader.LoadSettings(File.ReadAllBytes(GameData("settings.json")), "settings.json");

    [Fact]
    public void The_shipped_characters_load_with_exact_fixed_point_values()
    {
        var k = Kestrel;
        Assert.Equal("Kestrel", k.Name);
        Assert.Equal(F("1.5"), k.WalkSpeed);
        Assert.Equal(F("0.23"), k.Gravity);
        Assert.Equal(F("4.5"), k.Hurtboxes[0].Radius);
        Assert.Equal(new BoxSize(F(6), F(14)), k.LedgeBox);
        Assert.True(k.CanWallJump);
    }

    [Fact]
    public void Kestrel_is_fast_and_light_with_three_jumps_and_bastion_heavy_with_one_double_jump()
    {
        var k = Kestrel;
        var b = Bastion;
        Assert.True(k.Weight < b.Weight);
        Assert.True(k.RunSpeed > b.RunSpeed);
        Assert.True(k.WalkSpeed > b.WalkSpeed);
        Assert.Equal(2, k.AirJumps); // 3 jumps in all
        Assert.Equal(1, b.AirJumps);
        Assert.False(b.CanWallJump);
        Assert.Equal(2, b.Hurtboxes.Length);
    }

    [Fact]
    public void The_shipped_settings_load_and_pick_the_melee_air_dodge()
    {
        var s = GameSettings;
        Assert.Equal(4, s.MaxCharacters);
        Assert.Equal(Melee, s.AirDodge);
        Assert.Equal(new BufferWindows { Jump = 6, Shield = 6, Dash = 6, Down = 6 }, s.Buffer);
        Assert.False(s.Ledge.Trump);
        Assert.Equal(F("1.6"), s.TeeterMaxSpeed);
    }

    [Fact]
    public void The_shipped_stage_loads_with_its_ledges()
    {
        var stage = GameDataLoader.LoadStage(File.ReadAllBytes(GameData("Stages/proving-grounds.json")), "proving-grounds.json");

        Assert.Equal(4, stage.Platforms.Length);
        Assert.True(stage.Platforms[0].Solid);
        Assert.All(stage.Platforms[1..], p => Assert.False(p.Solid));
        Assert.Equal(2, stage.Ledges.Length);
        Assert.Equal(new Ledge(F(-70), F(0), -1, 0), stage.Ledges[0]);
        Assert.Equal(4, stage.Spawns.Length);
    }

    [Fact]
    public void A_match_runs_on_the_shipped_data()
    {
        var stage = GameDataLoader.LoadStage(File.ReadAllBytes(GameData("Stages/proving-grounds.json")), "stage");
        var match = new Match(GameSettings, stage, [Kestrel, Bastion]);

        for (var t = 0; t < 120; t++)
            match.Step([In.Right, In.Left]);

        Assert.True(match.State.Characters[0].PositionX > stage.Spawns[0].X);
        Assert.True(match.State.Characters[1].PositionX < stage.Spawns[1].X);
    }

    static string WithSettings(string airDodge, string buffer) => $$"""
        {
          "maxCharacters": 4,
          "stick": { "walkThreshold": 24, "tapThreshold": 80, "tapOriginMax": 40, "downTapThreshold": 64, "ledgeThreshold": 64, "airDodgeThreshold": 40 },
          "bufferWindow": {{buffer}},
          {{airDodge}}
          "airDodgePresets": {
            "melee":    { "directional": true,  "zeroVelocityAtEnd": false, "helplessAfter": true },
            "ultimate": { "directional": false, "zeroVelocityAtEnd": true,  "helplessAfter": false }
          },
          "ledge": { "trump": true, "intangibilityTicks": 30, "regrabIntangibilityShrinkTicks": 8, "exitIntangibilityTicks": 5,
                     "regrabCooldownTicks": 20, "trumpVelocityX": 1.5, "trumpVelocityY": 1.5, "trumpStunTicks": 20, "trumpRegrabCooldownTicks": 30 },
          "teeterMaxSpeed": 1.6,
          "wallJumpReach": 2
        }
        """;

    [Fact]
    public void The_air_dodge_preset_is_chosen_by_name_and_defaults_to_melee()
    {
        var ultimate = GameDataLoader.LoadSettings(Bytes(WithSettings("\"airDodge\": \"ultimate\",", "{ \"default\": 6 }")), "s");
        Assert.Equal(Ultimate, ultimate.AirDodge);

        var fallback = GameDataLoader.LoadSettings(Bytes(WithSettings("", "{ \"default\": 6 }")), "s");
        Assert.Equal(Melee, fallback.AirDodge);

        var error = Assert.Throws<GameDataException>(() =>
            GameDataLoader.LoadSettings(Bytes(WithSettings("\"airDodge\": \"brawl\",", "{ \"default\": 6 }")), "s"));
        Assert.Contains("brawl", error.Message);
    }

    [Fact]
    public void Buffer_windows_default_and_can_be_overridden_per_action_type()
    {
        var s = GameDataLoader.LoadSettings(Bytes(WithSettings("", "{ \"default\": 6, \"jump\": 3, \"down\": 9 }")), "s");

        Assert.Equal(new BufferWindows { Jump = 3, Shield = 6, Dash = 6, Down = 9 }, s.Buffer);
    }

    [Fact]
    public void Unknown_keys_are_errors_so_typos_do_not_pass_silently()
    {
        var json = File.ReadAllText(GameData("Characters/kestrel.json")).Replace("\"walkSpeed\": 1.5,", "\"walkSpeed\": 1.5, \"walkSpeeed\": 2,");

        var error = Assert.Throws<GameDataException>(() => GameDataLoader.LoadCharacter(Bytes(json), "kestrel.json"));

        Assert.Contains("kestrel.json", error.Message);
        Assert.Contains("ground.walkSpeeed", error.Message);
    }

    [Fact]
    public void Missing_keys_are_errors()
    {
        var json = File.ReadAllText(GameData("Characters/kestrel.json")).Replace("\"traction\": 0.08", "\"tractionX\": 0.08");

        var error = Assert.Throws<GameDataException>(() => GameDataLoader.LoadCharacter(Bytes(json), "kestrel.json"));

        Assert.Contains("ground.traction", error.Message);
    }

    [Fact]
    public void Numbers_must_be_plain_decimals()
    {
        var json = File.ReadAllText(GameData("Characters/kestrel.json")).Replace("\"gravity\": 0.23", "\"gravity\": 2.3e-1");

        var error = Assert.Throws<GameDataException>(() => GameDataLoader.LoadCharacter(Bytes(json), "kestrel.json"));

        Assert.Contains("air.gravity", error.Message);
    }
}
