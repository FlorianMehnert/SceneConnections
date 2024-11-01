using System.Collections.Generic;
using UnityEditor;

namespace SceneConnections.Editor.Utils.ScriptVisualization
{
    public static class ScriptFinder
    {
        public static List<string> GetAllScriptPaths()
        {
            // Fetch all assets with the `.cs` extension in the project
            string[] guids = AssetDatabase.FindAssets("t:Script");
            var scriptPaths = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                scriptPaths.Add(path);
            }

            return scriptPaths;
        }
    }
}