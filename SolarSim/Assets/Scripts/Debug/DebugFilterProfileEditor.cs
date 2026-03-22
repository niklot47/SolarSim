#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SpaceSim.Debug
{
    /// <summary>
    /// Custom Inspector for DebugFilterProfile.
    /// Renders filter groups as a collapsible tree:
    ///   [✓] ▼ Economy              ← master toggle + foldout with name
    ///           [✓] CargoTransfer  ← indented child tag toggle
    ///           [✓] Production
    ///           [✓] TradeAI
    ///
    /// Provides convenience buttons:
    ///   [Enable All] [Disable All] [Reset to Defaults]
    ///
    /// Changes are applied immediately via ApplyTo() if the game is running,
    /// giving live feedback during Play mode.
    /// </summary>
    [CustomEditor(typeof(DebugFilterProfile))]
    public class DebugFilterProfileEditor : Editor
    {
        private const float TagIndentWidth = 40f;

        // Cached styles.
        private GUIStyle _tagStyle;
        private GUIStyle _groupStyle;
        private GUIStyle _categoryHintStyle;

        public override void OnInspectorGUI()
        {
            EnsureStyles();

            var profile = (DebugFilterProfile)target;

            EditorGUI.BeginChangeCheck();

            // Global settings.
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Global Settings", EditorStyles.boldLabel);

            profile.EnableLogging = EditorGUILayout.Toggle(
                new GUIContent("Enable Logging", "Master switch. When off, only errors pass."),
                profile.EnableLogging);

            profile.MinimumSeverity = (DebugSeverity)EditorGUILayout.EnumPopup(
                new GUIContent("Minimum Severity", "Logs below this level are discarded (errors always pass)."),
                profile.MinimumSeverity);

            // Convenience buttons.
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Enable All", GUILayout.Height(24)))
            {
                SetAllGroups(profile, true);
            }
            if (GUILayout.Button("Disable All", GUILayout.Height(24)))
            {
                SetAllGroups(profile, false);
            }
            if (GUILayout.Button("Reset Defaults", GUILayout.Height(24)))
            {
                Undo.RecordObject(profile, "Reset Debug Filter Defaults");
                profile.PopulateDefaults();
            }
            EditorGUILayout.EndHorizontal();

            // Filter groups — tree view.
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Filter Groups", EditorStyles.boldLabel);

            if (profile.Groups == null || profile.Groups.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No filter groups defined. Click 'Reset Defaults' to create standard groups.",
                    MessageType.Info);
            }
            else
            {
                for (int i = 0; i < profile.Groups.Count; i++)
                {
                    DrawFilterGroup(profile.Groups[i], i);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(profile);

                // Live-apply during Play mode.
                if (Application.isPlaying)
                {
                    var runtimeFilter = GameDebug.ActiveFilter;
                    if (runtimeFilter != null)
                    {
                        profile.ApplyTo(runtimeFilter);
                    }
                }
            }
        }

        private void DrawFilterGroup(DebugFilterGroup group, int groupIndex)
        {
            EditorGUILayout.BeginVertical("box");

            // Header row: [checkbox] [gap] [▼ GroupName]  [categories hint]
            EditorGUILayout.BeginHorizontal();

            // Master enable toggle — leftmost element.
            bool newEnabled = EditorGUILayout.Toggle(GUIContent.none, group.Enabled, GUILayout.Width(16));
            if (newEnabled != group.Enabled)
            {
                Undo.RecordObject(target, "Toggle Debug Group");
                group.Enabled = newEnabled;
            }

            // Small gap so foldout label does not overlap the checkbox.
            GUILayout.Space(6);

            // Foldout arrow + group name as its label — right after checkbox + gap.
            group.FoldoutOpen = EditorGUILayout.Foldout(group.FoldoutOpen, group.DisplayName, true, _groupStyle);

            // Category hint — small grey text pushed to the right.
            if (group.Categories != null && group.Categories.Count > 0)
            {
                string cats = string.Join(", ", group.Categories);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"[{cats}]", _categoryHintStyle, GUILayout.Width(120));
            }

            EditorGUILayout.EndHorizontal();

            // Children: individual tag toggles (only when foldout is open).
            if (group.FoldoutOpen && group.Tags != null && group.Tags.Count > 0)
            {
                // Dim children when master toggle is off.
                bool wasEnabled = GUI.enabled;
                if (!group.Enabled)
                    GUI.enabled = false;

                for (int j = 0; j < group.Tags.Count; j++)
                {
                    DrawTagToggle(group.Tags[j]);
                }

                GUI.enabled = wasEnabled;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTagToggle(DebugTagToggle tagToggle)
        {
            EditorGUILayout.BeginHorizontal();

            // Indent child tags to the right so they sit under the group name, not the checkbox.
            GUILayout.Space(TagIndentWidth);

            bool newEnabled = EditorGUILayout.Toggle(GUIContent.none, tagToggle.Enabled, GUILayout.Width(16));
            if (newEnabled != tagToggle.Enabled)
            {
                Undo.RecordObject(target, "Toggle Debug Tag");
                tagToggle.Enabled = newEnabled;
            }

            EditorGUILayout.LabelField(tagToggle.Tag, _tagStyle);
            EditorGUILayout.EndHorizontal();
        }

        private void SetAllGroups(DebugFilterProfile profile, bool enabled)
        {
            Undo.RecordObject(profile, enabled ? "Enable All Debug Groups" : "Disable All Debug Groups");
            foreach (var group in profile.Groups)
            {
                group.Enabled = enabled;
                if (group.Tags != null)
                {
                    foreach (var tag in group.Tags)
                        tag.Enabled = enabled;
                }
            }
        }

        private void EnsureStyles()
        {
            if (_tagStyle != null) return;

            _tagStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal
            };
            _tagStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            _groupStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            _categoryHintStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleRight
            };
            _categoryHintStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        }
    }
}
#endif
