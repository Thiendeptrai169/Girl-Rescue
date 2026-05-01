namespace DragonRescue.Data
{
    /// <summary>
    /// Colors used to match cannons against dragon segments.
    /// Add new colors here — all systems read from this enum.
    /// </summary>
    public enum CannonColor
    {
        Blue,
        Green,
        Red,
        Yellow,
        Purple,
        Pink,
        Cyan,
        Brown
    }

    /// <summary>
    /// Types of movement available for the dragon.
    /// </summary>
    public enum DragonMovementType
    {
        Linear,
        Waypoint
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

    /// <summary>
    /// Direction an arrow block faces / moves toward.
    /// </summary>
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right,
        UpLeft,
        UpRight,
        DownLeft,
        DownRight
    }

    /// <summary>
    /// Types of boosters available to the player.
    /// </summary>
    public enum BoosterType
    {
        Unlock,
        Remove,
        Sort,
        Further
    }

    /// <summary>
    /// Modes for the Sort booster.
    /// </summary>
    public enum SortMode
    {
        BringUsefulColorsUp,
        ShuffleColumns
    }
}
