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
    /// Displays additional ship-specific fields when a ship is selected.
    /// Displays station-specific fields when a station is selected.
    /// Shows SOI info for bodies and dominant body for ships.
    /// Shows docking info for ships and stations.
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
        private VisualElement _shipSOIRow;
        private Label _soiBodyValue;

        // Ship docking UI elements.
        private VisualElement _shipDockedAtRow;
        private Label _dockedAtValue;
        private VisualElement _shipDockedPortRow;
        private Label _dockedPortValue;

        // Station-specific UI elements.
        private VisualElement _stationKindRow;
        private Label _stationKindValue;
        private VisualElement _attachmentRow;
        private Label _attachmentValue;

        // Station docking UI elements.
        private VisualElement _stationPortsRow;
        private Label _stationPortsValue;
        private VisualElement _stationOccupiedRow;
        private Label _stationOccupiedValue;

        // SOI radius row (for non-ship bodies with SOI).
        private VisualElement _soiRadiusRow;
        private Label _soiRadiusValue;

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

            SetLabel(_root, "details-name-label", UIStrings.Get("panel.details.name"));
            SetLabel(_root, "details-type-label", UIStrings.Get("panel.details.type"));
            SetLabel(_root, "details-radius-label", UIStrings.Get("panel.details.radius"));
            SetLabel(_root, "details-parent-label", UIStrings.Get("panel.details.parent"));

            // Ship rows.
            _shipRoleRow = _root.Q<VisualElement>("details-role-row");
            _roleValue = _root.Q<Label>("details-role-value");
            _shipClassRow = _root.Q<VisualElement>("details-class-row");
            _classValue = _root.Q<Label>("details-class-value");
            _shipStateRow = _root.Q<VisualElement>("details-state-row");
            _stateValue = _root.Q<Label>("details-state-value");
            _shipDestRow = _root.Q<VisualElement>("details-dest-row");
            _destValue = _root.Q<Label>("details-dest-value");
            _shipSOIRow = _root.Q<VisualElement>("details-soi-row");
            _soiBodyValue = _root.Q<Label>("details-soi-value");

            SetLabel(_root, "details-role-label", UIStrings.Get("panel.details.role"));
            SetLabel(_root, "details-class-label", UIStrings.Get("panel.details.ship_class"));
            SetLabel(_root, "details-state-label", UIStrings.Get("panel.details.state"));
            SetLabel(_root, "details-dest-label", UIStrings.Get("panel.details.destination"));
            SetLabel(_root, "details-soi-label", UIStrings.Get("panel.details.soi_body"));

            // Ship docking rows.
            _shipDockedAtRow = _root.Q<VisualElement>("details-docked-at-row");
            _dockedAtValue = _root.Q<Label>("details-docked-at-value");
            _shipDockedPortRow = _root.Q<VisualElement>("details-docked-port-row");
            _dockedPortValue = _root.Q<Label>("details-docked-port-value");

            SetLabel(_root, "details-docked-at-label", UIStrings.Get("panel.details.docked_at"));
            SetLabel(_root, "details-docked-port-label", UIStrings.Get("panel.details.docking_port"));

            // Station rows.
            _stationKindRow = _root.Q<VisualElement>("details-stationkind-row");
            _stationKindValue = _root.Q<Label>("details-stationkind-value");
            _attachmentRow = _root.Q<VisualElement>("details-attachment-row");
            _attachmentValue = _root.Q<Label>("details-attachment-value");

            SetLabel(_root, "details-stationkind-label", UIStrings.Get("panel.details.station_kind"));
            SetLabel(_root, "details-attachment-label", UIStrings.Get("panel.details.attachment"));

            // Station docking rows.
            _stationPortsRow = _root.Q<VisualElement>("details-station-ports-row");
            _stationPortsValue = _root.Q<Label>("details-station-ports-value");
            _stationOccupiedRow = _root.Q<VisualElement>("details-station-occupied-row");
            _stationOccupiedValue = _root.Q<Label>("details-station-occupied-value");

            SetLabel(_root, "details-station-ports-label", UIStrings.Get("panel.details.docking_ports"));
            SetLabel(_root, "details-station-occupied-label", UIStrings.Get("panel.details.ports_occupied"));

            // SOI radius row (for bodies).
            _soiRadiusRow = _root.Q<VisualElement>("details-soiradius-row");
            _soiRadiusValue = _root.Q<Label>("details-soiradius-value");
            SetLabel(_root, "details-soiradius-label", UIStrings.Get("panel.details.soi_radius"));

            ShowNoSelection();
        }

        private void Update()
        {
            if (!_currentSelectionId.IsValid) return;
            var body = _registry?.GetCelestialBody(_currentSelectionId);
            if (body == null) return;

            // Live update dynamic fields.
            if (body.ShipInfo != null)
                UpdateShipDynamicFields(body);
            if (body.StationInfo != null)
                UpdateStationDynamicFields(body);
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
            SetRowVisible(_shipSOIRow, isShip);
            SetRowVisible(_shipDockedAtRow, false);
            SetRowVisible(_shipDockedPortRow, false);

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

            // Station-specific fields.
            bool isStation = body.BodyType == CelestialBodyType.Station && body.StationInfo != null;

            SetRowVisible(_stationKindRow, isStation);
            SetRowVisible(_attachmentRow, isStation);
            SetRowVisible(_stationPortsRow, false);
            SetRowVisible(_stationOccupiedRow, false);

            if (isStation)
            {
                if (_stationKindValue != null)
                    _stationKindValue.text = UIStrings.GetStationKindName(body.StationInfo.Kind.ToString());
                if (_attachmentValue != null)
                    _attachmentValue.text = UIStrings.GetAttachmentModeName(body.AttachmentMode.ToString());

                UpdateStationDynamicFields(body);
            }

            // SOI radius for non-ship bodies.
            bool hasSOI = !isShip && body.SOIRadius.HasValue;
            SetRowVisible(_soiRadiusRow, hasSOI);
            if (hasSOI && _soiRadiusValue != null)
            {
                _soiRadiusValue.text = $"{body.SOIRadius.Value:F1} Mm";
            }
        }

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

            // Docking info for ships.
            bool isDocked = info.State == ShipState.Docked && info.DockedAtStationId.IsValid;
            bool isApproaching = info.State == ShipState.ApproachingStation && info.DockedAtStationId.IsValid;

            SetRowVisible(_shipDockedAtRow, isDocked || isApproaching);
            SetRowVisible(_shipDockedPortRow, isDocked);

            if ((isDocked || isApproaching) && _dockedAtValue != null)
            {
                var station = _registry?.GetCelestialBody(info.DockedAtStationId);
                _dockedAtValue.text = station != null ? station.DisplayName : info.DockedAtStationId.ToString();
            }

            if (isDocked && _dockedPortValue != null)
            {
                _dockedPortValue.text = $"#{info.DockedPortId}";
            }

            // Update dominant SOI body display.
            if (_soiBodyValue != null)
            {
                if (info.CurrentSOIBodyId.IsValid)
                {
                    var soiBody = _registry?.GetCelestialBody(info.CurrentSOIBodyId);
                    _soiBodyValue.text = soiBody != null ? soiBody.DisplayName : info.CurrentSOIBodyId.ToString();
                }
                else
                {
                    _soiBodyValue.text = UIStrings.Get("panel.details.none");
                }
            }
        }

        private void UpdateStationDynamicFields(CelestialBody body)
        {
            if (body.StationInfo == null) return;

            bool hasDocking = body.StationInfo.HasDocking;
            SetRowVisible(_stationPortsRow, hasDocking);
            SetRowVisible(_stationOccupiedRow, hasDocking);

            if (hasDocking)
            {
                var docking = body.StationInfo.Docking;
                if (_stationPortsValue != null)
                    _stationPortsValue.text = docking.TotalPorts.ToString();
                if (_stationOccupiedValue != null)
                    _stationOccupiedValue.text = $"{docking.OccupiedCount} / {docking.TotalPorts}";
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
