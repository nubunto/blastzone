namespace Blastzone.Simulation;

/// <summary>One Player's input for one Tick. The Simulation advances from these alone (ADR 0001).</summary>
public struct PlayerInput
{
    /// <summary>Horizontal stick, -127 (left) to 127 (right).</summary>
    public sbyte StickX;
    /// <summary>Vertical stick, -127 (down) to 127 (up).</summary>
    public sbyte StickY;
    /// <summary>One bit per logical button, see <see cref="InputButtons"/>.</summary>
    public ushort Buttons;

    public readonly bool Holds(ushort button) => (Buttons & button) != 0;
}

/// <summary>Bits of <see cref="PlayerInput.Buttons"/>. Devices are mapped to these by the Engine.</summary>
public static class InputButtons
{
    public const ushort Jump = 1 << 0;
    /// <summary>Unused until milestone 3 (moves).</summary>
    public const ushort Attack = 1 << 1;
    /// <summary>Unused until milestone 3 (moves).</summary>
    public const ushort Special = 1 << 2;
    /// <summary>Air dodge in the air, roll from a ledge. Shields come in a later brief.</summary>
    public const ushort Shield = 1 << 3;
}
