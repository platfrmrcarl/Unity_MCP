using UnityEditor;
using UnityEngine;

namespace AIGameBuilder
{
    public class AIGameBuilderWindow : EditorWindow
    {
        [MenuItem("Window/AI Game Builder")]
        public static void Open()
        {
            var window = GetWindow<AIGameBuilderWindow>();
            window.titleContent = new GUIContent("AI Game Builder");
            window.minSize = new Vector2(360, 300);
        }

        private void OnGUI()
        {
            GUILayout.Label("AI Game Builder", EditorStyles.boldLabel);
        }
    }
}
