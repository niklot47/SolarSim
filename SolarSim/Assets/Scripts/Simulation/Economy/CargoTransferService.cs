using System;
using SpaceSim.Shared.Identifiers;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Economy
{
    /// <summary>
    /// Result of a cargo transfer operation.
    /// </summary>
    public struct CargoTransferResult
    {
        /// <summary>Resource type transferred.</summary>
        public ResourceType Resource;

        /// <summary>Actual amount transferred.</summary>
        public double Amount;

        /// <summary>Whether any cargo was moved.</summary>
        public bool Success;

        /// <summary>Human-readable reason if transfer failed or was partial.</summary>
        public string Reason;

        public static CargoTransferResult Ok(ResourceType resource, double amount)
        {
            return new CargoTransferResult
            {
                Resource = resource,
                Amount = amount,
                Success = amount > 0.001,
                Reason = ""
            };
        }

        public static CargoTransferResult Fail(string reason)
        {
            return new CargoTransferResult
            {
                Resource = default,
                Amount = 0.0,
                Success = false,
                Reason = reason
            };
        }
    }

    /// <summary>
    /// Handles cargo transfer operations between ships and stations.
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    ///
    /// Transfer rules:
    /// - Ship must be Docked at the station.
    /// - Cannot exceed ship cargo capacity.
    /// - Cannot go negative on source.
    /// - Transfer is instant (no duration).
    /// </summary>
    public class CargoTransferService
    {
        private readonly WorldRegistry _registry;

        /// <summary>
        /// Fired when cargo is transferred.
        /// Args: shipId, stationId, resourceType, amount, direction ("load"/"unload").
        /// </summary>
        public event Action<EntityId, EntityId, ResourceType, double, string> OnCargoTransferred;

        public CargoTransferService(WorldRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Load cargo from station to ship.
        /// Ship must be docked at the station.
        /// </summary>
        public CargoTransferResult LoadFromStation(
            EntityId shipId, EntityId stationId, ResourceType resource, double requestedAmount)
        {
            var validation = ValidateTransfer(shipId, stationId);
            if (!validation.Success) return validation;

            var ship = _registry.GetCelestialBody(shipId);
            var station = _registry.GetCelestialBody(stationId);

            var cargo = ship.ShipInfo.Cargo;
            var storage = station.StationInfo.Storage;

            if (cargo == null) return CargoTransferResult.Fail("Ship has no cargo hold");
            if (storage == null) return CargoTransferResult.Fail("Station has no storage");

            // How much the station has.
            double available = storage.GetAmount(resource);
            if (available <= 0.001) return CargoTransferResult.Fail("Station has no " + resource);

            // How much the ship can take.
            double canTake = Math.Min(requestedAmount, available);
            double actualLoaded = cargo.Add(resource, canTake);

            if (actualLoaded <= 0.001) return CargoTransferResult.Fail("Ship cargo full");

            // Remove from station what was actually loaded.
            storage.Remove(resource, actualLoaded);

            OnCargoTransferred?.Invoke(shipId, stationId, resource, actualLoaded, "load");
            return CargoTransferResult.Ok(resource, actualLoaded);
        }

        /// <summary>
        /// Unload cargo from ship to station.
        /// Ship must be docked at the station.
        /// </summary>
        public CargoTransferResult UnloadToStation(
            EntityId shipId, EntityId stationId, ResourceType resource, double requestedAmount)
        {
            var validation = ValidateTransfer(shipId, stationId);
            if (!validation.Success) return validation;

            var ship = _registry.GetCelestialBody(shipId);
            var station = _registry.GetCelestialBody(stationId);

            var cargo = ship.ShipInfo.Cargo;
            var storage = station.StationInfo.Storage;

            if (cargo == null) return CargoTransferResult.Fail("Ship has no cargo hold");
            if (storage == null) return CargoTransferResult.Fail("Station has no storage");

            // How much the ship has.
            double available = cargo.GetAmount(resource);
            if (available <= 0.001) return CargoTransferResult.Fail("Ship has no " + resource);

            double toUnload = Math.Min(requestedAmount, available);
            double actualAdded = storage.Add(resource, toUnload);

            if (actualAdded <= 0.001) return CargoTransferResult.Fail("Station storage full");

            // Remove from ship what was actually unloaded.
            cargo.Remove(resource, actualAdded);

            OnCargoTransferred?.Invoke(shipId, stationId, resource, actualAdded, "unload");
            return CargoTransferResult.Ok(resource, actualAdded);
        }

        /// <summary>
        /// Unload ALL cargo from ship to station.
        /// Returns total amount unloaded across all resource types.
        /// </summary>
        public double UnloadAll(EntityId shipId, EntityId stationId)
        {
            var ship = _registry.GetCelestialBody(shipId);
            if (ship?.ShipInfo?.Cargo == null) return 0.0;

            double totalUnloaded = 0.0;
            var cargoSnapshot = ship.ShipInfo.Cargo.GetAll();

            foreach (var kvp in cargoSnapshot)
            {
                if (kvp.Value <= 0.001) continue;
                var result = UnloadToStation(shipId, stationId, kvp.Key, kvp.Value);
                if (result.Success) totalUnloaded += result.Amount;
            }

            return totalUnloaded;
        }

        /// <summary>
        /// Load any available resource from station to ship (fill up cargo).
        /// Returns total amount loaded.
        /// </summary>
        public double LoadAny(EntityId shipId, EntityId stationId)
        {
            var station = _registry.GetCelestialBody(stationId);
            if (station?.StationInfo?.Storage == null) return 0.0;

            var ship = _registry.GetCelestialBody(shipId);
            if (ship?.ShipInfo?.Cargo == null) return 0.0;

            double totalLoaded = 0.0;
            var storageSnapshot = station.StationInfo.Storage.GetAll();

            foreach (var kvp in storageSnapshot)
            {
                if (kvp.Value <= 0.001) continue;
                if (ship.ShipInfo.Cargo.IsFull) break;

                double toLoad = Math.Min(kvp.Value, ship.ShipInfo.Cargo.FreeSpace);
                var result = LoadFromStation(shipId, stationId, kvp.Key, toLoad);
                if (result.Success) totalLoaded += result.Amount;
            }

            return totalLoaded;
        }

        private CargoTransferResult ValidateTransfer(EntityId shipId, EntityId stationId)
        {
            var ship = _registry.GetCelestialBody(shipId);
            if (ship == null) return CargoTransferResult.Fail("Ship not found");
            if (ship.ShipInfo == null) return CargoTransferResult.Fail("Not a ship");
            if (ship.ShipInfo.State != ShipState.Docked)
                return CargoTransferResult.Fail("Ship not docked");
            if (ship.ShipInfo.DockedAtStationId != stationId)
                return CargoTransferResult.Fail("Ship not docked at this station");

            var station = _registry.GetCelestialBody(stationId);
            if (station == null) return CargoTransferResult.Fail("Station not found");
            if (station.StationInfo == null) return CargoTransferResult.Fail("Not a station");

            // Return a "valid" result — caller checks Success.
            return new CargoTransferResult { Success = true };
        }
    }
}
