namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Current operational state of a ship.
    /// </summary>
    public enum ShipState
    {
        /// <summary>Ship is idle (no orbit, no route).</summary>
        Idle,

        /// <summary>Ship is orbiting its parent body.</summary>
        Orbiting,

        /// <summary>Ship is travelling between two bodies.</summary>
        Travelling,

        /// <summary>Ship has arrived at its destination.</summary>
        Arrived,

        /// <summary>Ship is approaching a station docking port.</summary>
        ApproachingStation,

        /// <summary>Ship is in the process of docking (final attachment).</summary>
        Docking,

        /// <summary>Ship is docked at a station.</summary>
        Docked
    }
}
