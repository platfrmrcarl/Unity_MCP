# Unity MCP "AI Game Builder" Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Unity 6 editor window that lets the author build games by typing plain English; the window drives the Claude Code CLI as a subprocess, which uses the Unity MCP and Blender MCP servers to do the work.

**Architecture:** A custom `EditorWindow` ("AI Game Builder") spawns `claude -p --output-format stream-json` as a background process, streams parsed events into a chat panel, and marshals all UI updates onto Unity's main thread. Unity manipulation comes from the third-party CoplayDev Unity MCP package; 3D assets come from the already-connected Blender MCP. We write only the chat UI, the subprocess driver, and the stream parser.

**Tech Stack:** Unity `6000.5.3f1` (URP, Linux), C# (IMGUI editor UI), Newtonsoft.Json, Claude Code CLI `2.1.210`, `uv`/`uvx`, Unity Test Framework (EditMode).

## Global Constraints

- **Unity version:** `6000.5.3f1`. Editor binary: `/home/carl/Unity/Hub/Editor/6000.5.3f1/Editor/Unity`.
- **Platform:** Linux only. Do not add Windows/macOS handling.
- **Repo root:** `/home/carl/GitHub/Unity_MCP` — the Unity project lives here (`Assets/`, `Packages/`, `ProjectSettings/` at root).
- **Our code namespace:** `AIGameBuilder`. All our C# lives under `Assets/AIGameBuilder/`.
- **UI toolkit:** IMGUI (`OnGUI`), not UI Toolkit — simplest for a prototype.
- **Never block the editor UI thread:** all subprocess I/O runs on background threads; UI mutations are enqueued and drained in `EditorApplication.update`.
- **Subprocess command (canonical form):**
  `claude -p "<prompt>" --output-format stream-json --verbose --mcp-config <repoRoot>/.mcp.json --strict-mcp-config --permission-mode bypassPermissions` (add `--continue` from Task 3.3 onward). `--verbose` is required by Claude Code whenever `--output-format stream-json` is used with `-p`.
- **Blender runs as a Flatpak** (`flatpak run org.blender.Blender`); it is not on PATH.
- **Commit after every task.** Do not push unless asked.

---

## File Structure

- `.gitignore` — Unity ignore rules (root).
- `Assets/`, `Packages/`, `ProjectSettings/` — the Unity URP project (Task 0.1).
- `Packages/manifest.json` — add CoplayDev Unity MCP + Newtonsoft (Tasks 0.2).
- `.mcp.json` — MCP servers (`unity`, `blender`) for the spawned Claude (Task 0.3).
- `.claude/settings.local.json` — tool allowlist for the subprocess (Task 0.3).
- `Assets/AIGameBuilder/Editor/AIGameBuilder.Editor.asmdef` — editor assembly (Task 1.1).
- `Assets/AIGameBuilder/Editor/AIGameBuilderWindow.cs` — the chat window (Tasks 1.1, 1.2, 2.3, 3.x).
- `Assets/AIGameBuilder/Editor/BuilderStatus.cs` — status enum + labels (Task 1.2).
- `Assets/AIGameBuilder/Editor/StreamEvent.cs` — parsed event struct (Task 2.1).
- `Assets/AIGameBuilder/Editor/StreamJsonParser.cs` — pure JSON→StreamEvent logic (Task 2.1).
- `Assets/AIGameBuilder/Editor/StatusMapper.cs` — tool name → BuilderStatus (Task 2.1).
- `Assets/AIGameBuilder/Editor/ClaudeCodeProcess.cs` — subprocess driver (Task 2.2).
- `Assets/AIGameBuilder/Editor/ContextHeader.cs` — scene/selection/error context (Task 3.1).
- `Assets/AIGameBuilder/Tests/Editor/AIGameBuilder.Tests.asmdef` — test assembly (Task 2.1).
- `Assets/AIGameBuilder/Tests/Editor/StreamJsonParserTests.cs` — parser unit tests (Task 2.1).
- `Assets/AIGameBuilder/Tests/Editor/StatusMapperTests.cs` — mapper unit tests (Task 2.1).
- `README.md` — setup + manual end-to-end checklist (Task 3.3).

---

## PHASE 0 — Prove the pipeline (no custom UI)

### Task 0.1: Create the Unity URP project at the repo root

**Files:**
- Create: `.gitignore`
- Create: `Assets/`, `Packages/`, `ProjectSettings/` (via Unity Hub)

**Interfaces:**
- Produces: a Unity `6000.5.3f1` URP project openable at the repo root.

