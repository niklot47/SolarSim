namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Type of a single route segment in the patched-conics trajectory model.
    /// Each segment represents one phase of travel through a specific reference frame.
    ///
    /// Segment sequence for a typical interplanetary transfer:
    ///   LocalOrbitDeparture → SOIExit → HeliocentricTransfer → SOIEntry → OrbitInsertion
    ///
    /// For same-parent transfers (e.g. Terra → Luna):
    ///   LocalOrbitDeparture → LocalTransfer → OrbitInsertion
    ///
    /// Phase 26a — data model only. Movement execution in Phase 26b.
    /// </summary>
    public enum SegmentType
    {
        /// <summary>
        /// Ship departs from orbit around the origin body.
        /// Reference frame: origin body.
        /// Ends at the edge of the origin body's SOI (or at a local transfer point).
        /// </summary>
        LocalOrbitDeparture,

        /// <summary>
        /// Ship crosses the SOI boundary of the origin body outward.
        /// Reference frame: origin body's parent (one level up in hierarchy).
        /// Transition segment — may be very short or zero-length for simple cases.
        /// </summary>
        SOIExit,

        /// <summary>
        /// Ship travels in heliocentric (star-centered) or parent-centered frame
        /// between two SOI boundaries.
        /// Reference frame: common parent body (usually the star).
        /// This is the main interplanetary cruise segment.
        /// </summary>
        HeliocentricTransfer,

        /// <summary>
        /// Ship crosses the SOI boundary of the destination body inward.
        /// Reference frame: destination body.
        /// Transition segment — may be very short or zero-length for simple cases.
        /// </summary>
        SOIEntry,

        /// <summary>
        /// Ship transfers within the same parent body's SOI.
        /// Reference frame: the shared parent body.
        /// Used for same-parent transfers (e.g. two moons of the same planet,
        /// or a planet-to-moon transfer within the planet's SOI).
        /// </summary>
        LocalTransfer,

        /// <summary>
        /// Ship approaches the destination body and settles into orbit.
        /// Reference frame: destination body (arrival parent).
        /// Final segment — ends when ship enters stable orbit.
        /// </summary>
        OrbitInsertion
    }
}
