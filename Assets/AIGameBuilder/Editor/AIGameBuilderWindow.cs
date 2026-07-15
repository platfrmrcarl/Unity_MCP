using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AIGameBuilder
{
    public class AIGameBuilderWindow : EditorWindow
    {
        private readonly List<string> _transcript = new List<string>();
        private string _input = "";
        private Vector2 _scroll;
        private BuilderStatus _status = BuilderStatus.Idle;
        private string _initMessage = "";

        [MenuItem("Window/AI Game Builder")]
        public static void Open()
        {
            var window = GetWindow<AIGameBuilderWindow>();
            window.titleContent = new GUIContent("AI Game Builder");
            window.minSize = new Vector2(360, 300);
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawTranscript();
            DrawInput();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Initialize", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    Initialize();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label(_status.Label(), EditorStyles.miniLabel);
            }
            if (!string.IsNullOrEmpty(_initMessage))
            {
                EditorGUILayout.HelpBox(_initMessage, MessageType.Info);
            }
        }

        private void DrawTranscript()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var line in _transcript)
            {
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawInput()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _input = EditorGUILayout.TextField(_input);
                if (GUILayout.Button("Send", GUILayout.Width(60)) && !string.IsNullOrWhiteSpace(_input))
                {
                    Send(_input);
                    _input = "";
                    GUIUtility.keyboardControl = 0;
                }
            }
        }

        // Overridden in Phase 2 to drive the subprocess. Echo for now.
        protected virtual void Send(string prompt)
        {
            _transcript.Add("You: " + prompt);
            _transcript.Add("(echo) " + prompt);
        }

        private void Initialize()
        {
            bool claudeOnPath = FindOnPath("claude") != null;
            _initMessage = claudeOnPath
                ? "claude found. Ensure the Unity editor's MCP bridge is running."
                : "claude NOT found on PATH. Install Claude Code first.";
        }

        private static string FindOnPath(string exe)
        {
            var path = System.Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                var candidate = Path.Combine(dir, exe);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
    }
}
