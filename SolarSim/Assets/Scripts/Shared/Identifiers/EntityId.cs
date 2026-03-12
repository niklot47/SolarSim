using System;

namespace SpaceSim.Shared.Identifiers
{
    /// <summary>
    /// Strongly-typed immutable identifier for world entities.
    /// Does not depend on UnityEngine.
    /// </summary>
    public readonly struct EntityId : IEquatable<EntityId>
    {
        // Internal storage.
        public ulong Value { get; }

        // Thread-safe counter for generating unique ids.
        private static ulong _nextId = 1;
        private static readonly object _lock = new object();

        public EntityId(ulong value)
        {
            Value = value;
        }

        /// <summary>
        /// Generate a new unique EntityId. Thread-safe.
        /// </summary>
        public static EntityId Generate()
        {
            lock (_lock)
            {
                return new EntityId(_nextId++);
            }
        }

        /// <summary>
        /// Create an EntityId from an existing value (e.g. deserialization).
        /// </summary>
        public static EntityId FromRaw(ulong value)
        {
            return new EntityId(value);
        }

        /// <summary>
        /// Invalid / unassigned id sentinel.
        /// </summary>
        public static readonly EntityId None = new EntityId(0);

        public bool IsValid => Value != 0;

        // Equality.
        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => $"E:{Value}";

        public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;
        public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
    }
}