- [ ] **Step 1: Write the Unity `.gitignore`**

Create `/home/carl/GitHub/Unity_MCP/.gitignore`:

```gitignore
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]serSettings/
.vs/
.vsconfig
*.csproj
*.sln
*.user
.DS_Store
```

- [ ] **Step 2: Create the URP project via Unity Hub (manual, reliable)**

CLI project creation cannot select the URP template, so use Hub once:
1. Open Unity Hub → **New Project**.
2. Select the **Universal 3D** (URP) template, Editor version `6000.5.3f1`.
3. Project name: `Unity_MCP_seed`. Location: `/home/carl/GitHub`.
4. Create, let it finish opening, then close the editor.

- [ ] **Step 3: Move the Unity folders into the repo root**

Run:
```bash
cd /home/carl/GitHub/Unity_MCP
mv ../Unity_MCP_seed/Assets ../Unity_MCP_seed/Packages ../Unity_MCP_seed/ProjectSettings .
rm -rf ../Unity_MCP_seed
```
Expected: `Assets/`, `Packages/`, `ProjectSettings/` now exist at the repo root.

- [ ] **Step 4: Verify the project opens headlessly**

Run:
```bash
/home/carl/Unity/Hub/Editor/6000.5.3f1/Editor/Unity -batchmode -quit -projectPath /home/carl/GitHub/Unity_MCP -logFile - 2>&1 | tail -20
```
Expected: log ends without fatal errors; exit code 0. (First import may take a minute.)

- [ ] **Step 5: Commit**

```bash
cd /home/carl/GitHub/Unity_MCP
git add .gitignore Assets Packages ProjectSettings
git commit -m "chore: scaffold Unity 6 URP project at repo root"
```

---

### Task 0.2: Install CoplayDev Unity MCP + Newtonsoft.Json

**Files:**
- Modify: `Packages/manifest.json`

**Interfaces:**
- Produces: the Unity MCP editor bridge and a Python MCP server command; the `Newtonsoft.Json` assembly available to our code.

- [ ] **Step 1: Get the current CoplayDev install URL**

WebFetch `https://github.com/CoplayDev/unity-mcp` (README) and extract the exact "Add package from git URL" string (it looks like `https://github.com/CoplayDev/unity-mcp.git?path=/<subdir>`). Use whatever the current README specifies — do not guess the subdir.

- [ ] **Step 2: Add both packages to the manifest**

Edit `Packages/manifest.json`, adding to the `dependencies` object (use the URL from Step 1 for the git entry):

```json
"com.coplaydev.unity-mcp": "<git-url-from-step-1>",
"com.unity.nuget.newtonsoft-json": "3.2.1"
```

- [ ] **Step 3: Let Unity resolve packages**

Run:
```bash
/home/carl/Unity/Hub/Editor/6000.5.3f1/Editor/Unity -batchmode -quit -projectPath /home/carl/GitHub/Unity_MCP -logFile - 2>&1 | tail -30
```
Expected: packages resolve without compile errors in the log.

- [ ] **Step 4: Verify the bridge in-editor (manual)**

Open the project in the Unity editor GUI. Open the CoplayDev window (menu named per its README, e.g. **Window > MCP For Unity**). Confirm it reports the bridge is running/listening and note the exact **Python server command** it displays (a `uv --directory <path> run server.py` style command). Save that command for Task 0.3.

- [ ] **Step 5: Commit**

```bash
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "chore: add CoplayDev Unity MCP and Newtonsoft.Json packages"
```

---

### Task 0.3: Configure `.mcp.json` and subprocess permissions

**Files:**
- Create: `.mcp.json`
- Create: `.claude/settings.local.json`

**Interfaces:**
- Consumes: the Unity MCP Python server command from Task 0.2 Step 4.
- Produces: `.mcp.json` that `claude` loads to reach both MCP servers.

- [ ] **Step 1: Write `.mcp.json`**

Create `/home/carl/GitHub/Unity_MCP/.mcp.json` (replace the `unity` command/args with the exact ones from Task 0.2 Step 4):

```json
{
  "mcpServers": {
    "unity": {
      "command": "uv",
      "args": ["--directory", "<path-from-task-0.2-step-4>", "run", "server.py"]
    },
    "blender": {
      "command": "uvx",
      "args": ["blender-mcp"]
    }
  }
}
```

- [ ] **Step 2: Write the subprocess allowlist**

Create `/home/carl/GitHub/Unity_MCP/.claude/settings.local.json`:

```json
{
  "permissions": {
    "allow": ["mcp__unity", "mcp__blender", "Read", "Write", "Edit", "Bash"]
  }
}
```

