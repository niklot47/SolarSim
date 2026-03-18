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
    ///
    /// Filter bar (inside collapsible body, above the list):
    ///   [Корабли toggle] [Свернуть всё] [Развернуть всё]
    ///
    /// Items with children display a ▼/▶ toggle button to hide/show their subtree.
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

        // Currently selected entity — used by BindListItem to apply highlight.
        private EntityId _currentSelectionId = EntityId.None;

        // Subtree collapse state — bodies whose children are hidden in the list.
        private readonly HashSet<EntityId> _collapsedIds = new HashSet<EntityId>();

        // Cache: which body IDs have at least one child present in the system.
        private readonly HashSet<EntityId> _bodyIdsWithChildren = new HashSet<EntityId>();

        // Filter state.
        private bool _showShips = true;

        // Filter bar buttons.
        private Button _filterBtnShips;
        private Button _filterBtnCollapseAll;
        private Button _filterBtnExpandAll;

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

            // Setup panel collapse.
            _panel = _root.Q<VisualElement>("left-panel");
            _panelBody = _root.Q<VisualElement>("left-panel-body");
            _collapseBtn = _root.Q<Button>("left-collapse-btn");
            if (_collapseBtn != null)
                _collapseBtn.clicked += TogglePanelCollapse;

            // Setup filter bar buttons.
            _filterBtnShips = _root.Q<Button>("filter-btn-ships");
            _filterBtnCollapseAll = _root.Q<Button>("filter-btn-collapse-all");
            _filterBtnExpandAll = _root.Q<Button>("filter-btn-expand-all");

            if (_filterBtnShips != null)
            {
                _filterBtnShips.text = UIStrings.Get("panel.object_list.ships");
                _filterBtnShips.clicked += OnToggleShips;
            }

            if (_filterBtnCollapseAll != null)
            {
                _filterBtnCollapseAll.text = UIStrings.Get("panel.object_list.collapse_all");
                _filterBtnCollapseAll.clicked += OnCollapseAll;
            }

            if (_filterBtnExpandAll != null)
            {
                _filterBtnExpandAll.text = UIStrings.Get("panel.object_list.expand_all");
                _filterBtnExpandAll.clicked += OnExpandAll;
            }

            UpdateFilterButtonVisuals();

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

            EntityId selectedId = _selectionService != null ? _selectionService.CurrentSelectionId : EntityId.None;

            RefreshBodyList();

            _listView.itemsSource = _bodies;
            _listView.Rebuild();

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

        // ---------------------------------------------------------------
        // Panel collapse (header button)
        // ---------------------------------------------------------------

        private void TogglePanelCollapse()
        {
            _isCollapsed = !_isCollapsed;

            if (_panelBody != null)
                _panelBody.style.display = _isCollapsed ? DisplayStyle.None : DisplayStyle.Flex;

            if (_panel != null)
                _panel.style.alignSelf = _isCollapsed ? Align.FlexStart : Align.Stretch;

            if (_collapseBtn != null)
                _collapseBtn.text = _isCollapsed ? "\u25B6" : "\u25BC";
        }

        // ---------------------------------------------------------------
        // Filter bar actions
        // ---------------------------------------------------------------

        /// <summary>Toggle visibility of Ship entities in the list.</summary>
        private void OnToggleShips()
        {
            _showShips = !_showShips;
            UpdateFilterButtonVisuals();
            Refresh();
        }

        /// <summary>Collapse all subtrees that have children.</summary>
        private void OnCollapseAll()
        {
            foreach (var id in _bodyIdsWithChildren)
                _collapsedIds.Add(id);
            Refresh();
        }

        /// <summary>Expand all subtrees.</summary>
        private void OnExpandAll()
        {
            _collapsedIds.Clear();
            Refresh();
        }

        /// <summary>Update visual active state of filter buttons.</summary>
        private void UpdateFilterButtonVisuals()
        {
            if (_filterBtnShips == null) return;

            // Ships button: active (lit) when ships are VISIBLE, dimmed when hidden.
            if (_showShips)
                _filterBtnShips.AddToClassList("filter-btn-active");
            else
                _filterBtnShips.RemoveFromClassList("filter-btn-active");
        }

        // ---------------------------------------------------------------
        // Body list building (respects _collapsedIds and _showShips)
        // ---------------------------------------------------------------

        private void RefreshBodyList()
        {
            _bodies.Clear();
            _bodyIdsWithChildren.Clear();

            if (_registry == null || _system == null) return;

            // Pre-compute which bodies have visible children (accounting for ship filter).
            var systemIdSet = new HashSet<EntityId>(_system.AllBodyIds);
            foreach (var bodyId in _system.AllBodyIds)
            {
                var body = _registry.GetCelestialBody(bodyId);
                if (body == null) continue;
                foreach (var childId in body.ChildIds)
                {
                    if (!systemIdSet.Contains(childId)) continue;
                    var child = _registry.GetCelestialBody(childId);
                    if (child == null) continue;
                    // A body "has children" if at least one child would be visible.
                    if (child.BodyType == CelestialBodyType.Ship && !_showShips) continue;
                    _bodyIdsWithChildren.Add(bodyId);
                    break;
                }
            }

            foreach (var rootId in _system.RootBodyIds)
                AddBodyAndChildren(rootId);
        }

        private void AddBodyAndChildren(EntityId bodyId)
        {
            var body = _registry.GetCelestialBody(bodyId);
            if (body == null) return;

            // Apply ship filter.
            if (body.BodyType == CelestialBodyType.Ship && !_showShips) return;

            _bodies.Add(body);

            // If collapsed, skip children.
            if (_collapsedIds.Contains(bodyId)) return;

            foreach (var childId in body.ChildIds)
                AddBodyAndChildren(childId);
        }

        // ---------------------------------------------------------------
        // List item creation
        // ---------------------------------------------------------------

        /// <summary>
        /// Each row: [collapse-btn (optional)] [icon] [label]
        ///
        /// The collapse button registers its click handler ONCE here in MakeListItem.
        /// BindListItem only updates userData (the EntityId) and the button text/visibility.
        /// This avoids the reliability issue caused by replacing clickable on every rebind.
        /// StopImmediatePropagation prevents the ListView from also handling the pointer event.
        /// </summary>
        private VisualElement MakeListItem()
        {
            var row = new VisualElement();
            row.AddToClassList("list-item-row");

            // Collapse toggle — handler registered once here, ID stored in userData.
            var collapseToggle = new Button();
            collapseToggle.name = "item-collapse-btn";
            collapseToggle.AddToClassList("list-item-collapse-btn");
            collapseToggle.style.width = 16;
            collapseToggle.style.height = 16;
            collapseToggle.style.minWidth = 16;
            collapseToggle.style.flexShrink = 0;
            collapseToggle.style.fontSize = 9;
            collapseToggle.style.paddingLeft = 0;
            collapseToggle.style.paddingRight = 0;
            collapseToggle.style.paddingTop = 0;
            collapseToggle.style.paddingBottom = 0;
            collapseToggle.style.marginRight = 2;
            collapseToggle.style.borderTopWidth = 0;
            collapseToggle.style.borderBottomWidth = 0;
            collapseToggle.style.borderLeftWidth = 0;
            collapseToggle.style.borderRightWidth = 0;
            collapseToggle.style.borderTopLeftRadius = 2;
            collapseToggle.style.borderTopRightRadius = 2;
            collapseToggle.style.borderBottomLeftRadius = 2;
            collapseToggle.style.borderBottomRightRadius = 2;
            collapseToggle.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            collapseToggle.style.color = new StyleColor(new Color(0.4f, 0.86f, 0.31f, 0.8f));
            collapseToggle.style.unityTextAlign = TextAnchor.MiddleCenter;


            // Intercept PointerDownEvent in TrickleDown phase — fires BEFORE ListView processes
            // pointer input, so the list never starts a selection change on collapse-button clicks.
            // This prevents the Refresh() race where ListView rebuilds the list mid-click.
            collapseToggle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (collapseToggle.userData is EntityId pdId && pdId.IsValid)
                {
                    UnityEngine.Debug.Log(
                        $"[CollapseBtn] PointerDown intercepted | id={pdId} | phase={evt.propagationPhase}");
                    evt.StopImmediatePropagation();
                }
            }, TrickleDown.TrickleDown);

            // ClickEvent for the actual toggle — registered once, ID read from userData.
            collapseToggle.RegisterCallback<ClickEvent>(evt =>
            {
                if (collapseToggle.userData is EntityId clickId && clickId.IsValid)
                {
                    UnityEngine.Debug.Log(
                        $"[CollapseBtn] Click | id={clickId} | phase={evt.propagationPhase} | text='{collapseToggle.text}'");
                    OnToggleSubtree(clickId);
                }
                evt.StopImmediatePropagation();
            });
            row.Add(collapseToggle);

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

        private void BindListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _bodies.Count) return;
            var body = _bodies[index];

            var label = element.Q<Label>("item-label");
            var icon = element.Q("item-icon");
            var collapseToggle = element.Q<Button>("item-collapse-btn");

            if (label != null)
                label.text = body.DisplayName;

            if (icon != null)
            {
                int depth = GetDepth(body);
                icon.style.marginLeft = depth > 0 ? depth * 12 : 0;

                var texture = BodyIconResolver.GetSmallIcon(body);
                if (texture != null)
                {
                    icon.style.backgroundImage = new StyleBackground(texture);
                    icon.style.backgroundColor = StyleKeyword.None;
                    icon.style.borderTopLeftRadius = 0;
                    icon.style.borderTopRightRadius = 0;
                    icon.style.borderBottomLeftRadius = 0;
                    icon.style.borderBottomRightRadius = 0;
                    icon.style.width = 16;
                    icon.style.height = 16;
                }
                else
                {
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

            if (collapseToggle != null)
            {
                bool hasChildren = _bodyIdsWithChildren.Contains(body.Id);

                if (hasChildren)
                {
                    // Store current body ID so the single registered handler can read it.
                    collapseToggle.userData = body.Id;
                    collapseToggle.style.display = DisplayStyle.Flex;
                    collapseToggle.text = _collapsedIds.Contains(body.Id) ? "\u25B6" : "\u25BC";
                }
                else
                {
                    // No visible children — transparent placeholder, clear ID so click is no-op.
                    collapseToggle.userData = EntityId.None;
                    collapseToggle.style.display = DisplayStyle.Flex;
                    collapseToggle.text = "";
                }
            }

            // Apply or remove selected highlight directly in BindListItem.
            // This is the ONLY reliable place — it runs after every Rebuild/Refresh,
            // so the style survives list updates caused by ship movements etc.
            // element is the ListView item wrapper; row is our inner flex container.
            var row = element.Q(className: "list-item-row");
            bool isSelected = _currentSelectionId.IsValid && body.Id == _currentSelectionId;
            if (row != null)
            {
                if (isSelected)
                    row.AddToClassList("list-item-selected");
                else
                    row.RemoveFromClassList("list-item-selected");
            }
            // Suppress Unity's opaque inline background on the wrapper.
            element.style.backgroundColor = isSelected
                ? new StyleColor(Color.clear)
                : StyleKeyword.Null;
        }

        // ---------------------------------------------------------------
        // Subtree collapse / expand
        // ---------------------------------------------------------------

        private void OnToggleSubtree(EntityId bodyId)
        {
            if (_collapsedIds.Contains(bodyId))
                _collapsedIds.Remove(bodyId);
            else
                _collapsedIds.Add(bodyId);

            Refresh();
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

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
                    // BindListItem handles the visual highlight — just trigger a rebind.
                    _listView.RefreshItems();
                    return;
                }
            }

            // Nothing selected.
            _listView.RefreshItems();
        }

        private void OnExternalSelectionChanged(EntityId previousId, EntityId newId)
        {
            if (_listView == null) return;

            _currentSelectionId = newId;
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
                        _listView.ScrollToItem(i);
                        break;
                    }
                }
            }

            // BindListItem handles the visual — refresh to rebind all visible items.
            _listView.RefreshItems();

            _suppressSelectionEvent = false;
        }
    }
}
