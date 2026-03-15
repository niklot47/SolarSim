namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Tracks production progress for a station.
    /// Attached to StationInfo when station has a production recipe.
    /// Pure data object — no Unity dependency.
    /// </summary>
    public class StationProductionState
    {
        /// <summary>The production recipe this station follows.</summary>
        public StationProductionRecipe Recipe { get; set; }

        /// <summary>Accumulated progress toward current cycle (in sim-seconds).</summary>
        public double Progress { get; set; }

        /// <summary>Last simulation time when production was updated.</summary>
        public double LastUpdateTime { get; set; }

        /// <summary>Whether production is currently stalled (missing inputs or output full).</summary>
        public bool IsStalled { get; set; }

        /// <summary>Human-readable reason for stall, if stalled.</summary>
        public string StallReason { get; set; }

        /// <summary>Total number of cycles completed since initialization.</summary>
        public int TotalCyclesCompleted { get; set; }

        public StationProductionState()
        {
            Recipe = null;
            Progress = 0.0;
            LastUpdateTime = 0.0;
            IsStalled = false;
            StallReason = "";
            TotalCyclesCompleted = 0;
        }

        public StationProductionState(StationProductionRecipe recipe, double startTime)
        {
            Recipe = recipe;
            Progress = 0.0;
            LastUpdateTime = startTime;
            IsStalled = false;
            StallReason = "";
            TotalCyclesCompleted = 0;
        }

        /// <summary>Whether this state has a valid recipe assigned.</summary>
        public bool HasRecipe => Recipe != null;

        /// <summary>Progress as fraction 0..1 of current cycle.</summary>
        public double ProgressFraction =>
            HasRecipe && Recipe.CycleTime > 0.0 ? Progress / Recipe.CycleTime : 0.0;

        public override string ToString()
        {
            string status = IsStalled ? $"STALLED({StallReason})" : $"{ProgressFraction:P0}";
            return $"ProdState[{status} cycles={TotalCyclesCompleted}]";
        }
    }
}
