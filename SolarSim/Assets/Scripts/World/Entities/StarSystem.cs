using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Container representing a star system — a collection of celestial bodies
    /// with one or more root objects (typically stars).
    /// </summary>
    public class StarSystem
    {
        /// <summary>Unique system identifier.</summary>
        public EntityId SystemId { get; }

        /// <summary>Display name or localization key.</summary>
        public string DisplayName { get; set; }

        /// <summary>Localization key for system name.</summary>
        public string LocalizationKeyName { get; set; }

        /// <summary>Ids of root bodies (stars or free-floating objects).</summary>
        public List<EntityId> RootBodyIds { get; } = new List<EntityId>();

        /// <summary>All body ids in this system (flat list for quick enumeration).</summary>
        public List<EntityId> AllBodyIds { get; } = new List<EntityId>();

        public StarSystem(EntityId systemId, string displayName)
        {
            SystemId = systemId;
            DisplayName = displayName;
            LocalizationKeyName = "";
        }

        public StarSystem(string displayName)
            : this(EntityId.Generate(), displayName)
        {
        }

        /// <summary>
        /// Register a body as belonging to this system.
        /// If isRoot is true, also adds to RootBodyIds.
        /// </summary>
        public void AddBody(EntityId bodyId, bool isRoot = false)
        {
            if (!AllBodyIds.Contains(bodyId))
                AllBodyIds.Add(bodyId);

            if (isRoot && !RootBodyIds.Contains(bodyId))
                RootBodyIds.Add(bodyId);
        }

        public override string ToString()
        {
            return $"StarSystem[{SystemId}] \"{DisplayName}\" bodies={AllBodyIds.Count} roots={RootBodyIds.Count}";
        }
    }
}
