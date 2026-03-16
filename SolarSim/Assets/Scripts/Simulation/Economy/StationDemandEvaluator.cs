using System;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Economy
{
    /// <summary>
    /// Evaluates demand and surplus scores for all stations in the world.
    /// Demand is derived from production input requirements and low storage levels.
    /// Surplus is derived from high storage levels and production output.
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    ///
    /// Scoring logic:
    /// - Demand for a resource increases when:
    ///   * Station's production recipe requires it as input AND storage is low
    ///   * Storage level is below the demand threshold
    /// - Surplus for a resource increases when:
    ///   * Storage level is above the surplus threshold
    ///   * Station produces this resource (output of production recipe)
    ///
    /// Thresholds are configurable constants.
    /// </summary>
    public class StationDemandEvaluator
    {
        private readonly WorldRegistry _registry;

        /// <summary>
        /// Below this amount, a production input resource is considered in demand.
        /// Relative to one cycle's consumption.
        /// </summary>
        public double InputDemandCycleMultiplier { get; set; } = 3.0;

        /// <summary>
        /// Above this amount, a resource is considered surplus.
        /// </summary>
        public double SurplusThreshold { get; set; } = 50.0;

        /// <summary>
        /// Below this amount, any resource is considered in demand
        /// (even if not a production input).
        /// </summary>
        public double GeneralDemandThreshold { get; set; } = 20.0;

        /// <summary>
        /// Maximum demand score (clamped).
        /// </summary>
        public double MaxDemandScore { get; set; } = 100.0;

        /// <summary>
        /// Maximum surplus score (clamped).
        /// </summary>
        public double MaxSurplusScore { get; set; } = 100.0;

        public StationDemandEvaluator(WorldRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Evaluate demand and surplus for all stations.
        /// Call periodically (not every frame — once per few simulation seconds is enough).
        /// Creates StationDemand on StationInfo if not present.
        /// </summary>
        public void EvaluateAll()
        {
            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.BodyType != CelestialBodyType.Station) continue;
                if (body.StationInfo == null) continue;

                EvaluateStation(body);
            }
        }

        /// <summary>
        /// Evaluate demand and surplus for a single station.
        /// </summary>
        public void EvaluateStation(CelestialBody station)
        {
            if (station.StationInfo == null) return;

            // Ensure demand object exists.
            if (station.StationInfo.Demand == null)
                station.StationInfo.Demand = new StationDemand();

            var demand = station.StationInfo.Demand;
            demand.Clear();

            var storage = station.StationInfo.Storage;
            if (storage == null) return;

            var production = station.StationInfo.Production;

            // Evaluate demand for production inputs.
            if (production != null && production.HasRecipe && production.Recipe.HasInputs)
            {
                foreach (var input in production.Recipe.InputsPerCycle)
                {
                    double currentAmount = storage.GetAmount(input.Key);
                    double neededPerCycle = input.Value;
                    double threshold = neededPerCycle * InputDemandCycleMultiplier;

                    if (currentAmount < threshold)
                    {
                        // Score is higher when storage is more depleted.
                        double ratio = 1.0 - (currentAmount / threshold);
                        double score = ratio * MaxDemandScore;
                        demand.DemandScores[input.Key] = Math.Min(score, MaxDemandScore);
                    }
                }
            }

            // Evaluate general demand for low-stock resources.
            foreach (ResourceType res in Enum.GetValues(typeof(ResourceType)))
            {
                double amount = storage.GetAmount(res);

                // Skip if already scored by production input logic.
                if (demand.DemandScores.ContainsKey(res)) continue;

                if (amount < GeneralDemandThreshold && amount > 0.01)
                {
                    double ratio = 1.0 - (amount / GeneralDemandThreshold);
                    double score = ratio * MaxDemandScore * 0.3; // Lower priority than production inputs.
                    demand.DemandScores[res] = Math.Min(score, MaxDemandScore);
                }
            }

            // Evaluate surplus.
            foreach (ResourceType res in Enum.GetValues(typeof(ResourceType)))
            {
                double amount = storage.GetAmount(res);

                if (amount <= SurplusThreshold) continue;

                // Check if this resource is a production input — don't export it if needed.
                bool isProductionInput = false;
                if (production != null && production.HasRecipe && production.Recipe.HasInputs)
                {
                    isProductionInput = production.Recipe.InputsPerCycle.ContainsKey(res);
                }

                if (isProductionInput)
                {
                    // Only surplus if way above what production needs.
                    double neededPerCycle = production.Recipe.InputsPerCycle[res];
                    double safeReserve = neededPerCycle * InputDemandCycleMultiplier * 2.0;
                    if (amount <= safeReserve) continue;

                    double excess = amount - safeReserve;
                    double score = Math.Min(excess / SurplusThreshold * 50.0, MaxSurplusScore);
                    demand.SurplusScores[res] = score;
                }
                else
                {
                    // Non-input resource: surplus based on how much above threshold.
                    double excess = amount - SurplusThreshold;
                    double score = Math.Min(excess / SurplusThreshold * 50.0, MaxSurplusScore);

                    // Boost surplus score for production outputs.
                    bool isProductionOutput = production != null
                        && production.HasRecipe
                        && production.Recipe.OutputResource == res;
                    if (isProductionOutput)
                        score *= 1.5;

                    demand.SurplusScores[res] = Math.Min(score, MaxSurplusScore);
                }
            }
        }
    }
}
