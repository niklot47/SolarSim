using UnityEngine;
using UnityEngine.UIElements;
using SpaceSim.Simulation.Core;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.UI.Localization;

// Resolve ambiguity with UnityEngine.EntityId (Unity 6+).
using EntityId = SpaceSim.Shared.Identifiers.EntityId;

namespace SpaceSim.UI.Panels
{
    /// <summary>
    /// UI Toolkit controller for the object details panel.
    /// Shows properties of the currently selected celestial body.
    /// </summary>
    public class ObjectDetailsPanelController : MonoBehaviour
    {
        private WorldRegistry _registry;
        private SelectionService _selectionService;

        private VisualElement _root;
        private Label _titleLabel;
        private Label _nameValue;
        private Label _typeValue;
        private Label _radiusValue;
        private Label _parentValue;
        private VisualElement _contentContainer;
        private Label _noSelectionLabel;

        public void Initialize(WorldRegistry registry, SelectionService selectionService)
        {
            _registry = registry;
            _selectionService = selectionService;

            if (_selectionService != null)
                _selectionService.OnSelectionChanged += OnSelectionChanged;
        }

        private void OnDestroy()
        {
            if (_selectionService != null)
                _selectionService.OnSelectionChanged -= OnSelectionChanged;
        }

        /// <summary>
        /// Build the UI from a shared root VisualElement.
        /// </summary>
        public void SetupUI(VisualElement root)
        {
            _root = root;
            if (_root == null) return;

            _titleLabel = _root.Q<Label>("details-title");
            if (_titleLabel != null)
                _titleLabel.text = UIStrings.Get("panel.details.title");

            _contentContainer = _root.Q<VisualElement>("details-content");
            _noSelectionLabel = _root.Q<Label>("details-no-selection");

            _nameValue = _root.Q<Label>("details-name-value");
            _typeValue = _root.Q<Label>("details-type-value");
            _radiusValue = _root.Q<Label>("details-radius-value");
            _parentValue = _root.Q<Label>("details-parent-value");

            // Set static labels from localization.
            SetLabel(_root, "details-name-label", UIStrings.Get("panel.details.name"));
            SetLabel(_root, "details-type-label", UIStrings.Get("panel.details.type"));
            SetLabel(_root, "details-radius-label", UIStrings.Get("panel.details.radius"));
            SetLabel(_root, "details-parent-label", UIStrings.Get("panel.details.parent"));

            // Start with no selection visible.
            ShowNoSelection();
        }

        private void OnSelectionChanged(EntityId previousId, EntityId newId)
        {
            if (!newId.IsValid)
            {
                ShowNoSelection();
                return;
            }

            var body = _registry?.GetCelestialBody(newId);
            if (body == null)
            {
                ShowNoSelection();
                return;
            }

            ShowDetails(body);
        }

        private void ShowDetails(CelestialBody body)
        {
            if (_contentContainer != null)
                _contentContainer.style.display = DisplayStyle.Flex;
            if (_noSelectionLabel != null)
                _noSelectionLabel.style.display = DisplayStyle.None;

            if (_nameValue != null)
                _nameValue.text = body.DisplayName;

            if (_typeValue != null)
                _typeValue.text = UIStrings.GetBodyTypeName(body.BodyType.ToString());

            if (_radiusValue != null)
                _radiusValue.text = body.Radius.ToString("F2");

            if (_parentValue != null)
            {
                if (body.ParentId.IsValid)
                {
                    var parent = _registry?.GetCelestialBody(body.ParentId);
                    _parentValue.text = parent != null ? parent.DisplayName : body.ParentId.ToString();
                }
                else
                {
                    _parentValue.text = UIStrings.Get("panel.details.none");
                }
            }
        }

        private void ShowNoSelection()
        {
            if (_contentContainer != null)
                _contentContainer.style.display = DisplayStyle.None;
            if (_noSelectionLabel != null)
            {
                _noSelectionLabel.style.display = DisplayStyle.Flex;
                _noSelectionLabel.text = UIStrings.Get("panel.details.no_selection");
            }
        }

        private static void SetLabel(VisualElement root, string name, string text)
        {
            var label = root.Q<Label>(name);
            if (label != null) label.text = text;
        }
    }
}
