namespace Blastzone.Simulation;

/// <summary>What a Character is doing. Stored as an int in <see cref="CharacterState.Action"/>.</summary>
public enum CharacterAction
{
    Idle,
    Walk,
    Dash,
    Run,
    RunTurn,
    Teeter,
    JumpSquat,
    Landing,
    Airborne,
    Helpless,
    AirDodge,
    LedgeHang,
    LedgeGetup,
    LedgeRoll,
    Trumped,
}

public static class CharacterActions
{
    public static bool IsGrounded(CharacterAction a) => a is CharacterAction.Idle or CharacterAction.Walk
        or CharacterAction.Dash or CharacterAction.Run or CharacterAction.RunTurn or CharacterAction.Teeter
        or CharacterAction.JumpSquat or CharacterAction.Landing;

    public static bool IsOnLedge(CharacterAction a) =>
        a is CharacterAction.LedgeHang or CharacterAction.LedgeGetup or CharacterAction.LedgeRoll;

    public static BoxKind Box(CharacterAction a) => a switch
    {
        CharacterAction.JumpSquat or CharacterAction.Landing => BoxKind.Crouching,
        CharacterAction.Airborne or CharacterAction.Helpless or CharacterAction.AirDodge or CharacterAction.Trumped => BoxKind.Airborne,
        CharacterAction.LedgeHang or CharacterAction.LedgeGetup or CharacterAction.LedgeRoll => BoxKind.Ledge,
        _ => BoxKind.Standing,
    };
}
