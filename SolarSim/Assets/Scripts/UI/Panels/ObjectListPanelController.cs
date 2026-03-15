using System.Collections.Generic;
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
    /// UI Toolkit controller for the celestial body list panel.
    /// Populates a hierarchical ListView with icon placeholders and keeps selection
    /// in sync with SelectionService. Supports collapsible panel header.
    /// When collapsed, panel shrinks to show only the header bar.
    /// </summary>
    public class ObjectListPanelController : MonoBehaviour
    {
        private WorldRegistry _registry;
        private StarSystem _system;
        private SelectionService _selectionService;

        private VisualElement _root;
        private ListView _listView;
        private List<CelestialBody> _bodies = new List<CelestialBody>();

        // Panel collapse state.
        private VisualElement _panel;
        private VisualElement _panelBody;
        private Button _collapseBtn;
        private bool _isCollapsed;

        // Guard against feedback loops when programmatically setting selection.
        private bool _suppressSelectionEvent;

        public void Initialize(WorldRegistry registry, StarSystem system, SelectionService selectionService)
        {
            _registry = registry;
            _system = system;
            _selectionService = selectionService;
        }

        /// <summary>
        /// Build the UI from a shared root VisualElement.
        /// </summary>
        public void SetupUI(VisualElement root)
        {
            _root = root;
            if (_root == null) return;

            var titleLabel = _root.Q<Label>("object-list-title");
            if (titleLabel != null)
                titleLabel.text = UIStrings.Get("panel.object_list.title");

            // Cache panel and body for collapse logic.
            _panel = _root.Q<VisualElement>("left-panel");
            _panelBody = _root.Q<VisualElement>("left-panel-body");
            _collapseBtn = _root.Q<Button>("left-collapse-btn");
            if (_collapseBtn != null)
                _collapseBtn.clicked += ToggleCollapse;

            _listView = _root.Q<ListView>("object-list-view");
            if (_listView == null) return;

            RefreshBodyList();

            _listView.makeItem = MakeListItem;
            _listView.bindItem = BindListItem;
            _listView.itemsSource = _bodies;
            _listView.selectionChanged += OnListSelectionChanged;

            // Subscribe to external selection changes to sync list highlight.
            if (_selectionService != null)
                _selectionService.OnSelectionChanged += OnExternalSelectionChanged;
        }

        private void OnDestroy()
        {
            if (_selectionService != null)
                _selectionService.OnSelectionChanged -= OnExternalSelectionChanged;
        }

        /// <summary>
        /// Rebuild the body list from current world state.
        /// Call this when hierarchy changes (e.g. ship departure/arrival).
        /// </summary>
        public void Refresh()
        {
            if (_listView == null) return;

            // Remember current selection.
            EntityId selectedId = _selectionService != null ? _selectionService.CurrentSelectionId : EntityId.None;

            RefreshBodyList();

            _listView.itemsSource = _bodies;
            _listView.Rebuild();

            // Restore selection highlight.
            if (selectedId.IsValid)
            {
                _suppressSelectionEvent = true;
                for (int i = 0; i < _bodies.Count; i++)
                {
                    if (_bodies[i].Id == selectedId)
                    {
                        _listView.SetSelection(i);
                        break;
                    }
                }
                _suppressSelectionEvent = false;
            }
        }

        private void ToggleCollapse()
        {
            _isCollapsed = !_isCollapsed;

            if (_panelBody != null)
            {
                if (_isCollapsed)
                    _panelBody.AddToClassList("panel-body-hidden");
                else
                    _panelBody.RemoveFromClassList("panel-body-hidden");
            }

            // Shrink/expand the panel itself so only header shows when collapsed.
            if (_panel != null)
            {
                if (_isCollapsed)
                {
                    _panel.style.flexGrow = 0;
                    _panel.style.height = StyleKeyword.Auto;
                }
                else
                {
                    _panel.style.flexGrow = StyleKeyword.Null;
                    _panel.style.height = StyleKeyword.Null;
                }
            }

            if (_collapseBtn != null)
                _collapseBtn.text = _isCollapsed ? "\u25B6" : "\u25BC";
        }

        private void RefreshBodyList()
        {
            _bodies.Clear();
            if (_registry == null || _system == null) return;

            foreach (var rootId in _system.RootBodyIds)
            {
                AddBodyAndChildren(rootId);
            }
        }

        private void AddBodyAndChildren(EntityId bodyId)
        {
            var body = _registry.GetCelestialBody(bodyId);
            if (body == null) return;

            _bodies.Add(body);

            foreach (var childId in body.ChildIds)
            {
                AddBodyAndChildren(childId);
            }
        }

        // ---------------------------------------------------------------
        // List item creation with icon placeholder
        // ---------------------------------------------------------------

        /// <summary>
        /// Create a list item with a small circular icon placeholder and a label.
        /// </summary>
        private VisualElement MakeListItem()
        {
            var row = new VisualElement();
            row.AddToClassList("list-item-row");

            var icon = new VisualElement();
            icon.name = "item-icon";
            icon.AddToClassList("list-item-icon");
            row.Add(icon);

            var label = new Label();
            label.name = "item-label";
            label.AddToClassList("list-item-label");
            row.Add(label);

            return row;
        }

        /// <summary>
        /// Bind body data to a list item (icon color + indented name).
        /// </summary>
        private void BindListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _bodies.Count) return;
            var body = _bodies[index];

            var label = element.Q<Label>("item-label");
            var icon = element.Q("item-icon");

            if (label != null)
            {
                int depth = GetDepth(body);
                string indent = new string(' ', depth * 3);
                label.text = $"{indent}{body.DisplayName}";
            }

            // Color the icon placeholder by body type.
            if (icon != null)
            {
                int depth = GetDepth(body);
                icon.style.marginLeft = depth * 12;
                icon.style.backgroundColor = new StyleColor(GetIconColor(body));
            }
        }

        /// <summary>
        /// Get an icon placeholder color based on body type and role.
        /// </summary>
        private static Color GetIconColor(CelestialBody body)
        {
            if (body.BodyType == CelestialBodyType.Ship && body.ShipInfo != null)
            {
                return body.ShipInfo.Role switch
                {
                    ShipRole.Player => new Color(0.2f, 1.0f, 0.4f),
                    ShipRole.Trader => new Color(0.9f, 0.7f, 0.2f),
                    ShipRole.Patrol => new Color(0.9f, 0.3f, 0.3f),
                    ShipRole.Civilian => new Color(0.7f, 0.7f, 0.8f),
                    _ => new Color(0.5f, 0.8f, 0.5f)
                };
            }

            if (body.BodyType == CelestialBodyType.Station && body.StationInfo != null)
            {
                return body.StationInfo.Kind switch
                {
                    StationKind.Orbital => new Color(0.5f, 0.9f, 1.0f),
                    StationKind.Surface => new Color(0.9f, 0.6f, 0.3f),
                    _ => new Color(0.6f, 0.8f, 0.6f)
                };
            }

            return body.BodyType switch
            {
                CelestialBodyType.Star => new Color(1f, 0.9f, 0.3f),
                CelestialBodyType.Planet => new Color(0.15f, 0.85f, 0.35f),
                CelestialBodyType.Moon => new Color(0.5f, 0.7f, 0.5f),
                _ => new Color(0.4f, 0.7f, 0.4f)
            };
        }

        /// <summary>
        /// Calculate hierarchy depth by walking up ParentId chain.
        /// </summary>
        private int GetDepth(CelestialBody body)
        {
            int depth = 0;
            var current = body;
            while (current != null && current.ParentId.IsValid)
            {
                depth++;
                current = _registry.GetCelestialBody(current.ParentId);
                if (depth > 10) break; // Safety limit.
            }
            return depth;
        }

        /// <summary>
        /// User clicked in the list — propagate to SelectionService.
        /// </summary>
        private void OnListSelectionChanged(IEnumerable<object> selection)
        {
            if (_suppressSelectionEvent) return;

            foreach (var item in selection)
            {
                if (item is CelestialBody body)
                {
                    _selectionService?.Select(body.Id);
                    return;
                }
            }
        }

        /// <summary>
        /// Selection changed externally (e.g. scene click) — sync ListView highlight.
        /// </summary>
        private void OnExternalSelectionChanged(EntityId previousId, EntityId newId)
        {
            if (_listView == null) return;

            _suppressSelectionEvent = true;

            if (!newId.IsValid)
            {
                _listView.ClearSelection();
            }
            else
            {
                for (int i = 0; i < _bodies.Count; i++)
                {
                    if (_bodies[i].Id == newId)
                    {
                        _listView.SetSelection(i);
                        break;
                    }
                }
            }

            _suppressSelectionEvent = false;
        }
    }
}
