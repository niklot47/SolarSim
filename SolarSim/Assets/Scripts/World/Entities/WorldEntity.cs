using SpaceSim.Shared.Identifiers;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Base class for all world objects (planets, ships, stations, etc.).
    /// Keeps minimal footprint — only identity and display name.
    /// </summary>
    public class WorldEntity
    {
        /// <summary>
        /// Unique identifier for this entity.
        /// </summary>
        public EntityId Id { get; }

        /// <summary>
        /// Human-readable display name (localization key or raw name).
        /// </summary>
        public string DisplayName { get; set; }

        public WorldEntity(EntityId id, string displayName = "")
        {
            Id = id;
            DisplayName = displayName;
        }

        /// <summary>
        /// Create a new entity with an auto-generated id.
        /// </summary>
        public WorldEntity(string displayName = "")
            : this(EntityId.Generate(), displayName)
        {
        }

        public override string ToString()
        {
            return $"{GetType().Name}[{Id}] \"{DisplayName}\"";
        }
    }
}
