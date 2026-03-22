#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SpaceSim.Debug
{
    /// <summary>
    /// Editor menu to create a pre-configured DebugFilterProfile asset.
    /// Menu: SpaceSim -> Create Debug Filter Profile.
    /// </summary>
    public static class DebugFilterProfileCreator
    {
        [MenuItem("SpaceSim/Create Debug Filter Profile")]
        public static void CreateDefaultProfile()
        {
            var asset = ScriptableObject.CreateInstance<DebugFilterProfile>();
            asset.PopulateDefaults();

            string path = "Assets/Data/Config/DebugFilterProfile.asset";
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;

            UnityEngine.Debug.Log($"[DebugFilterProfileCreator] Created default profile at {path}");
        }
    }
}
#endif