- [ ] **Step 3: Verify Claude sees both servers**

Run (with the Unity editor open so the bridge is up):
```bash
cd /home/carl/GitHub/Unity_MCP
claude mcp list
```
Expected: `unity` and `blender` both listed and reporting connected/available.

- [ ] **Step 4: Commit**

```bash
git add .mcp.json .claude/settings.local.json
git commit -m "chore: configure MCP servers and subprocess permissions"
```

---

### Task 0.4: Hand-driven end-to-end proof (GATE)

**Files:** none (manual verification).

**Interfaces:** Consumes everything from Tasks 0.1–0.3.

- [ ] **Step 1: Run one full request through terminal Claude**

With the Unity editor open (bridge running), run:
```bash
cd /home/carl/GitHub/Unity_MCP
claude -p "Create a C# script Assets/Scripts/Spinner.cs that rotates its GameObject, then create a Cube GameObject in the active scene and attach Spinner to it." --output-format stream-json --verbose --mcp-config /home/carl/GitHub/Unity_MCP/.mcp.json --strict-mcp-config --permission-mode bypassPermissions
```

- [ ] **Step 2: Verify the result**

Expected: `Assets/Scripts/Spinner.cs` exists and compiles; a Cube with the `Spinner` component exists in the active scene (check in the editor). This proves the Unity MCP path.

- [ ] **Step 3: Prove the Blender path (best-effort)**

Run:
```bash
claude -p "Use the Blender MCP to generate a simple low-poly rock model and export it into Assets/Models/." --output-format stream-json --verbose --mcp-config /home/carl/GitHub/Unity_MCP/.mcp.json --strict-mcp-config --permission-mode bypassPermissions
```
Expected: a model file appears under `Assets/Models/`. If Blender asset generation is unavailable, record the failure in the README's "Known limitations" — do not block the rest of the plan on it.

- [ ] **Step 4: Commit any generated proof assets**

```bash
git add Assets
git commit -m "test: end-to-end pipeline proof (AI-generated script + asset)"
```

**GATE:** Do not start Phase 1 until Step 2 passes.

---

## PHASE 1 — Window shell (echo only, no AI)

### Task 1.1: Editor assembly + empty window

**Files:**
- Create: `Assets/AIGameBuilder/Editor/AIGameBuilder.Editor.asmdef`
- Create: `Assets/AIGameBuilder/Editor/AIGameBuilderWindow.cs`

**Interfaces:**
- Produces: `AIGameBuilder.Editor` assembly; menu item `Window/AI Game Builder` opening `AIGameBuilderWindow`.

- [ ] **Step 1: Create the editor asmdef**

Create `Assets/AIGameBuilder/Editor/AIGameBuilder.Editor.asmdef`:

```json
{
  "name": "AIGameBuilder.Editor",
  "references": ["Unity.Nuget.Newtonsoft-Json"],
  "includePlatforms": ["Editor"],
  "overrideReferences": false,
  "autoReferenced": true
}
```

- [ ] **Step 2: Create the window with a menu item**

Create `Assets/AIGameBuilder/Editor/AIGameBuilderWindow.cs`:

```csharp
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
```

- [ ] **Step 3: Verify it compiles headlessly**

Run:
```bash
/home/carl/Unity/Hub/Editor/6000.5.3f1/Editor/Unity -batchmode -quit -projectPath /home/carl/GitHub/Unity_MCP -logFile - 2>&1 | grep -iE "error CS|Compilation failed" || echo "NO COMPILE ERRORS"
```
Expected: `NO COMPILE ERRORS`.

- [ ] **Step 4: Verify the window opens (manual)**

In the editor GUI: **Window > AI Game Builder** opens a dockable window titled "AI Game Builder".

- [ ] **Step 5: Commit**

```bash
git add Assets/AIGameBuilder
git commit -m "feat: add AI Game Builder editor window shell"
```

---

### Task 1.2: Chat UI, status enum, echo, Initialize button

**Files:**
- Create: `Assets/AIGameBuilder/Editor/BuilderStatus.cs`
- Modify: `Assets/AIGameBuilder/Editor/AIGameBuilderWindow.cs`

**Interfaces:**
- Produces: `enum BuilderStatus { Idle, Thinking, EditingScene, ModelingInBlender, Compiling }`, `BuilderStatusExtensions.Label(this BuilderStatus)`; window fields `_transcript` (List<string>), `_input` (string), `_status` (BuilderStatus).

- [ ] **Step 1: Create the status enum + labels**

