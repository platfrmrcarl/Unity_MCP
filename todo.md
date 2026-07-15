# AI Game Builder — TODO / Future Work

Ideas and known gaps for the prototype, roughly in priority order within each
group. Nothing here blocks current use; the prototype is functional and proven
end-to-end. See `README.md` for how it works and
`docs/superpowers/specs/2026-07-14-unity-mcp-ai-game-builder-design.md` for the
original scope decisions.

## Chat & UX polish

- [ ] **Stream assistant text into the bubble as it arrives.** Today each
  assistant text block appears when its stream-json line lands. Pass
  `--include-partial-messages` to `claude` and handle partial/delta events in
  `StreamJsonParser` so a bubble fills in token-by-token like a real chat.
- [ ] **Markdown rendering in AI bubbles.** Render code blocks, bold, and lists
  (IMGUI `richText` for basic styling, or a small markdown-to-rich-text pass).
  Code blocks especially — the AI often shows C# snippets.
- [ ] **Copy button** on messages / code blocks.
- [ ] **Message timestamps** (small, greyed, under each bubble).
- [ ] **Collapse tool activity.** Group consecutive `· tool` lines into an
  expandable "used N tools" row so long runs stay readable.
- [ ] **Show token/cost usage** from the final `result` event (it carries
  usage + cost) in the toolbar or under the last message.
- [ ] **"Jump to latest" button** when scrolled up and new messages arrive.
- [ ] **Persist the conversation across domain reloads.** State resets whenever
  Unity recompiles; serialize `_messages` (e.g. `[SerializeField]` backing list
  or `SessionState`) so a recompile mid-chat doesn't wipe the transcript.
- [ ] **New Chat / Clear button** to start a fresh conversation (resets
  `_conversationStarted` so the next send drops `--continue`).

## Robustness / hardening (from code review)

- [ ] **Kill the whole process tree on Cancel.** `Process.Kill(entireProcessTree)`
  needs netstandard2.1/.NET Core; the project is .NET Standard 2.0, so Cancel
  can briefly orphan a stdio MCP child. Options: bump the API compatibility
  level, or shell out to `pkill -P <pid>` before `Kill()`.
- [ ] **Clear the event queue on Cancel/re-send.** Buffered `OutputDataReceived`
  callbacks from a killed process can enqueue after a new run starts, bleeding a
  couple of stale transcript lines into the next conversation. Flush `_events`
  in `Cancel()` / at the top of `Send()`.
- [ ] **De-hardcode the repo root.** `AIGameBuilderWindow.OnEnable` hardcodes
  `/home/carl/GitHub/Unity_MCP`. Derive it from
  `Application.dataPath` (`Assets/`'s parent) so the project is portable.
- [ ] **Process cleanup.** Add `WaitForExit()`/`Dispose()` after `Kill()`; wire
  or drop the unused `EnableRaisingEvents`/`Exited` config.
- [ ] **Status granularity.** `BuilderStatus.Compiling` is currently unreachable
  (Unity compiles arrive as a `mcp__unity__` tool call → `EditingScene`). Either
  detect real compilation and use it, or remove the dead enum value.
- [ ] **Configurable `claude` path.** If `claude` isn't on the editor's PATH
  (launched from the desktop rather than a terminal), let the user set an
  absolute path instead of failing.

## Deferred FRD features (intentional v1 cuts)

- [ ] **FR-5.1 Accept/Reject gating.** Review changes before they apply, instead
  of Claude applying directly (currently rely on git/Undo to roll back).
- [ ] **FR-5.2 In-app rollback** beyond plain git (e.g. snapshot per generation).
- [ ] **FR-1.2 Setup wizard.** Replace the Initialize button + README with a
  guided first-run wizard (checks claude/uv/Blender, starts the bridge).
- [ ] **FR-1.1 Single-package UPM distribution** so others can install the
  extension via one git URL.
- [ ] **FR-4.4 Guaranteed auto PBR materials** on generated Blender assets
  (currently best-effort).
- [ ] **NFR-3 Windows / macOS support** (paths + the Flatpak Blender assumption
  are Linux-specific today).

## Nice-to-haves

- [ ] Attach a viewport screenshot or selected-object details as extra context.
- [ ] Quick-action buttons / prompt presets ("add a light", "make it night").
- [ ] Surface Blender viewport screenshots inline after asset generation.
- [ ] Per-conversation model/effort selection.
