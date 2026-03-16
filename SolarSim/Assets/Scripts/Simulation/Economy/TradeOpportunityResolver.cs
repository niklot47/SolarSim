using System;
using System.Collections.Generic;
using SpaceSim.Shared.Identifiers;
using SpaceSim.Shared.Math;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;

namespace SpaceSim.Simulation.Economy
{
    /// <summary>
    /// Scans all stations and produces a scored list of trade opportunities.
    /// A trade opportunity exists when one station has surplus of a resource
    /// and another station has demand for it.
    ///
    /// Score formula: (demand * surplus) / (1 + distance)
    /// Higher score = more valuable trade route.
    ///
    /// Pure C# — no UnityEngine dependency. Lives in Simulation layer.
    /// </summary>
    public class TradeOpportunityResolver
    {
        private readonly WorldRegistry _registry;

        /// <summary>
        /// Minimum demand score required to consider a station as a valid destination.
        /// </summary>
        public double MinDemandScore { get; set; } = 5.0;

        /// <summary>
        /// Minimum surplus score required to consider a station as a valid source.
        /// </summary>
        public double MinSurplusScore { get; set; } = 5.0;

        /// <summary>
        /// Cached list of current trade opportunities, sorted by score descending.
        /// </summary>
        public List<TradeOpportunity> Opportunities { get; } = new List<TradeOpportunity>();

        // Internal caches.
        private readonly List<EntityId> _stationIds = new List<EntityId>();

        public TradeOpportunityResolver(WorldRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Rebuild the list of trade opportunities.
        /// Call after StationDemandEvaluator.EvaluateAll().
        /// Optionally provide a position resolver for distance calculation.
        /// </summary>
        public void Resolve(Func<EntityId, double, SimVec3> positionResolver = null, double simTime = 0.0)
        {
            Opportunities.Clear();
            RebuildStationList();

            if (_stationIds.Count < 2) return;

            // For each pair of stations, check if surplus at A matches demand at B.
            for (int i = 0; i < _stationIds.Count; i++)
            {
                var sourceId = _stationIds[i];
                var source = _registry.GetCelestialBody(sourceId);
                if (source?.StationInfo?.Demand == null) continue;

                var sourceDemand = source.StationInfo.Demand;

                foreach (var res in sourceDemand.GetSurplusResources())
                {
                    double surplusScore = sourceDemand.GetSurplusScore(res);
                    if (surplusScore < MinSurplusScore) continue;

                    for (int j = 0; j < _stationIds.Count; j++)
                    {
                        if (i == j) continue;

                        var destId = _stationIds[j];
                        var dest = _registry.GetCelestialBody(destId);
                        if (dest?.StationInfo?.Demand == null) continue;

                        double demandScore = dest.StationInfo.Demand.GetDemandScore(res);
                        if (demandScore < MinDemandScore) continue;

                        // Compute distance for scoring.
                        double distance = 1.0;
                        if (positionResolver != null)
                        {
                            SimVec3 srcPos = positionResolver(sourceId, simTime);
                            SimVec3 dstPos = positionResolver(destId, simTime);
                            distance = SimVec3.Distance(srcPos, dstPos);
                            if (distance < 1.0) distance = 1.0;
                        }

                        // Score: demand * surplus / distance.
                        double score = (demandScore * surplusScore) / distance;

                        Opportunities.Add(new TradeOpportunity
                        {
                            Resource = res,
                            SourceStationId = sourceId,
                            DestinationStationId = destId,
                            Score = score
                        });
                    }
                }
            }

            // Sort by score descending — best opportunities first.
            Opportunities.Sort((a, b) => b.Score.CompareTo(a.Score));
        }

        /// <summary>
        /// Find the best opportunity for a trader ship, optionally excluding
        /// stations that are already targeted by other traders.
        /// Returns null if no suitable opportunity exists.
        /// </summary>
        public TradeOpportunity? FindBestForTrader(
            EntityId shipId,
            EntityId currentLocationId,
            HashSet<EntityId> excludeSourceStations = null)
        {
            for (int i = 0; i < Opportunities.Count; i++)
            {
                var opp = Opportunities[i];

                // Skip if source station is excluded (already being served).
                if (excludeSourceStations != null && excludeSourceStations.Contains(opp.SourceStationId))
                    continue;

                return opp;
            }

            return null;
        }

        /// <summary>
        /// Get a human-readable status string.
        /// </summary>
        public string GetStatus()
        {
            return $"TradeOpportunities: {Opportunities.Count} routes across {_stationIds.Count} stations";
        }

        private void RebuildStationList()
        {
            _stationIds.Clear();
            foreach (var body in _registry.AllCelestialBodies)
            {
                if (body.BodyType != CelestialBodyType.Station) continue;
                if (body.StationInfo == null) continue;
                if (!body.StationInfo.HasDocking) continue;
                if (!body.StationInfo.HasStorage) continue;

                _stationIds.Add(body.Id);
            }
        }
    }
}
