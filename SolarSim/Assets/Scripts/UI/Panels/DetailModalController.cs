using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.UI.Localization;

// Resolve ambiguity with UnityEngine.EntityId (Unity 6+).
using EntityId = SpaceSim.Shared.Identifiers.EntityId;

namespace SpaceSim.UI.Panels
{
    /// <summary>
    /// Controls the modal detail window that opens over the center of the screen.
    /// Shows full object information organized into tabs:
    /// Details, Market, Missions, Modules, Hangar.
    /// Currently only Details tab has content; others are placeholders.
    /// </summary>
    public class DetailModalController : MonoBehaviour
    {
        private WorldRegistry _registry;

        private VisualElement _root;
        private VisualElement _overlay;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Button _closeBtn;

        // Tab buttons.
        private Button _tabDetails;
        private Button _tabMarket;
        private Button _tabMissions;
        private Button _tabModules;
        private Button _tabHangar;

        // Tab content containers.
        private VisualElement _contentDetails;
        private VisualElement _contentMarket;
        private VisualElement _contentMissions;
        private VisualElement _contentModules;
        private VisualElement _contentHangar;

        private EntityId _currentEntityId = EntityId.None;
        private int _activeTabIndex;

        // Reusable string builder.
        private readonly StringBuilder _sb = new StringBuilder();

        // Tab definitions for iteration.
        private Button[] _tabButtons;
        private VisualElement[] _tabContents;
        private string[] _tabNames;

        public void Initialize(WorldRegistry registry)
        {
            _registry = registry;
        }

