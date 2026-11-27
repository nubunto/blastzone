namespace Blastzone.Simulation;

/// <summary>One Player's input for one Tick. The Simulation advances from these alone (ADR 0001).</summary>
public struct PlayerInput
{
    /// <summary>Horizontal stick, -127 (left) to 127 (right).</summary>
    public sbyte StickX;
    /// <summary>Vertical stick, -127 (down) to 127 (up).</summary>
    public sbyte StickY;
    /// <summary>One bit per button. Unused by the milestone 1 stub, but part of the input stream.</summary>
    public ushort Buttons;
}
