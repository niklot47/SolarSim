using System;
using System.Collections.Generic;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Cargo storage for a station. Tracks resource amounts.
    /// Attached to StationInfo when station has storage capability.
    /// Pure data object — no Unity dependency.
    /// </summary>
    public class StationStorage
    {
        /// <summary>Current resource amounts at this station.</summary>
        private readonly Dictionary<ResourceType, double> _resources = new Dictionary<ResourceType, double>();

        /// <summary>Maximum capacity per resource type. 0 = unlimited for now.</summary>
        public double CapacityPerResource { get; set; }

        public StationStorage(double capacityPerResource = 0.0)
        {
            CapacityPerResource = capacityPerResource;
        }

        /// <summary>Get the amount of a specific resource.</summary>
        public double GetAmount(ResourceType type)
        {
            return _resources.TryGetValue(type, out double amount) ? amount : 0.0;
        }

        /// <summary>Set the amount of a specific resource directly.</summary>
        public void SetAmount(ResourceType type, double amount)
        {
            if (amount < 0.0) amount = 0.0;
            _resources[type] = amount;
        }

        /// <summary>
        /// Add a quantity of resource to storage.
        /// Returns the actual amount added (may be less if capacity limited).
        /// </summary>
        public double Add(ResourceType type, double amount)
        {
            if (amount <= 0.0) return 0.0;

            double current = GetAmount(type);
            double toAdd = amount;

            if (CapacityPerResource > 0.0)
            {
                double space = CapacityPerResource - current;
                if (space <= 0.0) return 0.0;
                toAdd = Math.Min(toAdd, space);
            }

            _resources[type] = current + toAdd;
            return toAdd;
        }

        /// <summary>
        /// Remove a quantity of resource from storage.
        /// Returns the actual amount removed (may be less if not enough).
        /// </summary>
        public double Remove(ResourceType type, double amount)
        {
            if (amount <= 0.0) return 0.0;

            double current = GetAmount(type);
            double toRemove = Math.Min(amount, current);
            _resources[type] = current - toRemove;
            return toRemove;
        }

        /// <summary>Check if the station has any of a resource.</summary>
        public bool Has(ResourceType type)
        {
            return GetAmount(type) > 0.0;
        }

        /// <summary>Get a read-only snapshot of all resources.</summary>
        public Dictionary<ResourceType, double> GetAll()
        {
            return new Dictionary<ResourceType, double>(_resources);
        }

        /// <summary>Get all resource types that have non-zero amounts.</summary>
        public IEnumerable<ResourceType> GetNonEmptyTypes()
        {
            foreach (var kvp in _resources)
            {
                if (kvp.Value > 0.0)
                    yield return kvp.Key;
            }
        }

        public override string ToString()
        {
            if (_resources.Count == 0) return "StationStorage[empty]";
            var parts = new List<string>();
            foreach (var kvp in _resources)
            {
                if (kvp.Value > 0.0)
                    parts.Add($"{kvp.Key}={kvp.Value:F0}");
            }
            return $"StationStorage[{string.Join(", ", parts)}]";
        }
    }
}
