using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SpaceSim.Simulation.Core;
using SpaceSim.World.Entities;
using SpaceSim.World.Systems;
using SpaceSim.UI.Localization;
using SpaceSim.UI.Core;

// Resolve ambiguity with UnityEngine.EntityId (Unity 6+).
using EntityId = SpaceSim.Shared.Identifiers.EntityId;

namespace SpaceSim.UI.Panels
{
    /// <summary>
    /// UI Toolkit controller for the celestial body list panel.
    /// Populates a hierarchical ListView with PNG icon support and keeps selection
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

            // Hide/show the entire panel body (content + padding disappear).
            if (_panelBody != null)
                _panelBody.style.display = _isCollapsed ? DisplayStyle.None : DisplayStyle.Flex;

            // When collapsed, align-self: flex-start shrinks panel height to header only.
            // When expanded, align-self: stretch fills the full parent height.
            if (_panel != null)
                _panel.style.alignSelf = _isCollapsed ? Align.FlexStart : Align.Stretch;

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
        // List item creation with icon (PNG or fallback circle)
        // ---------------------------------------------------------------

        /// <summary>
        /// Create a list item with an icon element and a label.
        /// Icon will display PNG if available, or a colored circle fallback.
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
        /// Bind body data to a list item (icon + indented name).
        /// </summary>
        private void BindListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _bodies.Count) return;
            var body = _bodies[index];

            var label = element.Q<Label>("item-label");
            var icon = element.Q("item-icon");

            if (label != null)
            {
                label.text = body.DisplayName;
            }

            if (icon != null)
            {
                int depth = GetDepth(body);
                icon.style.marginLeft = depth * 12;

                // Try to load PNG icon. Fall back to colored circle.
                var texture = BodyIconResolver.GetSmallIcon(body);
                if (texture != null)
                {
                    icon.style.backgroundImage = new StyleBackground(texture);
                    icon.style.backgroundColor = StyleKeyword.None;
                    // Square icon, no border-radius for PNG.
                    icon.style.borderTopLeftRadius = 0;
                    icon.style.borderTopRightRadius = 0;
                    icon.style.borderBottomLeftRadius = 0;
                    icon.style.borderBottomRightRadius = 0;
                    icon.style.width = 16;
                    icon.style.height = 16;
                }
                else
                {
                    // Fallback: colored circle.
                    icon.style.backgroundImage = StyleKeyword.None;
                    icon.style.backgroundColor = new StyleColor(GetIconColor(body));
                    icon.style.borderTopLeftRadius = 5;
                    icon.style.borderTopRightRadius = 5;
                    icon.style.borderBottomLeftRadius = 5;
                    icon.style.borderBottomRightRadius = 5;
                    icon.style.width = 10;
                    icon.style.height = 10;
                }
            }
        }

        /// <summary>
        /// Fallback icon color based on body type and role.
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
                if (depth > 10) break;
            }
            return depth;
        }

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