Create `Assets/AIGameBuilder/Editor/BuilderStatus.cs`:

```csharp
namespace AIGameBuilder
{
    public enum BuilderStatus
    {
        Idle,
        Thinking,
        EditingScene,
        ModelingInBlender,
        Compiling
    }

    public static class BuilderStatusExtensions
    {
        public static string Label(this BuilderStatus status)
        {
            switch (status)
            {
                case BuilderStatus.Thinking: return "Thinking...";
                case BuilderStatus.EditingScene: return "Editing scene...";
                case BuilderStatus.ModelingInBlender: return "Modeling in Blender...";
                case BuilderStatus.Compiling: return "Compiling...";
                default: return "Idle";
            }
        }
    }
}
```

- [ ] **Step 2: Build the chat UI with echo + Initialize**

Replace the body of `AIGameBuilderWindow.cs` with:

```csharp
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
```

- [ ] **Step 3: Verify it compiles headlessly**

Run:
```bash
/home/carl/Unity/Hub/Editor/6000.5.3f1/Editor/Unity -batchmode -quit -projectPath /home/carl/GitHub/Unity_MCP -logFile - 2>&1 | grep -iE "error CS|Compilation failed" || echo "NO COMPILE ERRORS"
```
Expected: `NO COMPILE ERRORS`.

- [ ] **Step 4: Verify behavior (manual)**

Open the window: typing text + Send appends "You: ..." and "(echo) ..."; Initialize shows the claude-found message; status shows "Idle".

- [ ] **Step 5: Commit**

```bash
git add Assets/AIGameBuilder
git commit -m "feat: chat UI, status indicator, echo, and Initialize check"
```

---

## PHASE 2 — Subprocess + streaming

### Task 2.1: StreamEvent, StreamJsonParser, StatusMapper (TDD)

**Files:**
- Create: `Assets/AIGameBuilder/Editor/StreamEvent.cs`
- Create: `Assets/AIGameBuilder/Editor/StreamJsonParser.cs`
- Create: `Assets/AIGameBuilder/Editor/StatusMapper.cs`
- Create: `Assets/AIGameBuilder/Tests/Editor/AIGameBuilder.Tests.asmdef`
- Create: `Assets/AIGameBuilder/Tests/Editor/StreamJsonParserTests.cs`
- Create: `Assets/AIGameBuilder/Tests/Editor/StatusMapperTests.cs`

**Interfaces:**
- Produces:
  - `enum StreamEventKind { Init, AssistantText, ToolUse, Result, Unknown }`
  - `struct StreamEvent { StreamEventKind Kind; string Text; string ToolName; }`
  - `static StreamEvent StreamJsonParser.ParseLine(string jsonLine)`
  - `static BuilderStatus StatusMapper.ForTool(string toolName)`

- [ ] **Step 1: Create the test asmdef**

Create `Assets/AIGameBuilder/Tests/Editor/AIGameBuilder.Tests.asmdef`:

```json
{
  "name": "AIGameBuilder.Tests",
  "references": ["AIGameBuilder.Editor", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "optionalUnityReferences": ["TestAssemblies"],
  "autoReferenced": false
}
```

- [ ] **Step 2: Write the failing parser tests**

Create `Assets/AIGameBuilder/Tests/Editor/StreamJsonParserTests.cs`:

```csharp
using NUnit.Framework;
using AIGameBuilder;

public class StreamJsonParserTests
{
    [Test]
    public void Init_line_parses_as_Init()
    {
        var e = StreamJsonParser.ParseLine("{\"type\":\"system\",\"subtype\":\"init\"}");
        Assert.AreEqual(StreamEventKind.Init, e.Kind);
    }

    [Test]
    public void Assistant_text_is_extracted()
    {
        var line = "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"Hello\"}]}}";
        var e = StreamJsonParser.ParseLine(line);
        Assert.AreEqual(StreamEventKind.AssistantText, e.Kind);
        Assert.AreEqual("Hello", e.Text);
    }

    [Test]
    public void Tool_use_name_is_extracted()
    {
        var line = "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"mcp__blender__generate_hyper3d_model_via_text\",\"input\":{}}]}}";
        var e = StreamJsonParser.ParseLine(line);
        Assert.AreEqual(StreamEventKind.ToolUse, e.Kind);
        Assert.AreEqual("mcp__blender__generate_hyper3d_model_via_text", e.ToolName);
    }

    [Test]
    public void Result_line_parses_as_Result()
    {
        var e = StreamJsonParser.ParseLine("{\"type\":\"result\",\"subtype\":\"success\"}");
        Assert.AreEqual(StreamEventKind.Result, e.Kind);
    }

    [Test]
    public void Garbage_line_parses_as_Unknown_without_throwing()
    {
        var e = StreamJsonParser.ParseLine("not json");
        Assert.AreEqual(StreamEventKind.Unknown, e.Kind);
    }
}
```

