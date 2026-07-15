# Functional Requirements Document (FRD)
## Project: Unity MCP Editor Extension (AI Game Builder)

### 1. Executive Summary
The Unity MCP (Model Context Protocol) Editor Extension is an AI-assisted game development tool integrated directly into the Unity Game Engine. It enables developers to build games by describing their requirements in plain English. By leveraging the Model Context Protocol (MCP), the extension orchestrates communication between the Unity Editor, Claude Code (or alternative LLM clients), and a Blender MCP server. It automates the generation of Unity source code, scene hierarchies, components, and 3D assets (via Blender) entirely from a unified natural language interface.

---

### 2. System Architecture & Context
The system utilizes a decentralized, protocol-driven architecture where Unity acts as both the user interface and the primary execution environment.

```
+-----------------------------------------------------------------+
|                       Unity Editor                              |
|   +------------------+                    +-----------------+   |
|   |  Extension UI    |                    |  Unity Runtime  |   |
|   |  (Chat/Console)  |                    |  & Scene Graph  |   |
|   +--------+---------+                    +--------+--------+   |
|            |                                       ^            |
+------------|---------------------------------------|------------+
             |                                       |
             v                                       v
   +------------------+                    +-----------------+
   |   Claude Code    | <===============>  |    Unity MCP    |
   |   (LLM Client)   |     MCP Rules      |     Server      |
   +--------+---------+                    +-----------------+
            |
            | (Asset Generation Request)
            v
   +------------------+
   |   Blender MCP    |
   |     Server       |
   +------------------+
```

1. **Unity Editor Extension**: An editor window (`EditorWindow`) providing a chat interface, configuration panels, and asset synchronization status.
2. **Unity MCP Server**: A local or remote server implementing the Model Context Protocol, exposing Unity-specific tools (e.g., `create_script`, `instantiate_prefab`, `modify_scene_hierarchy`, `compile_project`).
3. **Claude Code / LLM Client**: The reasoning engine that processes natural language prompts, interprets the current Unity state, and executes tool calls via MCP.
4. **Blender MCP Server**: A specialized MCP server that exposes Blender's Python API (`bpy`) to generate, texture, and export 3D assets (`.fbx`, `.obj`) directly into the Unity project's `Assets/` directory.

---

### 3. Functional Requirements

#### 3.1 Installation & Setup (Ease of Use)
* **FR-1.1: Single-Package Installation:** The extension must be installable via a single git URL or a custom scoped registry using the Unity Package Manager (UPM).
* **FR-1.2: One-Click Setup Wizard:** Upon installation, a setup wizard must guide the user to configure paths for Claude Code, the Blender executable, and necessary API keys.
* **FR-1.3: Automated Server Lifecycle:** The extension must automatically launch the local Unity MCP server and Blender MCP server background processes when the Unity project opens, and terminate them when Unity closes.

#### 3.2 Plain-English Prompting Interface
* **FR-2.1: Integrated Chat Window:** A dockable `EditorWindow` (e.g., "Unity MCP Builder") containing a chat interface for inputting natural language instructions.
* **FR-2.2: Context-Aware Submissions:** Every prompt sent to the LLM must automatically attach the current context, including the open scene hierarchy, selected game objects, and compiler errors (if any).
* **FR-2.3: Real-Time Token/Status Streaming:** Visual indicators showing what the AI is currently doing (e.g., "Generating Script...", "Compiling...", "Blender Modeling...").

#### 3.3 Unity Code Generation & Scene Manipulation
* **FR-3.1: Automated C# Script Generation:** The extension must allow Claude Code to create new C# scripts in the `Assets/Scripts/` folder, ensuring proper namespace compliance and inheriting from `MonoBehaviour`.
* **FR-3.2: Automated Component Attachment:** The AI must be able to attach generated scripts to existing or newly created GameObjects in the active scene.
* **FR-3.3: Scene Graph Manipulation:** Ability to add, delete, rename, and transform (Position, Rotation, Scale) GameObjects within the active Unity Scene via natural language commands.
* **FR-3.4: Live Compilation Hook:** After script generation, the extension must trigger Unity's internal compilation loop (`AssetDatabase.Refresh()`) and report compilation errors back to Claude Code for auto-fixing.

#### 3.4 Blender MCP Integration for 3D Asset Generation
* **FR-4.1: Natural Language Asset Requests:** Users can type commands like *"Generate a low-poly medieval sword and place it in the scene"*.
* **FR-4.2: Procedural/Generative Blender Modeling:** The tool will route the request to the Blender MCP server, which compiles procedural geometry scripts or interfaces with 3D generative APIs.
* **FR-4.3: Automated Export/Import Pipeline:** The Blender MCP server must automatically save the generated asset as `.fbx` or `.blend` directly inside `Assets/Models/`.
* **FR-4.4: Auto-Material & Texture Mapping:** Generated models must automatically have standard PBR materials created and assigned within Unity based on the description.

#### 3.5 Game Loop & Safety Control
* **FR-5.1: Execution Sandbox Prompting:** Users must review and approve code changes or critical scene overwrites via an explicit "Accept / Reject" UI before execution.
* **FR-5.2: Rollback System:** A local Git or temporary cache-based system inside the extension allowing users to undo the last generation cycle.

---

### 4. Non-Functional Requirements

* **NFR-1: Performance:** Script creation and asset importing must not block the Unity Editor UI thread (use asynchronous tasks / `EditorApplication.update` loops).
* **NFR-2: Extensibility:** The MCP tool definitions must follow standard JSON-RPC schema so new tools can be added without breaking backwards compatibility.
* **NFR-3: Platform Compatibility:** The Unity extension and underlying local MCP servers must support Windows (10/11) and macOS (Intel & Apple Silicon).
* **NFR-4: Security:** Local server communication must be bound strictly to `localhost` with authorization tokens to prevent remote command execution.

---

### 5. Step-by-Step User Workflow
1. Developer opens **Unity -> Window -> AI Game Builder**.
2. Developer clicks "Initialize" (starts Unity and Blender MCP daemons).
3. Developer types: *"Create a basic third-person controller with a sci-fi drone player model."*
4. **Claude Code** receives the command -> Invokes **Blender MCP** tool `generate_model(style="sci-fi drone")`.
5. Blender opens in background, saves `drone.fbx` directly into Unity `Assets/Art/`.
6. **Claude Code** creates `DroneController.cs` via **Unity MCP** tool `create_script()`.
7. Unity re-compiles; **Claude Code** creates a GameObject, adds the `Rigidbody`, `Collider`, and `DroneController` components, then attaches the `drone.fbx` as a child.
8. Developer hits Play in Unity to test immediately.
