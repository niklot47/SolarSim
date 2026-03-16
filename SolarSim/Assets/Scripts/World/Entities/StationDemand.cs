using System.Collections.Generic;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Tracks demand and surplus scores for resources at a station.
    /// Updated periodically by the demand evaluation system.
    /// Higher demand score = station needs this resource more urgently.
    /// Higher surplus score = station has excess of this resource.
    /// Pure data object — no Unity dependency.
    /// </summary>
    public class StationDemand
    {
        /// <summary>
        /// Demand scores per resource type.
        /// Higher value = station needs this resource more.
        /// 0 = no demand.
        /// </summary>
        public Dictionary<ResourceType, double> DemandScores { get; }
            = new Dictionary<ResourceType, double>();

        /// <summary>
        /// Surplus scores per resource type.
        /// Higher value = station has more excess available for export.
        /// 0 = no surplus.
        /// </summary>
        public Dictionary<ResourceType, double> SurplusScores { get; }
            = new Dictionary<ResourceType, double>();

        /// <summary>
        /// Get demand score for a resource. Returns 0 if not tracked.
        /// </summary>
        public double GetDemandScore(ResourceType type)
        {
            return DemandScores.TryGetValue(type, out double score) ? score : 0.0;
        }

        /// <summary>
        /// Get surplus score for a resource. Returns 0 if not tracked.
        /// </summary>
        public double GetSurplusScore(ResourceType type)
        {
            return SurplusScores.TryGetValue(type, out double score) ? score : 0.0;
        }

        /// <summary>
        /// Clear all scores for fresh recalculation.
        /// </summary>
        public void Clear()
        {
            DemandScores.Clear();
            SurplusScores.Clear();
        }

        /// <summary>
        /// Get all resource types with non-zero demand.
        /// </summary>
        public IEnumerable<ResourceType> GetDemandedResources()
        {
            foreach (var kvp in DemandScores)
            {
                if (kvp.Value > 0.01)
                    yield return kvp.Key;
            }
        }

        /// <summary>
        /// Get all resource types with non-zero surplus.
        /// </summary>
        public IEnumerable<ResourceType> GetSurplusResources()
        {
            foreach (var kvp in SurplusScores)
            {
                if (kvp.Value > 0.01)
                    yield return kvp.Key;
            }
        }

        public override string ToString()
        {
            var parts = new List<string>();
            foreach (var kvp in DemandScores)
            {
                if (kvp.Value > 0.01)
                    parts.Add($"D:{kvp.Key}={kvp.Value:F1}");
            }
            foreach (var kvp in SurplusScores)
            {
                if (kvp.Value > 0.01)
                    parts.Add($"S:{kvp.Key}={kvp.Value:F1}");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "no demand/surplus";
        }
    }
}
