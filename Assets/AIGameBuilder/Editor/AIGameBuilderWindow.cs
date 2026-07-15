using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AIGameBuilder
{
    public class AIGameBuilderWindow : EditorWindow
    {
        private enum Role { User, Assistant, Tool }

        private struct ChatMessage
        {
            public Role Role;
            public string Text;
        }

        private readonly List<ChatMessage> _messages = new List<ChatMessage>();
        private string _input = "";
        private Vector2 _scroll;
        private bool _scrollToBottom;
        private BuilderStatus _status = BuilderStatus.Idle;
        private string _initMessage = "";
        private ClaudeCodeProcess _proc;
        private readonly List<string> _recentErrors = new List<string>();
        private readonly object _errorsLock = new object();
        private bool _conversationStarted;

        // Lazily built inside OnGUI (EditorStyles is only valid during GUI).
        private GUIStyle _userBubble, _aiBubble, _toolStyle, _roleLabel, _inputStyle, _placeholder, _greeting;

        private const string InputControlName = "ChatInput";

        [MenuItem("Window/AI Game Builder")]
        public static void Open()
        {
            var window = GetWindow<AIGameBuilderWindow>();
            window.titleContent = new GUIContent("AI Game Builder");
            window.minSize = new Vector2(360, 300);
        }

        private void OnEnable()
        {
            _proc = new ClaudeCodeProcess("/home/carl/GitHub/Unity_MCP");
            EditorApplication.update += Pump;
            Application.logMessageReceived += OnLog;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Pump;
            Application.logMessageReceived -= OnLog;
            _proc?.Cancel();
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                lock (_errorsLock)
                {
                    _recentErrors.Add(condition);
                    while (_recentErrors.Count > 5) _recentErrors.RemoveAt(0);
                }
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            DrawTranscript();
            DrawInput();
        }

        private void EnsureStyles()
        {
            if (_userBubble != null) return;

            _userBubble = new GUIStyle(EditorStyles.helpBox)
            {
                wordWrap = true,
                fontSize = 12,
                padding = new RectOffset(10, 10, 8, 8)
            };
            _userBubble.normal.textColor = Color.white;

            _aiBubble = new GUIStyle(EditorStyles.helpBox)
            {
                wordWrap = true,
                fontSize = 12,
                padding = new RectOffset(10, 10, 8, 8)
            };

            _toolStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                fontStyle = FontStyle.Italic
            };

            _roleLabel = new GUIStyle(EditorStyles.miniLabel);

            _inputStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                fontSize = 12,
                padding = new RectOffset(8, 8, 6, 6)
            };

            _placeholder = new GUIStyle(EditorStyles.label) { fontSize = 12 };
            _placeholder.normal.textColor = new Color(0.5f, 0.5f, 0.5f);

            _greeting = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            _greeting.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Initialize", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    Initialize();
                }
                using (new EditorGUI.DisabledScope(!_proc.IsRunning))
                {
                    if (GUILayout.Button("Cancel", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        _proc.Cancel();
                        _status = BuilderStatus.Idle;
                    }
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

            if (_messages.Count == 0)
            {
                GUILayout.Space(24);
                GUILayout.Label(
                    "Hi! Describe the game, scene, or object you'd like to build.\n\n" +
                    "e.g. \"Create a third-person player with a WASD controller.\"",
                    _greeting);
            }
            else
            {
                GUILayout.Space(4);
                foreach (var msg in _messages)
                {
                    switch (msg.Role)
                    {
                        case Role.User: DrawBubble(msg.Text, "You", isUser: true); break;
                        case Role.Assistant: DrawBubble(msg.Text, "AI Builder", isUser: false); break;
                        case Role.Tool: GUILayout.Label("     · " + msg.Text, _toolStyle); break;
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            if (_scrollToBottom && Event.current.type == EventType.Repaint)
            {
                _scroll.y = float.MaxValue;
                _scrollToBottom = false;
                Repaint();
            }
        }

        private void DrawBubble(string text, string role, bool isUser)
        {
            float maxWidth = position.width * 0.72f;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (isUser) GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(maxWidth)))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (isUser) GUILayout.FlexibleSpace();
                        GUILayout.Label(role, _roleLabel);
                        if (!isUser) GUILayout.FlexibleSpace();
                    }
                    var prev = GUI.backgroundColor;
                    GUI.backgroundColor = isUser
                        ? new Color(0.26f, 0.55f, 0.96f)   // blue
                        : new Color(0.62f, 0.62f, 0.62f);  // grey
                    GUILayout.Box(text, isUser ? _userBubble : _aiBubble);
                    GUI.backgroundColor = prev;
                }
                if (!isUser) GUILayout.FlexibleSpace();
            }
            GUILayout.Space(4);
        }

        private void DrawInput()
        {
            var e = Event.current;
            bool submit =
                e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
                !e.shift &&
                GUI.GetNameOfFocusedControl() == InputControlName;

            // Consume the Enter before the TextArea can insert a newline.
            if (submit) e.Use();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.SetNextControlName(InputControlName);
                _input = EditorGUILayout.TextArea(_input, _inputStyle,
                    GUILayout.MinHeight(38), GUILayout.MaxHeight(96));

                if (string.IsNullOrEmpty(_input))
                {
                    GUI.Label(GUILayoutUtility.GetLastRect(), "  Message the builder…", _placeholder);
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_input)))
                {
                    if (GUILayout.Button("Send", GUILayout.Width(60), GUILayout.Height(38)))
                    {
                        submit = true;
                    }
                }
            }

            GUILayout.Label("Enter to send · Shift+Enter for newline", EditorStyles.miniLabel);

            if (submit && !string.IsNullOrWhiteSpace(_input))
            {
                Send(_input.TrimEnd());
                _input = "";
                GUIUtility.keyboardControl = 0;
                _scrollToBottom = true;
            }
        }

        protected virtual void Send(string prompt)
        {
            _messages.Add(new ChatMessage { Role = Role.User, Text = prompt });
            _scrollToBottom = true;
            _status = BuilderStatus.Thinking;
            List<string> errorsSnapshot;
            lock (_errorsLock) { errorsSnapshot = new List<string>(_recentErrors); }
            var full = ContextHeader.Build(errorsSnapshot) + "\n" + prompt;
            _proc.Send(full, _conversationStarted);
            _conversationStarted = true;
        }

        private void Pump()
        {
            if (_proc == null) return;
            var events = _proc.DrainEvents();
            bool changed = events.Count > 0;
            while (events.Count > 0)
            {
                var e = events.Dequeue();
                switch (e.Kind)
                {
                    case StreamEventKind.AssistantText:
                        if (!string.IsNullOrEmpty(e.Text)) AppendAssistantText(e.Text);
                        break;
                    case StreamEventKind.ToolUse:
                        _status = StatusMapper.ForTool(e.ToolName);
                        _messages.Add(new ChatMessage { Role = Role.Tool, Text = e.ToolName });
                        _scrollToBottom = true;
                        break;
                    case StreamEventKind.Result:
                        _status = BuilderStatus.Idle;
                        break;
                }
            }
            if (changed) Repaint();
        }

        // Group consecutive assistant chunks into a single bubble; a tool call breaks the group.
        private void AppendAssistantText(string text)
        {
            if (_messages.Count > 0 && _messages[_messages.Count - 1].Role == Role.Assistant)
            {
                var last = _messages[_messages.Count - 1];
                last.Text = last.Text + "\n" + text;
                _messages[_messages.Count - 1] = last;
            }
            else
            {
                _messages.Add(new ChatMessage { Role = Role.Assistant, Text = text });
            }
            _scrollToBottom = true;
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
