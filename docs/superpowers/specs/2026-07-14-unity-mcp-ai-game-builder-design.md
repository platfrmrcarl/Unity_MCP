# Design: Unity MCP "AI Game Builder" (Linux Prototype)

**Date:** 2026-07-14
**Status:** Approved — ready for implementation planning
**Scope:** Working prototype for personal use, built fast by leaning on existing open-source MCP servers.

Derived from the Functional Requirements Document in `unity_mcp.md`. This spec deliberately narrows that FRD to a single, achievable prototype.

---

## 1. Goal & Intent

A functional tool the author personally uses inside Unity to build games by typing plain-English requests. Priority is a working end-to-end loop, not a shippable product. We assemble and extend proven components rather than writing everything from scratch.

**Non-goals for this prototype:** cross-platform distribution, a polished setup wizard, UPM packaging for others, and the full safety/rollback UI. These are noted as future work, not built now.

## 2. Environment (verified)

- **OS:** Linux (CachyOS). Windows/macOS explicitly out of scope for the prototype.
- **Unity:** `6000.5.3f1` (Unity 6.2) installed via Unity Hub at `~/Unity/Hub/Editor/6000.5.3f1/`.
- **Claude Code CLI:** `claude` `2.1.210` at `~/.local/bin/claude`.
- **uv / uvx:** present (`~/.local/bin`) — used to run the Python MCP servers.
- **python3, node, git:** present.
- **Blender:** `org.blender.Blender` **5.1** installed as a **Flatpak** (runs via `flatpak run org.blender.Blender`; not on PATH). A Blender MCP is already connected to the author's Claude session.
- **dotnet:** not installed — not required; Unity bundles its own C# compiler.
- **Repo:** greenfield git repo, remote `https://github.com/platfrmrcarl/Unity_MCP.git`, no commits yet. Contains `unity_mcp.md` (the FRD).

## 3. Architecture

```
+---------------------------------------------------+
|                  Unity Editor (Linux)             |
|  +-----------------------------+                  |
|  |  AI Game Builder (our C#)   |   live scene     |
|  |  chat + status + Initialize |   updates        |
|  +--------------+--------------+                  |
|                 | spawns                          |
+-----------------|---------------------------------+
                  v (System.Diagnostics.Process)
        claude -p --output-format stream-json
        --mcp-config .mcp.json
                  |
        +---------+-----------+
        v                     v
  Unity MCP server      Blender MCP server
  (CoplayDev, uv)       (uvx blender-mcp)
        |                     |
        v                     v
  Unity Editor bridge   Flatpak Blender + addon
  (C# TCP listener)     (asset generation/export)
```

The Unity chat window is only a UI + subprocess driver. All AI reasoning, the agent loop, MCP orchestration, streaming, and tool permissioning are handled by the Claude Code CLI we reuse. All Unity manipulation is done through the CoplayDev Unity MCP tools; all 3D asset generation through the Blender MCP.

**Chosen approach (Approach A):** Unity spawns the `claude -p` CLI as a subprocess. Rejected alternatives: direct Anthropic API in C# (reinvents the agent loop) and a Node Agent-SDK sidecar (unnecessary extra component for a prototype).

## 4. Components

### 4.1 Unity project (repo root)
- Unity `6000.5.3f1`, **3D (URP)** template — URP handles the FRD's PBR materials well and is the modern Unity 6 default.
- Standard layout at repo root: `Assets/`, `Packages/`, `ProjectSettings/`. Existing `unity_mcp.md` and new `docs/` live alongside.
- `.gitignore` for Unity (Library/, Temp/, obj/, Logs/, etc.).

### 4.2 CoplayDev Unity MCP (third-party, installed)
- Installed via Unity Package Manager git URL.
- Provides the Unity-side C# bridge (a TCP listener inside the editor) and a `uv`-run Python MCP server exposing tools such as `manage_script`, `manage_scene`, `manage_gameobject`, `manage_asset`, `read_console`, `execute_menu_item`.
- This is the FRD's "Unity MCP Server" (subsystems #2 and the Unity half of #3). We do not reimplement it.
- Covers FR-3.1 (script gen), FR-3.2 (component attach), FR-3.3 (scene manipulation), FR-3.4 (compile + console error read-back).

### 4.3 Blender MCP (third-party, already connected)
- `uvx blender-mcp` server talking to the Flatpak Blender via its addon.
- Registered in the repo `.mcp.json` so the spawned Claude Code sees it.
- Covers FR-4.1/4.2/4.3 (NL asset requests, generative modeling, export into `Assets/`). FR-4.4 (auto PBR materials) is best-effort via Blender-exported materials, not guaranteed.

### 4.4 Custom "AI Game Builder" extension (our code)
Location: `Assets/AIGameBuilder/Editor/`.