- [ ] **Step 3: Write the failing status-mapper tests**

Create `Assets/AIGameBuilder/Tests/Editor/StatusMapperTests.cs`:

```csharp
using NUnit.Framework;
using AIGameBuilder;

public class StatusMapperTests
{
    [Test]
    public void Blender_tools_map_to_ModelingInBlender()
    {
        Assert.AreEqual(BuilderStatus.ModelingInBlender, StatusMapper.ForTool("mcp__blender__generate_hyper3d_model_via_text"));
    }

    [Test]
    public void Unity_tools_map_to_EditingScene()
    {
        Assert.AreEqual(BuilderStatus.EditingScene, StatusMapper.ForTool("mcp__unity__manage_gameobject"));
    }

    [Test]
    public void Unknown_tool_maps_to_Thinking()
    {
        Assert.AreEqual(BuilderStatus.Thinking, StatusMapper.ForTool("Bash"));
    }
}
```

- [ ] **Step 4: Run tests to verify they fail (types missing)**

Run:
```bash
/home/carl/Unity/Hub/Editor/6000.5.3f1/Editor/Unity -runTests -batchmode -projectPath /home/carl/GitHub/Unity_MCP -testPlatform EditMode -testResults /tmp/results.xml -logFile - 2>&1 | grep -iE "error CS|Failed|Passed" | head
```
Expected: compile errors (types not defined) — i.e. tests do not pass.

- [ ] **Step 5: Implement StreamEvent**

Create `Assets/AIGameBuilder/Editor/StreamEvent.cs`:

```csharp
namespace AIGameBuilder
{
    public enum StreamEventKind { Init, AssistantText, ToolUse, Result, Unknown }

    public struct StreamEvent
    {
        public StreamEventKind Kind;
        public string Text;
        public string ToolName;

        public static StreamEvent Unknown => new StreamEvent { Kind = StreamEventKind.Unknown };
    }
}
```

- [ ] **Step 6: Implement StreamJsonParser**

Create `Assets/AIGameBuilder/Editor/StreamJsonParser.cs`:

```csharp
using Newtonsoft.Json.Linq;

namespace AIGameBuilder
{
    public static class StreamJsonParser
    {
        public static StreamEvent ParseLine(string jsonLine)
        {
            if (string.IsNullOrWhiteSpace(jsonLine)) return StreamEvent.Unknown;

            JObject root;
            try { root = JObject.Parse(jsonLine); }
            catch { return StreamEvent.Unknown; }

            var type = (string)root["type"];
            switch (type)
            {
                case "system":
                    return new StreamEvent { Kind = StreamEventKind.Init };
                case "result":
                    return new StreamEvent { Kind = StreamEventKind.Result };
                case "assistant":
                    return ParseAssistant(root);
                default:
                    return StreamEvent.Unknown;
            }
        }

        private static StreamEvent ParseAssistant(JObject root)
        {
            var content = root["message"]?["content"] as JArray;
            if (content == null) return StreamEvent.Unknown;

            foreach (var block in content)
            {
                var blockType = (string)block["type"];
                if (blockType == "tool_use")
                {
                    return new StreamEvent
                    {
                        Kind = StreamEventKind.ToolUse,
                        ToolName = (string)block["name"]
                    };
                }
                if (blockType == "text")
                {
                    return new StreamEvent
                    {
                        Kind = StreamEventKind.AssistantText,
                        Text = (string)block["text"]
                    };
                }
            }
            return StreamEvent.Unknown;
        }
    }
}
```

- [ ] **Step 7: Implement StatusMapper**

Create `Assets/AIGameBuilder/Editor/StatusMapper.cs`:

