 ▐▛███▜▌   Claude Code v2.1.89
▝▜█████▛▘  Opus 4.6 (1M context) · Claude Max
  ▘▘ ▝▝    ~/UnityProjects/EDI-Racing-Project-v2

⏺ Now I have a complete picture. Here's the gap analysis:

  ---
  PRP Gap Analysis Report

  Summary

  3 PRDs, 33 completed plans, 32 reports — 项目整体完成度很高。但有以下 6 
  个明确的 gap 需要处理：

  ---
  GAP 1: PRD Phase 6 未完成 — Unity 端 Survey 代码清理 (HIGH)

  来源: edi-survey-web-app.prd.md Phase 6 状态为 pending

  Survey Web App 已完全上线，但 Unity 端的旧 survey 代码仍然存在：

  ┌───────────────────────────────────────────┬──────┬────────┐
  │                   文件                    │ 行数 │  状态  │
  ├───────────────────────────────────────────┼──────┼────────┤
  │ Assets/Scripts/UI/SurveyBuilderPanel.cs   │ 502  │ 待移除 │
  ├───────────────────────────────────────────┼──────┼────────┤
  │ Assets/Scripts/UI/StudentSurveyPanel.cs   │ 495  │ 待移除 │
  ├───────────────────────────────────────────┼──────┼────────┤
  │ Assets/Scripts/Network/SurveyCollector.cs │ 163  │ 待移除 │
  └───────────────────────────────────────────┴──────┴────────┘

  共 1160 行 dead code。另有 7 个文件仍引用这些组件（SetupScreen.cs,
  NetworkSync.cs, RaceUI.cs, ConfigManagerPanel.cs, RuntimeSetup.cs, 以及两个
  Editor 脚本）。

  建议: 创建 Phase 6 的 implementation plan 并执行清理。

  ---
  GAP 2: 缺少报告 — engg1100-survey-template (LOW)

  engg1100-survey-template.plan.md 已在 completed/ 目录中，但没有对应的 report 
  文件。所有其他 32 个 completed plan 都有对应 report。

  建议: 补写 engg1100-survey-template-report.md 以保持 PRP 流程完整性。

  ---
  GAP 3: technical-preferences.md — 25 项 [TO BE CONFIGURED] (MEDIUM)

  项目已经实质完成，但 .claude/docs/technical-preferences.md 中仍有 25 
  个未配置项：
  - Rendering pipeline / Physics engine（已经在用 URP + AI Navigation）
  - Naming conventions（6 项 — 代码已有实际约定）
  - Performance budgets（4 项 — WebGL 目标已明确）
  - Testing framework / coverage（2 项 — 已用 Unity EditMode tests）
  - Engine specialists / file routing（7 项）

  这些大多可以从现有代码和 PRD 决策中推导出来并填写。

  建议: 根据已有代码实际约定填写，而非留空。

  ---
  GAP 4: 测试覆盖不足 (MEDIUM)

  当前只有 5 个 EditMode 单元测试：
  - CsvParserTests.cs
  - ResultsExporterTests.cs
  - RuleEngineTests.cs
  - SurveyResponseMapperTests.cs
  - JsonImporterTests.cs

  缺失的测试领域：
  - Web App（web-app/）— 零测试
  - Unity 核心系统：EventManager, CarSpawner, RaceManager, NetworkSync 无测试
  - 集成测试 / E2E 测试 — 不存在
  - 覆盖率远低于 coding standards 规定的 80% 目标

  ---
  GAP 5: 架构文档缺失 (LOW)

  Coding standards 要求 "Every system must have a corresponding architecture
  decision record in docs/architecture/"，但该目录仅有 tr-registry.yaml 一个文件
   — 没有任何 ADR。

  虽然 PRD 的 Decisions Log 部分记录了关键决策，但没有按 ADR 格式存放在
  docs/architecture/。

  ---
  GAP 6: CLAUDE.md 引用了错误的引擎参考 (LOW)

  CLAUDE.md 中有 @docs/engine-reference/godot/VERSION.md，但本项目是 Unity 
  项目，不是 Godot。这会误导 agent 去读 Godot 文档。应改为
  @docs/engine-reference/unity/VERSION.md。

  ---
  Priority Ranking

  ┌──────────┬─────────────────────────┬────────┬───────────────────────────┐
  │ Priority │           Gap           │ Effort │          Impact           │
  ├──────────┼─────────────────────────┼────────┼───────────────────────────┤
  │          │ GAP 1: Unity survey     │        │ 消除 1160 行 dead         │
  │ P0       │ 代码清理                │ Medium │ code，完成最后一个        │
  │          │                         │        │ pending phase             │
  ├──────────┼─────────────────────────┼────────┼───────────────────────────┤
  │          │ GAP 3:                  │        │                           │
  │ P1       │ technical-preferences   │ Small  │ agent 行为一致性          │
  │          │ 填写                    │        │                           │
  ├──────────┼─────────────────────────┼────────┼───────────────────────────┤
  │ P1       │ GAP 6: CLAUDE.md        │ Tiny   │ 防止 agent 误读 Godot     │
  │          │ 引擎引用修正            │        │ 文档                      │
  ├──────────┼─────────────────────────┼────────┼───────────────────────────┤
  │ P2       │ GAP 4: 测试覆盖补充     │ Large  │ 长期维护质量              │
  ├──────────┼─────────────────────────┼────────┼───────────────────────────┤
  │ P2       │ GAP 2: 补写 report      │ Tiny   │ PRP 流程完整性            │
  ├──────────┼─────────────────────────┼────────┼───────────────────────────┤
  │ P3       │ GAP 5: ADR 文档         │ Medium │ 架构可追溯性              │
  └──────────┴─────────────────────────┴────────┴───────────────────────────┘

  ---
  要针对哪个 gap 创建 implementation plan？最推荐从 GAP 1（Unity survey
  清理）开始，因为它是唯一一个 PRD phase 仍在 pending 的项目。

✻ Baked for 2m 23s