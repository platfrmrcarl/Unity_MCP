# AI Game Builder — Unity MCP Prototype

Build games in Unity by typing plain English. A dockable Unity editor window
("AI Game Builder") drives the [Claude Code](https://claude.com/claude-code)
CLI as a background subprocess. Claude Code connects out to two MCP servers —
the **Unity MCP** bridge (create scripts, manipulate the scene, compile) and
the **Blender MCP** (generate 3D assets) — and does the actual work. Scene
changes appear live; press Play to test.

This is a **personal Linux prototype**, not a shippable product. See
[Known limitations](#known-limitations) and [Deferred](#deferred-from-the-frd).

## Architecture

```
Unity Editor (AI Game Builder window)
        | spawns:  claude -p --output-format stream-json --mcp-config .mcp.json ...
        v
   Claude Code CLI
    |            |
    v            v
 unity MCP    blender MCP
 (HTTP 8080)  (uvx blender-mcp, stdio)
    |            |
 CoplayDev    Flatpak Blender + BlenderMCP addon
 bridge in    (asset generation / export -> Assets/)
 the editor
```

The window is only a chat UI + subprocess driver. Claude Code owns the agent
loop, MCP orchestration, streaming, and tool permissioning. Unity manipulation
comes from the third-party [CoplayDev MCP for Unity](https://github.com/CoplayDev/unity-mcp)
package; 3D assets come from the [Blender MCP](https://github.com/ahujasid/blender-mcp).

## Prerequisites

All verified on this machine (Linux / CachyOS):

- **Unity `6000.5.3f1`** (installed via Unity Hub). Linux editor.
- **Claude Code CLI** (`claude`) on `PATH`, logged in.
- **`uv` / `uvx`** on `PATH` (runs the Python MCP servers).
- **Blender** as a Flatpak: `org.blender.Blender` (`flatpak run org.blender.Blender`).
- The Unity packages install automatically from `Packages/manifest.json`
  (`com.coplaydev.unity-mcp`, `com.unity.nuget.newtonsoft-json`).

## Setup

1. **Open the project** in the Unity editor. First open resolves the packages
   (CoplayDev MCP for Unity + Newtonsoft.Json) — wait for it to finish compiling.
2. **Start the Unity MCP bridge.** Open **`Window → MCP for Unity`**. Confirm the
   server is running (it listens on `http://127.0.0.1:8080/mcp`). If Claude Code
   isn't configured yet, use **Configure All Detected Clients** in that window.
   The editor must stay **open** for the bridge to serve requests.
3. **(For 3D assets) have Blender reachable.** The Blender MCP server is
   `uvx blender-mcp`. It talks to a running Flatpak Blender that has the
   BlenderMCP addon enabled and its socket server started (port 9876). If Blender
   isn't running, the AI can launch it for you, but starting it yourself is more
   reliable.
4. **Open the builder.** **`Window → AI Game Builder`**. Click **Initialize** — it
   checks that `claude` is on `PATH` and reminds you the bridge must be running.
5. **Type a request** and click **Send.** Watch the transcript and the status
   indicator (`Thinking → Editing scene / Modeling in Blender → Idle`). Use
   **Cancel** to stop a run.

Configuration lives in:
- `.mcp.json` — the `unity` (HTTP) and `blender` (stdio) servers the subprocess loads.
- `.claude/settings.local.json` — auto-approves the project MCP servers and allows
  their tools without prompts (this file is git-ignored / local-only).

## Manual end-to-end checklist

With the editor open and the bridge running, in the AI Game Builder window:

1. **Unity path.** Send: *"Create a Sphere named TestBall in the active scene."*
   → a Sphere appears; status cycles and returns to Idle; the editor stays
   responsive.
2. **Script + component.** Send: *"Create Assets/Scripts/PlayerMove.cs that moves
   its GameObject with WASD, then create a Capsule named Player, add a Rigidbody,
   and attach PlayerMove."* → script compiles with no console errors; the Capsule
   has Rigidbody + PlayerMove. Press **Play** and confirm WASD moves it.
3. **Continuity.** Send *"Create a Cube named Foo."* then *"Rename it to Bar."* →
   the second request acts on the same conversation (renames the Cube), proving
   `--continue` threads context.
4. **Blender path (best-effort).** Send: *"Generate a low-poly rock and export it
   into Assets/Models/."* → a model file lands in `Assets/Models/`.

## Known limitations

- **Editor must be open.** The Unity MCP bridge is hosted through the running
  editor; nothing works if the editor is closed.
- **Blender asset generation is best-effort.** `execute_blender_code` (procedural
  geometry) works whenever Blender is running with the addon. Generative methods
  (Hyper3D/Rodin, etc.) need their own API keys and may be unavailable. Auto PBR
  materials are not guaranteed.
- **Cancel can briefly orphan a child MCP process.** The project targets
  .NET Standard 2.0, which lacks `Process.Kill(entireProcessTree)`, so Cancel
  kills the `claude` process but a stdio MCP child may linger briefly.
- **No accept/reject gating.** Claude applies changes directly. To undo, use
  **git** (`git restore` / `git checkout` / `git reset`) or Unity's Undo.
- **Linux only.** Paths and the Flatpak Blender assumption are Linux-specific. The
  repo root is currently hardcoded in the window (`AIGameBuilderWindow.OnEnable`).

## Deferred from the FRD

Intentionally out of scope for this prototype (see
`docs/superpowers/specs/2026-07-14-unity-mcp-ai-game-builder-design.md`):

- **FR-1.1** single-package UPM distribution of this extension.
- **FR-1.2** one-click setup wizard (replaced by the Initialize button + this README).
- **FR-5.1 / FR-5.2** in-app accept/reject and rollback UI (use git).
- **FR-4.4** guaranteed auto PBR material mapping.
- **NFR-3** Windows / macOS support.

## Layout

- `Assets/AIGameBuilder/Editor/` — the extension: window, subprocess driver,
  stream-json parser, status mapper, context header.
- `Assets/AIGameBuilder/Tests/Editor/` — EditMode unit tests for the parser and mapper.
- `docs/superpowers/` — the design spec and implementation plan.
- `.mcp.json`, `.claude/settings.local.json` — MCP + permission config.