```csharp
namespace AIGameBuilder
{
    public static class StatusMapper
    {
        public static BuilderStatus ForTool(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return BuilderStatus.Thinking;
            if (toolName.StartsWith("mcp__blender__")) return BuilderStatus.ModelingInBlender;
            if (toolName.StartsWith("mcp__unity__")) return BuilderStatus.EditingScene;
            return BuilderStatus.Thinking;
        }
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run:
```bash
/home/carl/Unity/Hub/Editor/6000.5.3f1/Editor/Unity -runTests -batchmode -projectPath /home/carl/GitHub/Unity_MCP -testPlatform EditMode -testResults /tmp/results.xml -logFile - 2>&1 | tail -5; grep -oE 'result="[A-Za-z]+"|total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' /tmp/results.xml | head
```
Expected: all tests pass (`failed="0"`).

- [ ] **Step 9: Commit**

```bash
git add Assets/AIGameBuilder
git commit -m "feat: stream-json parser and tool->status mapper with tests"
```

---

### Task 2.2: ClaudeCodeProcess subprocess driver

**Files:**
- Create: `Assets/AIGameBuilder/Editor/ClaudeCodeProcess.cs`

**Interfaces:**
- Consumes: `StreamJsonParser.ParseLine`, `StreamEvent`.
- Produces: `class ClaudeCodeProcess` with:
  - `ClaudeCodeProcess(string repoRoot)`
  - `void Send(string prompt)` — starts a `claude -p` process for the prompt.
  - `Queue<StreamEvent> DrainEvents()` — thread-safe drain of parsed events (call on main thread).
  - `bool IsRunning { get; }`
  - `void Cancel()` — kills the running process.

- [ ] **Step 1: Implement the driver**

Create `Assets/AIGameBuilder/Editor/ClaudeCodeProcess.cs`:

```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace AIGameBuilder
{
    public class ClaudeCodeProcess
    {
        private readonly string _repoRoot;
        private readonly Queue<StreamEvent> _events = new Queue<StreamEvent>();
        private readonly object _lock = new object();
        private Process _process;

        public ClaudeCodeProcess(string repoRoot) { _repoRoot = repoRoot; }

        public bool IsRunning { get { return _process != null && !_process.HasExited; } }

        public void Send(string prompt)
        {
            var mcpConfig = System.IO.Path.Combine(_repoRoot, ".mcp.json");
            var psi = new ProcessStartInfo
            {
                FileName = "claude",
                WorkingDirectory = _repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-p");
            psi.ArgumentList.Add(prompt);
            psi.ArgumentList.Add("--output-format"); psi.ArgumentList.Add("stream-json");
            psi.ArgumentList.Add("--verbose");
            psi.ArgumentList.Add("--mcp-config"); psi.ArgumentList.Add(mcpConfig);
            psi.ArgumentList.Add("--strict-mcp-config");
            psi.ArgumentList.Add("--permission-mode"); psi.ArgumentList.Add("bypassPermissions");

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                var parsed = StreamJsonParser.ParseLine(e.Data);
                lock (_lock) { _events.Enqueue(parsed); }
            };
            _process.Start();
            _process.BeginOutputReadLine();
            // Drain stderr on a background thread so it never blocks.
            new Thread(() => { try { _process.StandardError.ReadToEnd(); } catch { } })
                { IsBackground = true }.Start();
        }

        public Queue<StreamEvent> DrainEvents()
        {
            var drained = new Queue<StreamEvent>();
            lock (_lock)
            {
                while (_events.Count > 0) drained.Enqueue(_events.Dequeue());
            }
            return drained;
        }

        public void Cancel()
        {
            try { if (IsRunning) _process.Kill(); } catch { }
        }
    }
}
```

- [ ] **Step 2: Verify it compiles headlessly**

Run:
```bash
/home/carl/Unity/Hub/Editor/6000.5.3f1/Editor/Unity -batchmode -quit -projectPath /home/carl/GitHub/Unity_MCP -logFile - 2>&1 | grep -iE "error CS|Compilation failed" || echo "NO COMPILE ERRORS"
```
Expected: `NO COMPILE ERRORS`.

- [ ] **Step 3: Commit**

```bash
git add Assets/AIGameBuilder
git commit -m "feat: ClaudeCodeProcess subprocess driver with thread-safe event queue"
```

---

### Task 2.3: Wire subprocess into the window (GATE)

**Files:**
- Modify: `Assets/AIGameBuilder/Editor/AIGameBuilderWindow.cs`

**Interfaces:**
- Consumes: `ClaudeCodeProcess`, `StreamEvent`, `StatusMapper`.

- [ ] **Step 1: Replace echo `Send` with real subprocess + main-thread pump**

In `AIGameBuilderWindow.cs`: add a `ClaudeCodeProcess _proc;` field, subscribe/unsubscribe to `EditorApplication.update`, and replace the echo `Send`:

```csharp
private ClaudeCodeProcess _proc;

private void OnEnable()
{
    _proc = new ClaudeCodeProcess("/home/carl/GitHub/Unity_MCP");
    EditorApplication.update += Pump;
}

