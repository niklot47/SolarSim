using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.World.ValueTypes;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Main world entity representing a celestial body or orbital object.
    /// Extends WorldEntity with orbital/physical properties.
    /// Parent-child hierarchy is stored as ids for loose coupling.
    /// Ships use BodyType.Ship and attach ShipInfo for role/class data.
    /// Stations use BodyType.Station and attach StationInfo for kind/surface data.
    /// </summary>
    public class CelestialBody : WorldEntity
    {
        /// <summary>Type classification of this body.</summary>
        public CelestialBodyType BodyType { get; set; }

        /// <summary>Id of the parent body. EntityId.None if root (e.g. a star).</summary>
        public EntityId ParentId { get; set; }

        /// <summary>Ids of child bodies (moons, stations, etc.).</summary>
        public List<EntityId> ChildIds { get; } = new List<EntityId>();

        /// <summary>How this body is attached to its parent.</summary>
        public AttachmentMode AttachmentMode { get; set; }

        /// <summary>Orbital parameters. Null if not orbiting (root body or surface station).</summary>
        public OrbitDefinition Orbit { get; set; }

        /// <summary>Self-rotation parameters.</summary>
        public SpinDefinition Spin { get; set; }

        /// <summary>Body radius in world units (for rendering scale).</summary>
        public double Radius { get; set; }

        /// <summary>Whether the player can select this object.</summary>
        public bool IsSelectable { get; set; }

        /// <summary>Whether this body has a landable surface (future use).</summary>
        public bool HasSurface { get; set; }

        /// <summary>Localization key for the display name (future localization).</summary>
        public string LocalizationKeyName { get; set; }

        /// <summary>
        /// Sphere of Influence radius in world units (Mm).
        /// Null means this body does not define an SOI (ships, surface stations, etc.).
        /// Used by SOIResolver to determine which body dominates a given position.
        /// </summary>
        public double? SOIRadius { get; set; }

        /// <summary>
        /// Ship-specific data. Non-null only when BodyType == Ship.
        /// Contains role, ship key, and ship class information.
        /// </summary>
        public ShipInfo ShipInfo { get; set; }

        /// <summary>
        /// Station-specific data. Non-null only when BodyType == Station.
        /// Contains station kind (orbital/surface) and surface coordinates.
        /// </summary>
        public StationInfo StationInfo { get; set; }

        public CelestialBody(EntityId id, string displayName, CelestialBodyType bodyType)
            : base(id, displayName)
        {
            BodyType = bodyType;
            ParentId = EntityId.None;
            AttachmentMode = AttachmentMode.None;
            Orbit = null;
            Spin = new SpinDefinition();
            Radius = 1.0;
            IsSelectable = true;
            HasSurface = false;
            LocalizationKeyName = "";
            SOIRadius = null;
            ShipInfo = null;
            StationInfo = null;
        }

        public CelestialBody(string displayName, CelestialBodyType bodyType)
            : this(EntityId.Generate(), displayName, bodyType)
        {
        }

        /// <summary>
        /// Add a child body id to this body's children list.
        /// Does not modify the child itself — caller is responsible for setting ParentId.
        /// </summary>
        public void AddChildId(EntityId childId)
        {
            if (!ChildIds.Contains(childId))
                ChildIds.Add(childId);
        }

        /// <summary>
        /// Remove a child body id.
        /// </summary>
        public void RemoveChildId(EntityId childId)
        {
            ChildIds.Remove(childId);
        }

        public override string ToString()
        {
            string parent = ParentId.IsValid ? $" parent={ParentId}" : " ROOT";
            string soiStr = SOIRadius.HasValue ? $" SOI={SOIRadius.Value:F1}" : "";
            string shipStr = ShipInfo != null ? $" {ShipInfo}" : "";
            string stationStr = StationInfo != null ? $" {StationInfo}" : "";
            return $"{BodyType}[{Id}] \"{DisplayName}\"{parent} r={Radius:F2}{soiStr} children={ChildIds.Count}{shipStr}{stationStr}";
        }
    }
}
