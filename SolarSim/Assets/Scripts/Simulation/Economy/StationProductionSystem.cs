using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Economy
{
    /// <summary>
    /// Ticks production on all stations that have a production recipe.
    /// Each tick: accumulates progress, checks inputs, consumes inputs,
    /// produces output, respects storage capacity.
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    ///
    /// Update order in coordinator:
    ///   ShipMovementSystem → DockingSystem → NPCShipScheduler → StationProductionSystem → SOIResolver
    /// </summary>
    public class StationProductionSystem
    {
        private readonly WorldRegistry _registry;

        /// <summary>Total production cycles completed across all stations since start.</summary>
        public int TotalCyclesCompleted { get; private set; }

        /// <summary>
        /// Fired when a production cycle completes.
        /// Args: stationId, outputResource, outputAmount, inputsConsumed (may be null).
        /// </summary>
        public event Action<EntityId, ResourceType, double, Dictionary<ResourceType, double>> OnProductionCycleCompleted;

        public StationProductionSystem(WorldRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>
        /// Update production on all stations. Call once per simulation tick.
        /// </summary>
        /// <param name="simTime">Current simulation time in sim-seconds.</param>
        public void Update(double simTime)
        {
            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.BodyType != CelestialBodyType.Station) continue;
                if (body.StationInfo == null) continue;
                if (body.StationInfo.Production == null) continue;

                UpdateStation(body, simTime);
            }
        }

        private void UpdateStation(CelestialBody station, double simTime)
        {
            var prod = station.StationInfo.Production;
            if (!prod.HasRecipe) return;

            var recipe = prod.Recipe;
            var storage = station.StationInfo.Storage;
            if (storage == null) return;

            // Calculate elapsed time since last update.
            double dt = simTime - prod.LastUpdateTime;
            prod.LastUpdateTime = simTime;

            if (dt <= 0.0) return;

            // Check if output storage is full.
            if (storage.CapacityPerResource > 0.0 &&
                storage.GetAmount(recipe.OutputResource) >= storage.CapacityPerResource)
            {
                prod.IsStalled = true;
                prod.StallReason = "output_full";
                return;
            }

            // Check if all required inputs are available.
            if (recipe.HasInputs)
            {
                foreach (var input in recipe.InputsPerCycle)
                {
                    if (storage.GetAmount(input.Key) < input.Value)
                    {
                        prod.IsStalled = true;
                        prod.StallReason = $"need_{input.Key}";
                        return;
                    }
                }
            }

            // Inputs available (or none needed) — production proceeds.
            prod.IsStalled = false;
            prod.StallReason = "";

            // Accumulate progress.
            prod.Progress += dt;

            // Complete cycles.
            while (prod.Progress >= recipe.CycleTime)
            {
                // Re-check inputs before each cycle completion.
                if (recipe.HasInputs)
                {
                    bool hasAll = true;
                    foreach (var input in recipe.InputsPerCycle)
                    {
                        if (storage.GetAmount(input.Key) < input.Value)
                        {
                            hasAll = false;
                            break;
                        }
                    }
                    if (!hasAll)
                    {
                        // Not enough inputs for this cycle — stall with partial progress.
                        prod.IsStalled = true;
                        prod.StallReason = "need_inputs";
                        break;
                    }

                    // Consume inputs.
                    foreach (var input in recipe.InputsPerCycle)
                    {
                        storage.Remove(input.Key, input.Value);
                    }
                }

                // Re-check output capacity before producing.
                if (storage.CapacityPerResource > 0.0 &&
                    storage.GetAmount(recipe.OutputResource) + recipe.OutputAmountPerCycle > storage.CapacityPerResource)
                {
                    prod.IsStalled = true;
                    prod.StallReason = "output_full";
                    break;
                }

                // Produce output.
                storage.Add(recipe.OutputResource, recipe.OutputAmountPerCycle);
                prod.Progress -= recipe.CycleTime;
                prod.TotalCyclesCompleted++;
                TotalCyclesCompleted++;

                // Fire event for external logging (coordinator handles GameDebug).
                OnProductionCycleCompleted?.Invoke(
                    station.Id,
                    recipe.OutputResource,
                    recipe.OutputAmountPerCycle,
                    recipe.HasInputs ? recipe.InputsPerCycle : null);
            }
        }

        /// <summary>Get a human-readable status string.</summary>
        public string GetStatus()
        {
            int active = 0, stalled = 0, total = 0;
            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.StationInfo?.Production == null) continue;
                total++;
                if (body.StationInfo.Production.IsStalled)
                    stalled++;
                else
                    active++;
            }
            return $"Production: {total} stations ({active} active, {stalled} stalled), {TotalCyclesCompleted} cycles total";
        }
    }
}
