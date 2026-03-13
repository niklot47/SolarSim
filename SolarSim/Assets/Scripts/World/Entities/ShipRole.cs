namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Role classification for ship entities.
    /// Determines default behavior patterns and UI display.
    /// </summary>
    public enum ShipRole
    {
        /// <summary>Player-controlled ship.</summary>
        Player,

        /// <summary>Trading vessel — cargo transport between stations/planets.</summary>
        Trader,

        /// <summary>Patrol ship — security and defense routes.</summary>
        Patrol,

        /// <summary>Civilian ship — general NPC traffic.</summary>
        Civilian
    }
}
