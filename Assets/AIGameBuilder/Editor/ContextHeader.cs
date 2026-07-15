using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AIGameBuilder
{
    public static class ContextHeader
    {
        public static string Build(IEnumerable<string> recentErrors)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Unity context]");
            sb.AppendLine("Active scene: " + EditorSceneManager.GetActiveScene().name);

            var names = new List<string>();
            foreach (var go in Selection.gameObjects) names.Add(go.name);
            sb.AppendLine("Selected objects: " + (names.Count > 0 ? string.Join(", ", names) : "none"));

            var errs = new List<string>(recentErrors);
            if (errs.Count > 0)
            {
                sb.AppendLine("Recent console errors:");
                foreach (var err in errs) sb.AppendLine("  " + err);
            }
            sb.AppendLine("[end context]");
            return sb.ToString();
        }
    }
}