- **`AIGameBuilderWindow.cs`** — dockable `EditorWindow` (`Window > AI Game Builder`). Contains:
  - Scrollable chat transcript (user messages, assistant text, tool-activity lines).
  - Text input field + Send button.
  - **Status indicator** (FR-2.3): `Idle`, `Thinking`, `Editing scene`, `Modeling in Blender`, `Compiling`.
  - **Initialize** button (FR-1.3 lite): verifies `claude` is on PATH and the Unity MCP bridge is up; reports readiness.
- **`ClaudeCodeProcess.cs`** — wraps `System.Diagnostics.Process` running `claude -p --output-format stream-json --mcp-config <repo>/.mcp.json` with a scoped permission mode. Reads stdout asynchronously on a background thread; enqueues parsed events onto a thread-safe queue drained on Unity's main thread via `EditorApplication.update` (NFR-1: never block the UI thread). Handles process lifecycle (start, cancel, exit, error).
- **`StreamJsonParser.cs`** — pure C# that maps Claude Code stream-json events (assistant text, `tool_use` with tool name, result/errors) to chat lines and a status enum. Unit-tested (see §7).
- **`ContextHeader.cs`** — builds the FR-2.2 context header before each send: active scene name, selected GameObject names/paths, and recent console errors. Prepended to the user's prompt.

### 4.5 Configuration
- **`.mcp.json`** at repo root: entries for `unity` (CoplayDev Python server via `uv`) and `blender` (`uvx blender-mcp`).
- **`.claude/settings.json`** (or CLI flags): a scoped permission mode allowing the two MCP servers' tools plus file edits under the project, so the subprocess runs without interactive prompts (there is no human at the subprocess's stdin).
- Bind to `localhost` only (NFR-4) — both MCP servers already do this by default.

## 5. Data Flow (one prompt)

1. User types a request (e.g. "Create a third-person controller with a sci-fi drone player model") and clicks Send.
2. Window builds the context header (active scene, selection, recent console errors) and sends `header + prompt` to the subprocess.
3. Claude Code plans, then calls **Blender MCP** to generate the drone model and export it into `Assets/`.
4. Claude Code calls **Unity MCP** `manage_script` to create `DroneController.cs`, refreshes assets, reads the console, and auto-fixes any compile errors.
5. Claude Code calls **Unity MCP** `manage_gameobject` to create a GameObject, add `Rigidbody`/`Collider`/`DroneController`, and parent the imported model.
6. Stream events flow back into the chat and drive the status indicator; the Unity scene updates live.
7. User presses Play to test.

## 6. Non-Functional (prototype level)

- **NFR-1 Performance:** all subprocess I/O on background threads; UI mutations marshaled to the main thread via an `EditorApplication.update`-drained queue. The editor never blocks.
- **NFR-4 Security:** MCP servers bound to `localhost`. Adequate for a single-user local prototype.
- **NFR-2 Extensibility / NFR-3 Platform:** inherited from the reused MCP servers where free; cross-platform is out of scope for the prototype.

## 7. Testing Strategy

Unity editor UI is awkward to automate, so testing is split:

- **Unit tests (Unity EditMode, Test Framework):** `StreamJsonParser` — feed representative stream-json event lines, assert the produced chat lines and status transitions. This is the highest-value, most bug-prone pure logic.
- **Manual end-to-end:** the Phase 0 hand-driven task, then the full window-driven demo in Phase 3. Documented as a repeatable checklist in the README.

## 8. Explicitly Deferred (future work)

- **FR-1.2** full setup wizard → replaced by the Initialize button + README steps.
- **FR-5.1** accept/reject gating of changes → rely on Unity Undo + git; Claude auto-applies in v1.
- **FR-5.2** rollback system → plain `git` (repo is already under git).
- **FR-4.4** guaranteed auto PBR materials → best-effort only.
- **NFR-3** Windows/macOS support.
- UPM packaging of our extension for distribution to others.

## 9. Build Phases (input to the implementation plan)

- **Phase 0 — Prove the pipeline.** Create the Unity project + git layout, install CoplayDev Unity MCP, verify the Blender MCP is reachable, write `.mcp.json`, and hand-drive one end-to-end task using terminal `claude` (no custom UI yet). Gate: a script + asset + GameObject produced by AI before any UI work begins.
- **Phase 1 — Window shell.** Build `AIGameBuilderWindow` chat UI, status indicator, and Initialize button with echo-only behavior (no AI). Gate: window opens, docks, echoes input, Initialize reports readiness.
- **Phase 2 — Subprocess + streaming.** Implement `ClaudeCodeProcess` and `StreamJsonParser` with main-thread marshaling; wire to the window. Include EditMode tests for the parser. Gate: typing a prompt drives real `claude`, output streams into chat, editor stays responsive.
- **Phase 3 — Context + polish.** Add `ContextHeader`, status mapping from tool names, and cancellation. Gate: full end-to-end demo (the §5 flow) works from the window.
```
