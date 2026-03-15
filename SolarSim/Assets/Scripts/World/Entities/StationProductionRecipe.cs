using System.Collections.Generic;

namespace SpaceSim.World.Entities
{
    /// <summary>
    /// Defines what a station produces and what inputs it requires.
    /// Pure data object — no Unity dependency.
    /// One recipe per station (simple 1:1 production for foundation).
    /// </summary>
    public class StationProductionRecipe
    {
        /// <summary>Resource type produced by this station.</summary>
        public ResourceType OutputResource { get; set; }

        /// <summary>Amount of output resource produced per completed cycle.</summary>
        public double OutputAmountPerCycle { get; set; }

        /// <summary>
        /// Required input resources per cycle.
        /// Key = resource type, Value = amount consumed per cycle.
        /// Empty dictionary = basic producer (no inputs needed).
        /// </summary>
        public Dictionary<ResourceType, double> InputsPerCycle { get; set; }
            = new Dictionary<ResourceType, double>();

        /// <summary>Duration of one production cycle in simulation seconds.</summary>
        public double CycleTime { get; set; }

        public StationProductionRecipe() { }

        public StationProductionRecipe(
            ResourceType output,
            double outputAmount,
            double cycleTime,
            Dictionary<ResourceType, double> inputs = null)
        {
            OutputResource = output;
            OutputAmountPerCycle = outputAmount;
            CycleTime = cycleTime;
            InputsPerCycle = inputs ?? new Dictionary<ResourceType, double>();
        }

        /// <summary>Whether this recipe requires input resources.</summary>
        public bool HasInputs => InputsPerCycle != null && InputsPerCycle.Count > 0;

        public override string ToString()
        {
            string inputStr = HasInputs ? $" inputs={InputsPerCycle.Count}" : " (basic)";
            return $"Recipe[{OutputResource} x{OutputAmountPerCycle:F0} every {CycleTime:F1}s{inputStr}]";
        }
    }
}
