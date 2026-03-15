using System;
using System.Collections.Generic;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Cargo hold for a ship. Tracks resource amounts and enforces capacity.
    /// Attached to ShipInfo when ship has cargo capability.
    /// Pure data object — no Unity dependency.
    /// </summary>
    public class ShipCargo
    {
        /// <summary>Current cargo contents.</summary>
        private readonly Dictionary<ResourceType, double> _cargo = new Dictionary<ResourceType, double>();

        /// <summary>Maximum total cargo capacity (sum of all resources).</summary>
        public double Capacity { get; set; }

        public ShipCargo(double capacity = 100.0)
        {
            Capacity = capacity;
        }

        /// <summary>Get the amount of a specific resource in cargo.</summary>
        public double GetAmount(ResourceType type)
        {
            return _cargo.TryGetValue(type, out double amount) ? amount : 0.0;
        }

        /// <summary>Total amount of all resources currently in cargo.</summary>
        public double TotalUsed
        {
            get
            {
                double total = 0.0;
                foreach (var kvp in _cargo)
                    total += kvp.Value;
                return total;
            }
        }

        /// <summary>Remaining free cargo space.</summary>
        public double FreeSpace => Math.Max(0.0, Capacity - TotalUsed);

        /// <summary>Whether the cargo hold is completely full.</summary>
        public bool IsFull => FreeSpace <= 0.001;

        /// <summary>Whether the cargo hold is empty.</summary>
        public bool IsEmpty => TotalUsed <= 0.001;

        /// <summary>
        /// Add a quantity of resource to cargo.
        /// Returns the actual amount added (limited by free space).
        /// </summary>
        public double Add(ResourceType type, double amount)
        {
            if (amount <= 0.0) return 0.0;

            double space = FreeSpace;
            if (space <= 0.0) return 0.0;

            double toAdd = Math.Min(amount, space);
            double current = GetAmount(type);
            _cargo[type] = current + toAdd;
            return toAdd;
        }

        /// <summary>
        /// Remove a quantity of resource from cargo.
        /// Returns the actual amount removed.
        /// </summary>
        public double Remove(ResourceType type, double amount)
        {
            if (amount <= 0.0) return 0.0;

            double current = GetAmount(type);
            double toRemove = Math.Min(amount, current);
            _cargo[type] = current - toRemove;

            // Clean up zero entries.
            if (_cargo[type] <= 0.001)
                _cargo.Remove(type);

            return toRemove;
        }

        /// <summary>Remove all cargo of a specific type. Returns amount removed.</summary>
        public double RemoveAll(ResourceType type)
        {
            double current = GetAmount(type);
            if (current <= 0.0) return 0.0;
            _cargo.Remove(type);
            return current;
        }

        /// <summary>Check if the ship has any of a resource.</summary>
        public bool Has(ResourceType type)
        {
            return GetAmount(type) > 0.001;
        }

        /// <summary>Get a read-only snapshot of all cargo.</summary>
        public Dictionary<ResourceType, double> GetAll()
        {
            return new Dictionary<ResourceType, double>(_cargo);
        }

        /// <summary>Get all resource types that have non-zero amounts.</summary>
        public IEnumerable<ResourceType> GetNonEmptyTypes()
        {
            foreach (var kvp in _cargo)
            {
                if (kvp.Value > 0.001)
                    yield return kvp.Key;
            }
        }

        public override string ToString()
        {
            if (_cargo.Count == 0) return $"ShipCargo[empty cap={Capacity:F0}]";
            var parts = new List<string>();
            foreach (var kvp in _cargo)
            {
                if (kvp.Value > 0.001)
                    parts.Add($"{kvp.Key}={kvp.Value:F0}");
            }
            return $"ShipCargo[{string.Join(", ", parts)} used={TotalUsed:F0}/{Capacity:F0}]";
        }
    }
}
