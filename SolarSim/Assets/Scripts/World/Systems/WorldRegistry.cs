using System.Collections.Generic;
using System.Linq;
using SpaceSim.Shared.Identifiers;

namespace SpaceSim.World.Systems
{
    /// <summary>
    /// Central registry for world entities. Provides add/get/enumerate.
    /// Keeps entity storage simple and deterministic.
    /// </summary>
    public class WorldRegistry
    {
        private readonly Dictionary<EntityId, Entities.WorldEntity> _entities =
            new Dictionary<EntityId, Entities.WorldEntity>();

        /// <summary>Total registered entity count.</summary>
        public int Count => _entities.Count;

        /// <summary>Register an entity. Overwrites if id already exists.</summary>
        public void Add(Entities.WorldEntity entity)
        {
            _entities[entity.Id] = entity;
        }

        /// <summary>Remove an entity by id.</summary>
        public bool Remove(EntityId id)
        {
            return _entities.Remove(id);
        }

        /// <summary>Get any entity by id. Returns null if not found.</summary>
        public Entities.WorldEntity GetEntity(EntityId id)
        {
            _entities.TryGetValue(id, out var entity);
            return entity;
        }

        /// <summary>Get a celestial body by id. Returns null if not found or wrong type.</summary>
        public Entities.CelestialBody GetCelestialBody(EntityId id)
        {
            return GetEntity(id) as Entities.CelestialBody;
        }

        /// <summary>Check if an entity with this id exists.</summary>
        public bool Contains(EntityId id)
        {
            return _entities.ContainsKey(id);
        }

        /// <summary>Enumerate all entities.</summary>
        public IEnumerable<Entities.WorldEntity> AllEntities => _entities.Values;

        /// <summary>Enumerate all celestial bodies.</summary>
        public IEnumerable<Entities.CelestialBody> AllCelestialBodies =>
            _entities.Values.OfType<Entities.CelestialBody>();

        /// <summary>
        /// Get children of a celestial body by reading its ChildIds
        /// and resolving them through this registry.
        /// </summary>
        public List<Entities.CelestialBody> GetChildren(EntityId parentId)
        {
            var parent = GetCelestialBody(parentId);
            if (parent == null) return new List<Entities.CelestialBody>();

            var result = new List<Entities.CelestialBody>();
            foreach (var childId in parent.ChildIds)
            {
                var child = GetCelestialBody(childId);
                if (child != null)
                    result.Add(child);
            }
            return result;
        }

        /// <summary>Clear all entities.</summary>
        public void Clear()
        {
            _entities.Clear();
        }
    }
}
