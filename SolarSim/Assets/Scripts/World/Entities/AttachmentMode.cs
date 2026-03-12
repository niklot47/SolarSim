namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Describes how an object is positioned relative to its parent.
    /// </summary>
    public enum AttachmentMode
    {
        /// <summary>No parent relationship.</summary>
        None,

        /// <summary>Orbiting the parent body.</summary>
        Orbit,

        /// <summary>Attached to the surface of the parent body.</summary>
        Surface,

        /// <summary>Positioned in local space of the parent (e.g. docked).</summary>
        LocalSpace
    }
}
