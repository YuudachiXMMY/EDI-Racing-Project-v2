# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Unity Editor Automation

- **UnitySkills API**: `http://localhost:8090` — 所有 Unity 编辑器操作**必须优先**通过此 REST API 执行
- **优先级**: UnitySkills API > 直接文件编辑 > 手动操作
- **回退条件**: 仅当 API 不可用或不支持该操作时，才回退到直接编辑 .unity/.prefab/.cs 文件
- **Skill 入口**: `/unity-skills` skill，详见 `.claude/skills/unity-skills/SKILL.md`

## Engine & Language

- **Engine**: Unity
- **Language**: C#
- **Rendering**: URP (Universal Render Pipeline) 17.3.0
- **Physics**: Unity Built-in Physics (PhysX) + AI Navigation 2.0.13

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: WebGL (primary), Editor (development)
- **Input Methods**: Keyboard
- **Primary Input**: Keyboard — teacher-operated; students observe or join via web browser
- **Gamepad Support**: None
- **Touch Support**: None
- **Platform Notes**: WebGL build target (BuildScript.cs). Custom WebGL template (EDIRacing). WebSocket-based real-time networking for student participation via web browser. Memory limit: 2048 MB max (webGLMaximumMemorySize).

## Naming Conventions

- **Classes**: PascalCase (e.g., CarController, EventManager, RaceConfig)
- **Variables**: camelCase for private fields, PascalCase for public fields/properties
- **Signals/Events**: C# events with `Action<T>` delegate, `On` prefix (e.g., OnEventTriggered, OnStateChanged)
- **Files**: PascalCase matching class name (e.g., CarController.cs, RaceManager.cs)
- **Scenes/Prefabs**: Scenes: snake_case (complete_track_demo.unity); Prefabs: PascalCase (Car1.prefab, Car_Red.prefab)
- **Constants**: PascalCase (e.g., MaxRecoveryAttempts)

## Performance Budgets

- **Target Framerate**: 60 FPS (WebGL standard)
- **Frame Budget**: 16.67 ms
- **Draw Calls**: Not specified — optimize as needed for WebGL
- **Memory Ceiling**: 2048 MB (webGLMaximumMemorySize in ProjectSettings)

## Testing

- **Framework**: Unity Test Framework 1.6.0 + NUnit (EditMode tests)
- **Minimum Coverage**: 80% (per coding-standards.md)
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- [None configured yet — add as architectural decisions are made]

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here -->
- **nodemailer** (web-app runtime dependency) — SMTP client for password-recovery emails via the self-hosted Stalwart server. Pure-JS, no native build. Approved 2026-08-25.
- **QRCoder** (Unity runtime, vendored core only — `Assets/ThirdParty/QRCoder/`) — pure-C# QR generation for the host-screen student-join QR. Only the generator core is vendored (QRCodeGenerator/QRCodeData + Framework4.0Methods/Exceptions/Extensions polyfills, from tag v1.4.3); the `System.Drawing`-based renderers and the `PayloadGenerator` are excluded, so it is WebGL/IL2CPP-safe. QR pixels are rendered to a `Texture2D` by `QrCodeRenderer`. MIT license (see `Assets/ThirdParty/QRCoder/LICENSE.txt`). Approved 2026-09-04.

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# is the only language)
- **Shader Specialist**: unity-shader-specialist
- **UI Specialist**: unity-ui-specialist
- **Additional Specialists**: unity-addressables-specialist (if Addressables adopted), unity-dots-specialist (if DOTS adopted)
- **Routing Notes**: Project uses URP + UGUI + NavMesh. No DOTS/ECS. Shader work is minimal (asset store materials). UI is legacy UGUI (Text, Button, InputField).

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->
<!-- If a row says [TO BE CONFIGURED], fall back to Primary for that file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (`*.cs`) | unity-specialist |
| Shader / material files (`*.shader`, `*.shadergraph`, `*.mat`) | unity-shader-specialist |
| UI / screen files (UI-related `*.cs`, `*.uxml`, `*.uss`) | unity-ui-specialist |
| Scene / prefab / level files (`*.unity`, `*.prefab`) | unity-specialist |
| Native extension / plugin files (`*.jslib`) | unity-specialist |
| General architecture review | Primary |
