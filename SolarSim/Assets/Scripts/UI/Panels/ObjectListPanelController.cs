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
    /// Populates a hierarchical ListView and keeps selection in sync with SelectionService.
    /// Supports dynamic refresh when hierarchy changes (e.g. ship departure/arrival).
    /// </summary>
    public class ObjectListPanelController : MonoBehaviour
    {
        private WorldRegistry _registry;
        private StarSystem _system;
        private SelectionService _selectionService;

        private VisualElement _root;
        private ListView _listView;
        private List<CelestialBody> _bodies = new List<CelestialBody>();

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

            _listView = _root.Q<ListView>("object-list-view");
            if (_listView == null) return;

            RefreshBodyList();

            _listView.makeItem = () =>
            {
                var label = new Label();
                label.style.paddingLeft = 8;
                label.style.paddingTop = 4;
                label.style.paddingBottom = 4;
                label.style.fontSize = 14;
                label.style.color = new StyleColor(Color.white);
                label.style.unityFontStyleAndWeight = FontStyle.Normal;
                return label;
            };

            _listView.bindItem = (element, index) =>
            {
                if (index < 0 || index >= _bodies.Count) return;
                var body = _bodies[index];
                var label = element as Label;
                if (label == null) return;

                // Hierarchical indentation based on depth.
                int depth = GetDepth(body);
                string indent = new string(' ', depth * 4);

                string typeName = UIStrings.GetBodyTypeName(body.BodyType.ToString());
                label.text = $"{indent}{body.DisplayName} ({typeName})";
            };

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
        /// Call this when hierarchy changes (e.g. ship re-parenting after travel).
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
                // Find index of body with this id.
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
