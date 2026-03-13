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
    /// Displays additional ship-specific fields when a ship is selected:
    /// role, class, state, and destination (if travelling).
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

        // Ship-specific UI elements.
        private VisualElement _shipRoleRow;
        private Label _roleValue;
        private VisualElement _shipClassRow;
        private Label _classValue;
        private VisualElement _shipStateRow;
        private Label _stateValue;
        private VisualElement _shipDestRow;
        private Label _destValue;

        // Track selected id for live updates during travel.
        private EntityId _currentSelectionId = EntityId.None;

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

            // Ship-specific rows.
            _shipRoleRow = _root.Q<VisualElement>("details-role-row");
            _roleValue = _root.Q<Label>("details-role-value");
            _shipClassRow = _root.Q<VisualElement>("details-class-row");
            _classValue = _root.Q<Label>("details-class-value");
            _shipStateRow = _root.Q<VisualElement>("details-state-row");
            _stateValue = _root.Q<Label>("details-state-value");
            _shipDestRow = _root.Q<VisualElement>("details-dest-row");
            _destValue = _root.Q<Label>("details-dest-value");

            SetLabel(_root, "details-role-label", UIStrings.Get("panel.details.role"));
            SetLabel(_root, "details-class-label", UIStrings.Get("panel.details.ship_class"));
            SetLabel(_root, "details-state-label", UIStrings.Get("panel.details.state"));
            SetLabel(_root, "details-dest-label", UIStrings.Get("panel.details.destination"));

            // Start with no selection visible.
            ShowNoSelection();
        }

        private void Update()
        {
            // Live update ship state/destination while selected.
            if (!_currentSelectionId.IsValid) return;
            var body = _registry?.GetCelestialBody(_currentSelectionId);
            if (body == null || body.ShipInfo == null) return;
            UpdateShipDynamicFields(body);
        }

        private void OnSelectionChanged(EntityId previousId, EntityId newId)
        {
            _currentSelectionId = newId;

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

            // Ship-specific fields.
            bool isShip = body.BodyType == CelestialBodyType.Ship && body.ShipInfo != null;

            SetRowVisible(_shipRoleRow, isShip);
            SetRowVisible(_shipClassRow, isShip);
            SetRowVisible(_shipStateRow, isShip);
            SetRowVisible(_shipDestRow, isShip);

            if (isShip)
            {
                if (_roleValue != null)
                    _roleValue.text = UIStrings.GetShipRoleName(body.ShipInfo.Role.ToString());
                if (_classValue != null)
                    _classValue.text = string.IsNullOrEmpty(body.ShipInfo.ShipClass)
                        ? UIStrings.Get("panel.details.none")
                        : body.ShipInfo.ShipClass;

                UpdateShipDynamicFields(body);
            }
        }

        /// <summary>
        /// Update state and destination labels — called each frame for live data.
        /// </summary>
        private void UpdateShipDynamicFields(CelestialBody body)
        {
            var info = body.ShipInfo;
            if (info == null) return;

            if (_stateValue != null)
                _stateValue.text = UIStrings.GetShipStateName(info.State.ToString());

            bool isTravelling = info.State == ShipState.Travelling && info.CurrentRoute != null;
            SetRowVisible(_shipDestRow, isTravelling);

            if (isTravelling && _destValue != null)
            {
                var dest = _registry?.GetCelestialBody(info.CurrentRoute.DestinationBodyId);
                _destValue.text = dest != null ? dest.DisplayName : info.CurrentRoute.DestinationBodyId.ToString();
            }
        }

        private void ShowNoSelection()
        {
            _currentSelectionId = EntityId.None;

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

        private static void SetRowVisible(VisualElement row, bool visible)
        {
            if (row != null)
                row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