        public void SetupUI(VisualElement root)
        {
            _root = root;
            if (_root == null) return;

            _overlay = _root.Q<VisualElement>("modal-overlay");

            _titleLabel = _root.Q<Label>("modal-title");
            _subtitleLabel = _root.Q<Label>("modal-subtitle");

            _closeBtn = _root.Q<Button>("modal-close-btn");
            if (_closeBtn != null)
                _closeBtn.clicked += Close;

            // Close when clicking overlay background (outside window).
            if (_overlay != null)
            {
                _overlay.RegisterCallback<ClickEvent>(evt =>
                {
                    // Only close if clicking the overlay itself, not the window.
                    if (evt.target == _overlay)
                        Close();
                });
            }

            // Tabs.
            _tabDetails = _root.Q<Button>("modal-tab-details");
            _tabMarket = _root.Q<Button>("modal-tab-market");
            _tabMissions = _root.Q<Button>("modal-tab-missions");
            _tabModules = _root.Q<Button>("modal-tab-modules");
            _tabHangar = _root.Q<Button>("modal-tab-hangar");

            _contentDetails = _root.Q<VisualElement>("modal-content-details");
            _contentMarket = _root.Q<VisualElement>("modal-content-market");
            _contentMissions = _root.Q<VisualElement>("modal-content-missions");
            _contentModules = _root.Q<VisualElement>("modal-content-modules");
            _contentHangar = _root.Q<VisualElement>("modal-content-hangar");

            _tabButtons = new[] { _tabDetails, _tabMarket, _tabMissions, _tabModules, _tabHangar };
            _tabContents = new[] { _contentDetails, _contentMarket, _contentMissions, _contentModules, _contentHangar };
            _tabNames = new[]
            {
                "modal.tab.details",
                "modal.tab.market",
                "modal.tab.missions",
                "modal.tab.modules",
                "modal.tab.hangar"
            };

            // Set tab labels from localization.
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] != null)
                {
                    _tabButtons[i].text = UIStrings.Get(_tabNames[i]);
                    int tabIndex = i;
                    _tabButtons[i].clicked += () => SwitchTab(tabIndex);
                }
            }

            // Set placeholder labels for empty tabs.
            SetPlaceholderText(_contentMarket, "modal.placeholder.market");
            SetPlaceholderText(_contentMissions, "modal.placeholder.missions");
            SetPlaceholderText(_contentModules, "modal.placeholder.modules");
            SetPlaceholderText(_contentHangar, "modal.placeholder.hangar");
        }

        /// <summary>
        /// Open the modal for a specific entity.
        /// </summary>
        public void Open(EntityId entityId)
        {
            var body = _registry?.GetCelestialBody(entityId);
            if (body == null) return;

            _currentEntityId = entityId;

            // Set header.
            if (_titleLabel != null)
                _titleLabel.text = body.DisplayName;
            if (_subtitleLabel != null)
                _subtitleLabel.text = UIStrings.GetBodyTypeName(body.BodyType.ToString());

            // Build details tab content.
            BuildDetailsTab(body);

            // Show first tab.
            SwitchTab(0);

            // Show overlay.
            if (_overlay != null)
                _overlay.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Close the modal.
        /// </summary>
        public void Close()
        {
            _currentEntityId = EntityId.None;
            if (_overlay != null)
                _overlay.style.display = DisplayStyle.None;
        }

        /// <summary>Whether the modal is currently open.</summary>
        public bool IsOpen => _overlay != null && _overlay.resolvedStyle.display == DisplayStyle.Flex;

        private void Update()
        {
            // Live-update modal details if open.
            if (!_currentEntityId.IsValid) return;
            if (!IsOpen) return;

            var body = _registry?.GetCelestialBody(_currentEntityId);
            if (body == null) return;

            // Only rebuild if on the details tab.
            if (_activeTabIndex == 0)
                BuildDetailsTab(body);
        }

        // ---------------------------------------------------------------
        // Tab switching
        // ---------------------------------------------------------------

        private void SwitchTab(int index)
        {
            _activeTabIndex = index;

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] != null)
                {
                    if (i == index)
                        _tabButtons[i].AddToClassList("modal-tab-active");
                    else
                        _tabButtons[i].RemoveFromClassList("modal-tab-active");
                }

                if (_tabContents[i] != null)
                {
                    if (i == index)
                        _tabContents[i].RemoveFromClassList("modal-tab-content-hidden");
                    else
                        _tabContents[i].AddToClassList("modal-tab-content-hidden");
                }
            }
        }

        // ---------------------------------------------------------------
        // Details tab content builder
        // ---------------------------------------------------------------

        private void BuildDetailsTab(CelestialBody body)
        {
            if (_contentDetails == null) return;
            _contentDetails.Clear();

            // --- Identity section ---
            AddSectionHeader(_contentDetails, "modal.section.identity");
            AddDetailRow(_contentDetails, UIStrings.Get("panel.details.name"), body.DisplayName);
            AddDetailRow(_contentDetails, UIStrings.Get("panel.details.type"),
                UIStrings.GetBodyTypeName(body.BodyType.ToString()));

            if (body.ParentId.IsValid)
            {
                var parent = _registry?.GetCelestialBody(body.ParentId);
                AddDetailRow(_contentDetails, UIStrings.Get("panel.details.parent"),
                    parent != null ? parent.DisplayName : body.ParentId.ToString());
            }

            // --- Physical section ---
            AddSectionHeader(_contentDetails, "modal.section.physical");
            AddDetailRow(_contentDetails, UIStrings.Get("panel.details.radius"),
                $"{body.Radius:F2} Mm");

            if (body.SOIRadius.HasValue)
            {
                AddDetailRow(_contentDetails, UIStrings.Get("panel.details.soi_radius"),
                    $"{body.SOIRadius.Value:F1} Mm");
            }

            // --- Orbit section ---
            if (body.Orbit != null)
            {
                AddSectionHeader(_contentDetails, "modal.section.orbit");
                AddDetailRow(_contentDetails, UIStrings.Get("modal.details.orbit_radius"),
                    $"{body.Orbit.SemiMajorAxis:F1} Mm");
                AddDetailRow(_contentDetails, UIStrings.Get("modal.details.orbit_period"),
                    $"{body.Orbit.OrbitalPeriod:F1} sim-s");
                AddDetailRow(_contentDetails, UIStrings.Get("panel.details.attachment"),
                    UIStrings.GetAttachmentModeName(body.AttachmentMode.ToString()));
            }

            // --- Ship section ---
            if (body.BodyType == CelestialBodyType.Ship && body.ShipInfo != null)
            {
                var info = body.ShipInfo;

                AddDetailRow(_contentDetails, UIStrings.Get("panel.details.role"),
                    UIStrings.GetShipRoleName(info.Role.ToString()));
                AddDetailRow(_contentDetails, UIStrings.Get("panel.details.ship_class"),
                    string.IsNullOrEmpty(info.ShipClass) ? UIStrings.Get("panel.details.none") : info.ShipClass);
                AddDetailRow(_contentDetails, UIStrings.Get("panel.details.state"),
                    UIStrings.GetShipStateName(info.State.ToString()));

                if (info.State == ShipState.Travelling && info.CurrentRoute != null)
                {
                    var dest = _registry?.GetCelestialBody(info.CurrentRoute.DestinationBodyId);
                    AddDetailRow(_contentDetails, UIStrings.Get("panel.details.destination"),
                        dest != null ? dest.DisplayName : info.CurrentRoute.DestinationBodyId.ToString());
                }

                if (info.CurrentSOIBodyId.IsValid)
                {
                    var soiBody = _registry?.GetCelestialBody(info.CurrentSOIBodyId);
                    AddDetailRow(_contentDetails, UIStrings.Get("panel.details.soi_body"),
                        soiBody != null ? soiBody.DisplayName : info.CurrentSOIBodyId.ToString());
                }

                // Docking info.
                if (info.IsDocked || info.State == ShipState.ApproachingStation)
                {
                    AddSectionHeader(_contentDetails, "modal.section.docking");
                    if (info.DockedAtStationId.IsValid)
                    {
                        var station = _registry?.GetCelestialBody(info.DockedAtStationId);
                        AddDetailRow(_contentDetails, UIStrings.Get("panel.details.docked_at"),
                            station != null ? station.DisplayName : info.DockedAtStationId.ToString());
                    }
                    if (info.IsDocked)
                    {
                        AddDetailRow(_contentDetails, UIStrings.Get("panel.details.docking_port"),
                            $"#{info.DockedPortId}");
                    }
                }

                // Cargo info.
                if (info.Cargo != null)
                {
                    AddSectionHeader(_contentDetails, "modal.section.economy");
                    AddDetailRow(_contentDetails, UIStrings.Get("panel.details.cargo"),
                        FormatCargo(info.Cargo));
                }
            }

            // --- Station section ---
            if (body.BodyType == CelestialBodyType.Station && body.StationInfo != null)
            {
                var si = body.StationInfo;

                AddDetailRow(_contentDetails, UIStrings.Get("panel.details.station_kind"),
                    UIStrings.GetStationKindName(si.Kind.ToString()));

                // Docking.
                if (si.HasDocking)
                {
                    AddSectionHeader(_contentDetails, "modal.section.docking");
                    AddDetailRow(_contentDetails, UIStrings.Get("panel.details.docking_ports"),
                        si.Docking.TotalPorts.ToString());
                    AddDetailRow(_contentDetails, UIStrings.Get("panel.details.ports_occupied"),
                        $"{si.Docking.OccupiedCount} / {si.Docking.TotalPorts}");
                }

                // Storage.
                if (si.HasStorage)
                {
                    AddSectionHeader(_contentDetails, "modal.section.economy");
                    AddDetailRow(_contentDetails, UIStrings.Get("panel.details.storage"),
                        FormatStorage(si.Storage));
                }

                // Production.
                if (si.HasProduction)
                {
                    AddSectionHeader(_contentDetails, "modal.section.production");
                    AddDetailRow(_contentDetails, UIStrings.Get("panel.details.production"),
                        FormatProduction(si.Production));
                }
            }
        }

        // ---------------------------------------------------------------
        // UI builder helpers
        // ---------------------------------------------------------------

        private static void AddSectionHeader(VisualElement parent, string locKey)
        {
            var header = new Label(UIStrings.Get(locKey));
            header.AddToClassList("modal-section-header");
            parent.Add(header);
        }

        private static void AddDetailRow(VisualElement parent, string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("modal-details-row");

            var cellLabel = new VisualElement();
            cellLabel.AddToClassList("details-cell-label");
            var lbl = new Label(label);
            lbl.AddToClassList("details-label");
            cellLabel.Add(lbl);
            row.Add(cellLabel);

            var cellValue = new VisualElement();
            cellValue.AddToClassList("details-cell-value");
            var val = new Label(value);
            val.AddToClassList("details-value");
            cellValue.Add(val);
            row.Add(cellValue);

            parent.Add(row);
        }

        private static void SetPlaceholderText(VisualElement container, string locKey)
        {
            if (container == null) return;
            container.Clear();
            var label = new Label(UIStrings.Get(locKey));
            label.AddToClassList("modal-empty-text");
            container.Add(label);
        }

        // ---------------------------------------------------------------
        // Formatting (shared with details panel)
        // ---------------------------------------------------------------

        private string FormatCargo(ShipCargo cargo)
        {
            if (cargo == null || cargo.IsEmpty)
                return UIStrings.Get("panel.details.cargo_empty");

            _sb.Clear();
            bool first = true;
            foreach (var type in cargo.GetNonEmptyTypes())
            {
                if (!first) _sb.Append('\n');
                _sb.Append(UIStrings.GetResourceShortName(type.ToString()));
                _sb.Append(": ");
                _sb.Append(cargo.GetAmount(type).ToString("F0"));
                first = false;
            }
            _sb.Append($"\n[{cargo.TotalUsed:F0}/{cargo.Capacity:F0}]");
            return _sb.ToString();
        }

        private string FormatStorage(StationStorage storage)
        {
            if (storage == null)
                return UIStrings.Get("panel.details.storage_empty");

            _sb.Clear();
            bool first = true;
            bool any = false;
            foreach (var type in storage.GetNonEmptyTypes())
            {
                if (!first) _sb.Append('\n');
                _sb.Append(UIStrings.GetResourceShortName(type.ToString()));
                _sb.Append(": ");
                _sb.Append(storage.GetAmount(type).ToString("F0"));
                first = false;
                any = true;
            }

            return any ? _sb.ToString() : UIStrings.Get("panel.details.storage_empty");
        }

        private string FormatProduction(StationProductionState prod)
        {
            if (prod == null || !prod.HasRecipe)
                return UIStrings.Get("panel.details.production_none");

            var recipe = prod.Recipe;
            _sb.Clear();

            string outputName = UIStrings.GetResourceShortName(recipe.OutputResource.ToString());
            double rate = recipe.OutputAmountPerCycle / recipe.CycleTime;
            _sb.Append($"{outputName} +{rate:F1}/c");

            if (recipe.HasInputs)
            {
                foreach (var input in recipe.InputsPerCycle)
                {
                    string inputName = UIStrings.GetResourceShortName(input.Key.ToString());
                    double inputRate = input.Value / recipe.CycleTime;
                    _sb.Append($"\n\u2190 {inputName} -{inputRate:F1}/c");
                }
            }

            if (prod.IsStalled)
                _sb.Append($"\n[{UIStrings.Get("panel.details.production_stalled")}]");
            else
                _sb.Append($"\n[{prod.ProgressFraction:P0}]");

            return _sb.ToString();
        }
    }
}