private void OnDisable()
{
    EditorApplication.update -= Pump;
    _proc?.Cancel();
}

protected override void Send(string prompt)
{
    _transcript.Add("You: " + prompt);
    _status = BuilderStatus.Thinking;
    _proc.Send(prompt);
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
                if (!string.IsNullOrEmpty(e.Text)) _transcript.Add("AI: " + e.Text);
                break;
            case StreamEventKind.ToolUse:
                _status = StatusMapper.ForTool(e.ToolName);
                _transcript.Add("  · " + e.ToolName);
                break;
            case StreamEventKind.Result:
                _status = BuilderStatus.Idle;
                break;
        }
    }
    if (changed) Repaint();
}
```

Change the class declaration's `Send` from `protected virtual` (Task 1.2) to keep `protected override` here consistent — since Task 1.2's `Send` was `protected virtual`, this override compiles. (If you inlined Task 1.2 differently, make Task 1.2's `Send` `protected virtual` and this one `protected override`.)

- [ ] **Step 2: Verify it compiles headlessly**

Run:
```bash
/home/carl/Unity/Hub/Editor/6000.5.3f1/Editor/Unity -batchmode -quit -projectPath /home/carl/GitHub/Unity_MCP -logFile - 2>&1 | grep -iE "error CS|Compilation failed" || echo "NO COMPILE ERRORS"
```
Expected: `NO COMPILE ERRORS`.

- [ ] **Step 3: Verify end-to-end from the window (manual, GATE)**

With the editor open and MCP bridge running, open the window, type "Create a Sphere named TestBall in the active scene." and Send. Expected: tool-activity lines appear, status changes, the Sphere appears in the scene, status returns to Idle, and the editor stays responsive throughout.

- [ ] **Step 4: Commit**

```bash
git add Assets/AIGameBuilder
git commit -m "feat: drive Claude Code from the window and stream results into chat"
```

**GATE:** Step 3 must work before Phase 3.

---

## PHASE 3 — Context + polish

### Task 3.1: Context header (scene, selection, recent errors)

**Files:**
- Create: `Assets/AIGameBuilder/Editor/ContextHeader.cs`
- Modify: `Assets/AIGameBuilder/Editor/AIGameBuilderWindow.cs`

**Interfaces:**
- Produces: `static string ContextHeader.Build(IEnumerable<string> recentErrors)` returning a text block with active scene name, selected object names, and recent errors.

- [ ] **Step 1: Implement ContextHeader**

Create `Assets/AIGameBuilder/Editor/ContextHeader.cs`:

```csharp
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
```

- [ ] **Step 2: Capture recent errors + prepend context in the window**

In `AIGameBuilderWindow.cs`: add a recent-errors ring buffer fed by `Application.logMessageReceived`, and prepend the header in `Send`:

```csharp
private readonly List<string> _recentErrors = new List<string>();

// add inside OnEnable():
Application.logMessageReceived += OnLog;
// add inside OnDisable():
Application.logMessageReceived -= OnLog;

