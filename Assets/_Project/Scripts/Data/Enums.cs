namespace DragonRescue.Data
{
    /// <summary>
    /// Colors used to match cannons against dragon segments.
    /// Add new colors here — all systems read from this enum.
    /// </summary>
    public enum CannonColor
    {
        Red,
        Blue,
        Green,
        Yellow,
        Purple
    }

    /// <summary>
    /// Top-level game state machine states.
    /// </summary>
    public enum GameState
    {
        Loading,
        Playing,
        Won,
        Lost,
        Paused
    }
}
