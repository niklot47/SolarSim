using System;
using SpaceSim.Shared.Identifiers;

namespace SpaceSim.Simulation.Core
{
    /// <summary>
    /// Tracks the currently selected world entity.
    /// Pure C# — no UnityEngine dependency.
    /// </summary>
    public class SelectionService
    {
        /// <summary>Currently selected entity id. EntityId.None if nothing selected.</summary>
        public EntityId CurrentSelectionId { get; private set; } = EntityId.None;

        /// <summary>Raised when selection changes. Args: previous id, new id.</summary>
        public event Action<EntityId, EntityId> OnSelectionChanged;

        /// <summary>Whether anything is currently selected.</summary>
        public bool HasSelection => CurrentSelectionId.IsValid;

        /// <summary>Select an entity by id.</summary>
        public void Select(EntityId id)
        {
            if (CurrentSelectionId == id) return;

            var previous = CurrentSelectionId;
            CurrentSelectionId = id;
            OnSelectionChanged?.Invoke(previous, id);
        }

        /// <summary>Clear current selection.</summary>
        public void ClearSelection()
        {
            Select(EntityId.None);
        }
    }
}