private void OnLog(string condition, string stackTrace, LogType type)
{
    if (type == LogType.Error || type == LogType.Exception)
    {
        _recentErrors.Add(condition);
        while (_recentErrors.Count > 5) _recentErrors.RemoveAt(0);
    }
}
```

And change `Send` to prepend the header:

```csharp
protected override void Send(string prompt)
{
    _transcript.Add("You: " + prompt);
    _status = BuilderStatus.Thinking;
    var full = ContextHeader.Build(_recentErrors) + "\n" + prompt;
    _proc.Send(full);
}
```

- [ ] **Step 3: Verify it compiles headlessly**

Run:
```bash
/home/carl/Unity/Hub/Editor/6000.5.3f1/Editor/Unity -batchmode -quit -projectPath /home/carl/GitHub/Unity_MCP -logFile - 2>&1 | grep -iE "error CS|Compilation failed" || echo "NO COMPILE ERRORS"
```
Expected: `NO COMPILE ERRORS`.

- [ ] **Step 4: Commit**

```bash
git add Assets/AIGameBuilder
git commit -m "feat: inject scene/selection/error context into each prompt"
```

---

### Task 3.2: Conversation continuity + cancel button

**Files:**
- Modify: `Assets/AIGameBuilder/Editor/ClaudeCodeProcess.cs`
- Modify: `Assets/AIGameBuilder/Editor/AIGameBuilderWindow.cs`

**Interfaces:**
- Modifies: `ClaudeCodeProcess.Send(string prompt, bool continueConversation)`.

- [ ] **Step 1: Add `--continue` support to the driver**

In `ClaudeCodeProcess.cs`, change `Send` to accept continuity and add the flag after the permission-mode args:

```csharp
public void Send(string prompt, bool continueConversation)
{
    // ... unchanged up through the permission-mode ArgumentList.Add calls ...
    if (continueConversation)
    {
        psi.ArgumentList.Add("--continue");
    }
    // ... unchanged process start ...
}
```

(Move the `--continue` add to before `_process = new Process(...)`.)

- [ ] **Step 2: Track first-message + add a Cancel button**

In `AIGameBuilderWindow.cs`: add `private bool _conversationStarted;`, pass continuity, and add a Cancel button to the toolbar:

```csharp
protected override void Send(string prompt)
{
    _transcript.Add("You: " + prompt);
    _status = BuilderStatus.Thinking;
    var full = ContextHeader.Build(_recentErrors) + "\n" + prompt;
    _proc.Send(full, _conversationStarted);
    _conversationStarted = true;
}
```

In `DrawToolbar`, after the Initialize button:

```csharp
using (new EditorGUI.DisabledScope(!_proc.IsRunning))
{
    if (GUILayout.Button("Cancel", EditorStyles.toolbarButton, GUILayout.Width(60)))
    {
        _proc.Cancel();
        _status = BuilderStatus.Idle;
    }
}
```

- [ ] **Step 3: Verify it compiles headlessly**

Run:
```bash
/home/carl/Unity/Hub/Editor/6000.5.3f1/Editor/Unity -batchmode -quit -projectPath /home/carl/GitHub/Unity_MCP -logFile - 2>&1 | grep -iE "error CS|Compilation failed" || echo "NO COMPILE ERRORS"
```
Expected: `NO COMPILE ERRORS`.

- [ ] **Step 4: Verify continuity (manual)**

Send "Create a Cube named Foo.", then send "Rename it to Bar." Expected: the second request acts on the same conversation (renames the Cube), proving `--continue` works. Cancel mid-run stops the process and resets status.

- [ ] **Step 5: Commit**

```bash
git add Assets/AIGameBuilder
git commit -m "feat: conversation continuity via --continue and a Cancel button"
```

---

### Task 3.3: README + full demo (GATE)

**Files:**
- Create: `README.md`

**Interfaces:** none.

- [ ] **Step 1: Write the README**

Create `README.md` documenting: prerequisites (Unity `6000.5.3f1`, `claude`, `uv`/`uvx`, Flatpak Blender), setup steps (open project, packages resolve, open MCP For Unity bridge, `Window > AI Game Builder`, Initialize), the manual end-to-end checklist, known limitations (Blender asset gen best-effort; no accept/reject gating; Linux only; use `git` to roll back), and the deferred FRD items.

- [ ] **Step 2: Run the full demo (GATE)**

With everything running, from the window send: "Create a simple third-person setup: a Capsule named Player at the origin, add a Rigidbody, and create Assets/Scripts/PlayerMove.cs that moves it with WASD, then attach it." Expected: script created + compiled, Capsule with Rigidbody + PlayerMove in the scene, status transitions visible, editor responsive. Press Play and confirm WASD moves the Capsule.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: setup guide and manual end-to-end checklist"
```

**GATE:** Step 2 is the definition of done for the prototype.

---

## Self-Review Notes (coverage vs. spec)

- FR-1.1 single-package: N/A for a personal prototype (deferred — noted in README).
- FR-1.2 wizard → Initialize button (Task 1.2) + README (Task 3.3).
- FR-1.3 server lifecycle → CoplayDev bridge auto-runs in-editor; Initialize checks it (Task 1.2).
- FR-2.1 chat window → Tasks 1.1–1.2, 2.3.
- FR-2.2 context-aware → Task 3.1.
- FR-2.3 status streaming → Tasks 1.2 (enum), 2.1 (mapper), 2.3 (wiring).
- FR-3.1–3.4 code gen / components / scene / compile → CoplayDev Unity MCP, proven in Task 0.4, driven from Task 2.3 onward.
- FR-4.1–4.3 Blender assets → Blender MCP, proven best-effort in Task 0.4 Step 3.
- FR-4.4 auto PBR → best-effort (deferred note in README).
- FR-5.1/5.2 accept-reject / rollback → deferred to `git` (README).
- NFR-1 non-blocking → Tasks 2.2 (threads + queue), 2.3 (`EditorApplication.update` pump).
- NFR-4 localhost → inherited from both MCP servers (`.mcp.json`, README note).
```
